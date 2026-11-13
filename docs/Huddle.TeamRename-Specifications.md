# Huddle.TeamRename — Design Specification

**Date:** 2026-11-12, revised 2026-11-12 after an independent review · **Status:** Proposed, not built ·
**Decision record:** none yet; §9 holds the decisions an ADR would record, and that ADR amends
[ADR-0055](adr/0055-a-reference-is-text-with-a-stable-key.md) OP-1 ("Huddle offers no Team rename") ·
**Builds on:** [ADR-0011](adr/0011-a-rename-moves-the-agent-not-its-history.md) (an Agent rename),
[ADR-0052](adr/0052-a-room-belongs-to-the-root-or-to-one-team.md) and
[ADR-0082](adr/0082-a-room-may-belong-to-one-project-of-its-team.md) (Room Scope),
[ADR-0065](adr/0065-huddle-posts-as-itself.md) (Huddle posts as itself),
[ADR-0075](adr/0075-a-second-brain-layout-in-every-team.md) (the Team folder layout)

This is the design for **renaming a Team or a Project** from the app. Today neither can be renamed, and
the Team Name is the Team's only identity, so a folder renamed by hand reads as one Team vanishing and
another appearing.

It is written for the engineers or agents building it, with no memory of the conversation that produced
it. §4 lists every place that holds a Team or Project name, §5 is the rename itself, §6 is how Agents
that already have context learn of it, §7 is the confirmation the Human sees, and Appendix A is the
ordered, test-first task list.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides either.

---

## 1. Goal

1. **One action renames a Team or a Project everywhere Huddle stores its name.** Rooms, Tasks, Requests,
   saved Views, ids, cadences and file history follow; nothing is archived, duplicated or re-created.
2. **Agents whose context holds the old name are told,** once per session, on their next Message Turn.
   No Turn is started just to tell them.
3. **The Human sees it in chat.** The Team Room gets one line from Huddle saying what changed. It wakes
   no one.
4. **An old name is caught, not silently misused.** A read that uses it is forwarded; a write is refused
   with a hint naming the new name.
5. **The Human knows the cost before confirming.** The confirmation lists what can go wrong.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| R1 | The Human renames *Marketing* to *Growth* from the Team's **Details** tab | The name dialog checks the new name, then the confirmation (§7) opens. Nothing changes until **Rename** |
| R2 | The rename completes, with three or more members | `Teams/Growth/` exists, `Teams/Marketing/` does not and is not re-created, every member's `teams:` line says *Growth*, and the sidebar shows one Team |
| R3 | *Marketing* had a Team Room and five other Rooms | All six are listed under *Growth*. None is archived and no second Team Room is created. The Team Room is named *Growth* unless the Human had renamed it by hand |
| R4 | *Marketing* had 40 open Tasks | Their ids keep the `MARK-` prefix and the counter continues. No Task gains a Change log line and no assignee is woken |
| R5 | *Research* filed a Request into *Marketing*'s backlog, and *Marketing* filed one into *Research*'s | The *Research* copy now says `from_team: Growth`. Wakes, notifications and QA confirmation work for both, and no *Research* Task is reported as an outside edit |
| R6 | Nova, a *Marketing* member, takes its next Message Turn after the restart | Nova's session is fresh and its system prompt names *Growth*. Its Catch-up holds Messages from before the rename, so the Turn starts with the rename notice (§6.2) |
| R7 | Bo, the *Research* manager, has a session still open | Bo's next Message Turn in that session starts with the notice |
| R8 | The Team Room after the rename | One line from Huddle: "This Team was renamed from Marketing to Growth." No Agent takes a Turn because of it |
| R9 | Nova reads Team Memory with scope `Marketing` | The read is forwarded to `Growth`, and the result starts with "Team 'Marketing' was renamed to 'Growth'" |
| R10 | Nova writes Team Memory with scope `Marketing` | Refused: "Team 'Marketing' was renamed to 'Growth'. Use 'Growth'." Nothing is written |
| R11 | The Human opens an old Message linking to the *Marketing* Team, or an upload under `Teams/Marketing/raw/` | The Team link opens *Growth*'s page, and the upload opens from `Teams/Growth/raw/` |
| R12 | The Human renames the *Launch* Project of *Growth* to *Launch-2027* | Its Rooms, Tasks, Views and history follow. No Agent is restarted. Agents with open sessions in *Growth*'s Rooms get the notice |
| R13 | The Human renames *Growth* to *growth* (case only) | It works, through a temporary name (§5.5) |
| R14 | The Human types a name another Team, an archived Team, a Project of this Team, or a name in the rename journal already uses | The name dialog refuses it and says which one |
| R15 | A Team folder is renamed by hand in Explorer | Unchanged from today: the old Team's Rooms are archived ([TeamChats](Huddle.TeamChats-Specifications.md) T11). Only the in-app rename is a rename |
| R16 | Huddle stops between the folder move and the label rewrite | On the next start the rename is finished from its journal row (§5.4). No *Marketing* folder or Team Room is re-created |

---

## 3. What does not change

- **A Team's Name stays its identity.** This spec introduces no Team id (D-1).
- **Transcripts are not rewritten.** They are append-only, and a rename is not a retcon (ADR-0011).
  Messages keep the old name; their links are resolved through the journal (§6.3).
- **Feed rows are not rewritten.** The Feed's Team filter matches old rows through the journal.
- **An Agent's own memory files are not rewritten.** The notice asks the Agent to update them.
- **A hand rename in Explorer is not detected as a rename** (R15, D-2).
- **A freed name stays reserved.** A name in the rename journal cannot be given to another Team, so an
  old name never points at two Teams (D-7).

---

## 4. Every place a name is held

Found by a sweep of `src/Huddle.App` on 2026-11-12 and corrected by the review. "Rewrite" means §5
changes it; "Journal" means it keeps the old name and is resolved through the rename journal (§6.3).

| Area | Where | Holds | Handling |
| --- | --- | --- | --- |
| Folder | `Teams/<Team>[/<Project>]/`, including `_tasks/`, `raw/`, `memory/`, `outputs/`, `AGENTS.md`, `_team.md` | Path | Move (`ArchiveMove`) |
| Archive | `Archive/<Team>/` (archived Projects of the Team) | Path | Move (`ArchiveMove`) |
| Membership | Persona frontmatter `teams:` (`TeamMembership`, `PersonaFrontmatter`) | Team Name | Rewrite, in one batch (§5.3) |
| Rooms | `room_teams(team)` (`SqliteTeamDirectory`) | Team Name | Rewrite (new `RenameTeamScopeAsync`) |
| Rooms | `rooms.name` of the Team Room | Team Name | Rewrite only while it still equals the old Name (ADR-0011) |
| Rooms | `room_projects(project)` | Project name | Rewrite |
| Ids | `task_prefixes(team)` (`TaskIdAllocator`) | Upper-cased Team Name | Rewrite, keeping the prefix and counter. A leftover row for the new name (rows are never deleted) is refused in §5.5 |
| Cadence | `cadence_state(team)` (`CadenceStateStore`) | Team Name | Rewrite; a leftover row for the new name is replaced |
| Requests | `from_team:` in Task files of every Team (`RequestFields`) | Team Name | Rewrite, with those Teams paused (§5.3) |
| Attachments | Absolute paths in Task bodies (`TaskAttachmentText`) | Path | Rewrite |
| Views | `{DataDir}/views.json`, `TaskFilter.Teams`, `.Projects` (`ViewStore`) | Names | Rewrite (`ViewStore.RenameTeam`, `RenameProject`, after `RenameAgent`) |
| Workspace | `_team.md` `workspace:`, an absolute path | Path | Rewrite when it lies inside the moved folder |
| Library root | `workspace-{team}` root id (`LibraryRootStore`) | Slug of the Team Name | Rewrite the id, and move the doc-history keyed by it |
| History | `{DataDir}/doc-history/teams/<Team>/…` (`DocHistoryStore`) | Path | Move (`DocHistoryStore.Move`) |
| Ledgers | `agents-md-ledger` (hash of the relative path), `file-state/<Agent>.json` (`team:<Label>` entries, `Subscribed` paths), `raw_baselines(path)` | Paths | Rewrite |
| Watches | Persona `watches:` paths under `Teams/<Team>/`, on any Agent | Path | Journal; not rewritten, because rewriting a non-member's Persona text restarts it (D-8) |
| Feed | `feed_events.payload` `$.data.team`, `FromTeam`; `target_key` of TeamArchived | Team Name | Journal |
| Messages | Transcript text: upload paths, Team References, Library-file References under the folder (ADR-0055) | Name, path | Journal (path prefix map) |
| UI state | localStorage `huddle:tabs`, `shell:teamShortcuts`, `teamsNav:expanded`, `library:team:*`, `roomList:closedFolders` | Names | Rewrite on the next circuit, from the journal |
| Agents | System prompt, Catch-up, open sessions, the Agent's memory | Names, paths | §6 |

---

## 5. The rename

### 5.1 Who and where

A Team is renamed from its **Details** tab (a pencil beside the Name) and from its row's More (⋮) menu
in the sidebar. A Project is renamed from its row's More (⋮) menu. Only the Human can rename; there is no
Agent tool.

### 5.2 Locks

A new `TeamRenameService` copies `ArchiveService`'s shape, not a single lock around everything:

- **A service-level `SemaphoreSlim`** serialises renames and archives of the same Team. The async work
  (`team.db`, Task files) runs under it.
- **`ITeamFolders.RunExclusive` wraps only the folder moves.** It is a synchronous `lock`, and the
  documented lock order forbids holding `folderGate` while a membership change runs
  (`TeamFolderProvisioner`), so the label rewrite runs outside it.
- **`TaskStore.PauseWrites`** is held for the old name, the new name, and **every Team whose Task files
  step 6 rewrites** (the Teams with a `from_team:` of the old name). Files are written directly while
  paused, and lifting the pause adopts them as seen, so nothing is reported as an outside edit.
- **A reconciler suspend scope** (`TeamRoomReconciler.Suspend()`, taking the reconciler's own gate) is
  held from the folder move to the end, then released with one reconcile. While suspended, no catalog
  change creates or archives a Team Room.
- **The provisioner skips a name with a pending journal row.** `TeamFolderProvisioner.SyncFoldersLocked`
  does not create a folder for a label whose rename is in progress, so `Teams/Marketing/` is not
  re-created while some labels still say it, nor at startup after a crash.

### 5.3 The order

1. Validate the new name (§5.5). List the members that are mid-Turn for the confirmation (§7).
2. Write a `team_renames` row with `state = pending`.
3. Take the pauses and suspend the reconciler.
4. Move the folder, then `Archive/<Old>/` if it exists, each through `ArchiveMove` (staging folder, retry
   while a file is in use).
5. In one `team.db` transaction: `room_teams`, the Team Room's `rooms.name` (if still auto-named),
   `task_prefixes`, `cadence_state`.
6. Rewrite `from_team:` in the paused Teams' Requests, the Task attachment paths, `views.json`,
   `_team.md` `workspace:`, the Library root id, the ledgers and `raw_baselines`. Move doc-history.
7. Rewrite every member's `teams:` line **in one batch**: a new `PersonaStore.UpdateMany` that writes
   every file, then raises `PersonasChanged` once. This rebuilds the catalog and restarts the members.
8. Post the Team Room line (§6.4).
9. Mark the journal row `state = done`, release the reconciler with one reconcile, and lift the pauses.

### 5.4 Failure and recovery

- **Before step 4 finishes,** a failure undoes the move through `ArchiveMove`, deletes the pending row,
  and the dialog says which file was locked. The Team is as it was.
- **From step 5 on, the rename rolls forward, never back.** A failed step is retried; a step that still
  fails is listed in one Feed Notification, and the row stays `pending`.
- **At startup,** `TeamRenameService.RecoverAsync` finishes every `pending` row from its first unfinished
  step, before the provisioner and the reconciler run. Every step is idempotent, so running one twice is
  harmless.

### 5.5 Name rules

- The new name is entered in the house `NameDialog`; its `Validate` applies these rules, so the Human
  types it once.
- The Team Name rules apply (`TeamNames`): no `,` `;` `[` `]`, no leading `_` or `.`, the Library's
  file-name rules, unique regardless of case **among live Teams, archived Teams and names in the rename
  journal**.
- A `task_prefixes` row already held by the new name (from an earlier, deleted Team) is refused with a
  plain sentence, because its prefix and counter would collide.
- A Project name follows the Project rules (not `memory`, `raw`, `outputs`, no leading `_` or `.`), and is
  unique among the Team's Projects, live and archived.
- **A case-only rename** leaves the Team itself out of the uniqueness check
  (`TeamNames.ValidateTeamName` refuses it today) and moves through a temporary name, because Windows
  treats the two as one folder.

---

## 6. Agents that already have context

### 6.1 What each Agent has after the rename

An Agent's picture of its Team comes from its system prompt, its conversation (including Catch-up) and
its own memory. Who has the old name where:

| Agent | After the rename | Notice |
| --- | --- | --- |
| **A member of the renamed Team** | Restarted by step 7. A restart deletes its Room Sessions (`RoomSessionStore.ForgetAll`, rules.md "Restart forgets Room Sessions"), so every Room's next Turn opens fresh: the system prompt names *Growth*, but the Catch-up replays Messages that say *Marketing*. The working context of each session is lost, on every Adapter | Yes, in each Room whose Catch-up holds Messages from before the rename |
| **Any other Agent with an open or resumed session** | Not restarted. Its system prompt is built from its own labels, so it never named the Team, but its conversation may. A session still open in the same process keeps its old system prompt (`DotAcpPersonaHost.ResumeAsync`) | Yes, on its next Message Turn in that session |
| **An Agent whose session opens after the rename with no older Messages** | Everything it sees already uses the new name | No |

A **Project rename** restarts no one: every affected session is open or resumed, and is told.

### 6.2 The rename notice

A new `TeamRenamedTurnHook`, built like `AgentsMdTurnHook`, adds one block to the start of a Message Turn:

> Team 'Marketing' is now called 'Growth'. Its folder moved from `Teams/Marketing` to `Teams/Growth`.
> Earlier Messages and your own notes use the old name and old paths; use the new ones from now on, and
> update your own memory notes if they name the old Team.

A Project rename has the same text with the Team-qualified Project name.

- **Who gets it: every session that is not fresh, once,** plus a fresh session whose Catch-up starts
  before the rename. This needs no rule about who is involved with the Team: the notice is one short
  block, and sending it to an Agent that never knew the Team costs a sentence. It also covers the Chief
  of Staff and any Agent that read the Team's files.
- **Once per session:** the hook state holds the id of the last `team_renames` row the session has heard.
  - A **fresh** open records the latest row whose rename is older than the oldest Message in its Catch-up,
    so it hears the renames its Catch-up predates and nothing else.
  - A **resumed** or still-open session hears every `done` row after its recorded id.
  - A session with **no hook state** (opened before this feature) hears the `done` rows from the last
    14 days.
- **Message Turns only.** Hooks run only for `WorkItemKind.Message` (`RoomSession.cs`, `RunBeforeHooksAsync`),
  which is how Mentions, Room posts and Task wakes arrive. A Command Turn such as `@Nova /compact` does not
  carry it; the next Message Turn does.
- The notice text is a Prompt in `prompts.default.json`, so the Human can edit it (ADR-0007), and a
  golden pins it.
- **Project rename only:** `AgentsMdTurnHook` maps a heard Project-level `AGENTS.md` path to its new
  place, so open sessions see the same file rather than a removal and an addition. A Team rename needs
  no mapping, because its members' sessions are gone and `AgentsMdScopes` covers only an Agent's own
  Teams.

### 6.3 The rename journal and redirects

`team_renames(id, kind, team, old_name, new_name, state, renamed_at)` in `team.db` records every rename.
It is a sibling table, because `CREATE TABLE IF NOT EXISTS` cannot add columns to an existing database.
It is the notice's source, the recovery record and the alias table.

`ITeamRenames.Resolve(name)` follows a chain of renames to today's name. Because a journalled name is
reserved (§5.5), it is never also a live name.

| Caller | On an old name |
| --- | --- |
| `memory` reads (`list`, `read`) with scope `<Team>` or `<Team>/<Project>` | Forwarded; the result starts with "Team 'Old' was renamed to 'New'" |
| `memory` writes, `file_request` `toTeam`, any other tool that writes | Refused: "Team 'Old' was renamed to 'New'. Use 'New'." (D-6) |
| `search` Team scope | Forwarded with the note |
| Team References and `/teams/{Team}` routes | Redirect to the new name |
| Upload links and Library-file References under `Teams/Old/` | A path prefix map `Teams/Old/` to `Teams/New/` in the opener |
| The Feed's Team filter | Matches rows carrying any earlier name |
| Persona `watches:` paths under `Teams/Old/` | Resolved through the prefix map by `WatchedFolderResolver` |
| Process gates (`ProcessGateService`) | Read through `Resolve`, so a gate never fails open on a label that still says the old name |
| UI state in localStorage | Rewritten on the next circuit |

**An Agent's own file tools** go straight to disk. A Read of `Teams/Old/x` finds nothing; a **Write**
creates `Teams/Old/` and the catalog would list it as a Team. A guard in `WorkDirPermissionHandler`
refuses a Write under a journalled folder with the same hint, if spike V-2 shows those calls reach it.
Until then the confirmation says so (§7).

### 6.4 The Team Room line

Step 8 posts one line into the Team Room through `ChatService.PostAsHuddleAsync` (ADR-0065):

> This Team was renamed from **Marketing** to **Growth**.

A Project rename posts "Project **Launch** was renamed to **Launch-2027**." into the Team Room.

- **It mentions no one, so it wakes no one.** It spends no Budget, answers no Mention and clears nothing
  (ADR-0065). Spike V-3 confirms the Reply Gate delivers a Huddle post with no Mention to no Agent in a
  Room with several Agents.
- **It is history, not instruction.** It lands in the Transcript, so it reaches members in their next
  Catch-up, and the Human sees when the name changed. The notice (§6.2) is what tells an Agent what to do.
- The text is a Prompt, like the notice.

---

## 7. The confirmation ("Are you sure?")

The Human enters the new name in `NameDialog` (§5.5). **Rename** then opens a confirmation through the
shared confirm component (UX-R12), with the verb **Rename** and `Color.Error`, and **Cancel** first.

**Title:** Rename *Marketing* to *Growth*?

**Body:** a short list of what can go wrong, filled in with this Team's numbers. This is a deliberate
exception to [design-system.md](engineering/design-system.md)'s "one sentence of body says what is
lost": a rename loses several different things, and the Human asked to see them all.

> Huddle moves the Team's folder and updates its Rooms, Tasks, Requests and saved Views. Some things
> can go wrong or stay behind:
>
> - **5 members restart and start new conversations.** Each one keeps a summary of recent Messages but
>   loses its working context. Nova is in the middle of a Turn and loses that Turn's work.
> - **Agents are told on their next Turn, not now.** Until then an Agent may use the old name. Huddle
>   catches most of that, but an Agent that writes a file to the old folder brings it back as an empty
>   Team.
> - **Agents' own notes keep the old name** until each Agent updates them.
> - **Links outside Huddle break.** Bookmarks, scripts or other programs that use `Teams/Marketing` need
>   updating.
> - **This can't be undone in one step,** and *Marketing* stays reserved. Renaming back is another rename,
>   with the same effects.

- The mid-Turn sentence appears only when a member is mid-Turn, naming up to three. There is no wait
  option: Turns keep starting, so the Human chooses **Rename** or **Cancel** now.
- A Project rename has the same dialog with "No Agent restarts" in place of the first bullet.
- Update [wireframes.md](engineering/wireframes.md) with both dialogs, named per
  [UXDesignGuidelines.md](engineering/UXDesignGuidelines.md).

---

## 8. Verification and open questions

| Id | Question | How it is settled |
| --- | --- | --- |
| V-1 | Does a member's first Turn after the rename read the notice before acting on Catch-up? | A live run: rename, then post in a member's Room and check it uses the new name |
| V-2 | Do the Adapter's Write calls reach `WorkDirPermissionHandler`, so a guard can refuse a Write under the old folder? | A live run with a Write to an old path. If not, the dialog keeps its warning |
| V-3 | Does the Reply Gate deliver a Huddle post with no Mention to no Agent in a Team Room? | A test against `ChatService` and the Reply Gate, before building §6.4 |

---

## 9. Decisions

| # | Decision | Alternative it beat |
| --- | --- | --- |
| D-1 | The Name stays the identity; the rename rewrites every copy | A Team id: every store in §4 would need it, and the schema cannot gain columns |
| D-2 | Only the in-app rename is a rename; a hand rename keeps today's behaviour | Detecting a hand rename: a moved folder and a deleted-plus-new one look the same to the watchers |
| D-3 | Storage first, labels last and in one batch, with the reconciler suspended and the provisioner told | Labels first, or one at a time: the old folder and a second Team Room come back |
| D-4 | The rename rolls forward from a journal row, and recovers at startup | Rolling back every step: undoing a move can fail too, and a half-rolled-back rename shows as two Teams |
| D-5 | Agents are told by a Turn notice, once per session, sent to every non-fresh session | A rule for who is involved: costly to compute per Turn, and it misses Agents that only read the Team's files |
| D-6 | An old name is forwarded for reads and refused for writes, with a hint | Forwarding writes: a write meant for one Team must never land in another |
| D-7 | A journalled name is reserved | Letting a new Team take it: an old link or an Agent with old context would then reach the wrong Team |
| D-8 | `watches:` paths are resolved through the journal, not rewritten | Rewriting them: it changes a non-member's Persona text, which restarts that Agent |
| D-9 | The Team Room gets one Huddle line that wakes no one | Posting to every Room, or Mentioning members: every Agent would take a paid Turn just to be told |
| D-10 | The confirmation lists every consequence | One sentence, as the house rule says: the restart and the broken links are not obvious from "rename" |

---

## Appendix A. Task list

Test first: each task starts with a test that fails, per [agents/Testing.md](../agents/Testing.md).

1. `team_renames` table with `state`, and `ITeamRenames` (record, resolve a chain, reserve names).
2. `SqliteTeamDirectory.RenameTeamScopeAsync` and `RenameProjectScopeAsync`; `TaskIdAllocator` and
   `CadenceStateStore` renames.
3. `PersonaStore.UpdateMany` raising `PersonasChanged` once; the provisioner skipping a pending name. Test:
   three or more members, and `Teams/Marketing` is absent afterwards (R2).
4. `TeamRoomReconciler.Suspend()`: no Team Room created or archived while held, one reconcile on release (R3).
5. `TeamRenameService` with the §5.3 order, `ArchiveMove` for both folders, pausing every touched Team
   (R4, R5), failure before the move (R14 path in §5.4), case-only (R13).
6. `RecoverAsync` at startup for a `pending` row (R16).
7. `from_team:`, `ViewStore.RenameTeam` and `RenameProject`, workspace path, Library root id, doc-history,
   ledgers, attachment paths.
8. `TeamRenamedTurnHook`, its Prompt and golden (R6, R7); Project-level `AGENTS.md` path mapping.
9. Redirects: `memory` reads forwarded and writes refused (R9, R10), `file_request`, `search`, References,
   routes, the upload path map (R11), the Feed filter, `watches:`, process gates.
10. The Team Room line (R8), after V-3.
11. Project rename through the same service (R12).
12. `NameDialog` validation (R14), the confirmation (§7), the Details pencil and the More (⋮) items; the
    manual test page, `wireframes.md`, the ADR amending ADR-0055 OP-1, and the
    [known-limits.md](engineering/known-limits.md) entry for Team links.
