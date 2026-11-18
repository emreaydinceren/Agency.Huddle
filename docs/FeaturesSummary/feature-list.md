# Agency.Huddle feature list

What Huddle does **today**, on `main` at `987bf475` (PR #417, 2026-11-06). It is built
from the feature log (`private/feature-logs/Prs-Feature.md`, not in the public mirror), but each entry describes the
feature as it works now, after every later PR that changed it, not as it first shipped.

Each entry ends with **PRs**: the PR that added the feature first, then the ones that
changed it. Features that were replaced or removed are listed at the end, so an old PR
description is not read as current behaviour.

To bring this page up to date, follow `private/feature-logs/UPDATING.md`.

Sources: `docs/Huddle.UserGuide.md`, the status column of `docs/Index.md`, the PR
descriptions, and a check of `src/Huddle.App` where those disagreed.

## Contents

- [Install and run](#install-and-run)
- [The shell: Activity bar, sidebars, tabs and search](#the-shell-activity-bar-sidebars-tabs-and-search)
- [Agents](#agents)
- [Adapters, models and work modes](#adapters-models-and-work-modes)
- [Rooms and chat](#rooms-and-chat)
- [What Agents can do from a chat](#what-agents-can-do-from-a-chat)
- [Teams and Projects](#teams-and-projects)
- [Running a Team](#running-a-team)
- [Tasks](#tasks)
- [The Human gate, Needs you and Helpers](#the-human-gate-needs-you-and-helpers)
- [Scheduled Jobs](#scheduled-jobs)
- [Feed and notifications](#feed-and-notifications)
- [Library](#library)
- [Skills and Skill Packs](#skills-and-skill-packs)
- [Second Brain](#second-brain)
- [Memory and file changes](#memory-and-file-changes)
- [Appearance and Settings](#appearance-and-settings)
- [Remote access](#remote-access)
- [Developer tooling](#developer-tooling)
- [Replaced or removed](#replaced-or-removed)
- [Designed but not built](#designed-but-not-built)

## Install and run

**One-line installer.** A single PowerShell (Windows) or `curl | sh` (macOS) command
checks the machine, downloads the app into the user's own folders and opens it in the
browser. No admin rights are needed. Running it again upgrades, and keeps Rooms and
settings. The `huddle` command starts the app and `huddle stop` stops it; an uninstall flag
keeps the data unless a purge flag is added too. A release workflow builds the packages.
PRs: #128.

**Health endpoint and per-run logs.** The app answers `/health`, and each run can write
its own log file under `logs/`. A second copy of Huddle on the same machine stops at
start-up with a message naming `Team:PipeName`. PRs: #67, then #335.

## The shell: Activity bar, sidebars, tabs and search

**Activity bar.** A VS Code-style strip at the far left. Its buttons choose what the
sidebar shows: **Chats**, **Teams**, **Agents**, **Tasks**, **Library**, **Scheduled** and
**Remote access**, plus a **Feed** icon. **Archived** and **Settings** sit at the
bottom. Selecting the active button folds the sidebar away. PRs: #206, then #334.

**Team shortcuts.** Every Team gets an avatar tile near the top of the Activity bar, below
the Feed link, on by default. Clicking one narrows the sidebar to that Team's list of itself and its Projects, with the selected one's **ROOMS**,
**TASKS** and **LIBRARY** below. ROOMS splits into **Needs you** and **All** while a Room is
waiting, and its "+" starts a new chat. Right-click removes a shortcut or turns them
all off; the Team page has **Show shortcut** / **Hide shortcut** beside **Edit avatar…**. The choice is remembered per browser. PRs:
#277, then #278.

**Sidebar views.**

- **Chats** shows the **Needs you** card, then **Group by: All | Team**. **All** has a
  **Needs you** folder above an **All** folder; **Team** puts every chat in a folder per
  Team, in name order. Waiting chats wear a **Needs you** pill. A
  one-to-one Room's row shows the Agent's picture and title.
- **Teams** lists each Team as a root row. An open Team holds a **Rooms** folder (its Team
  Room first), a **Library** folder (its Projects) and a **Tasks** folder (every View,
  narrowed to that Team at `/teams/{Team}/tasks/{View}`). **Build a team** is at the
  bottom. Archived Teams and Projects are restored from the **Archived** page.
- The sidebar is resizable, 200 to 480 px, and remembers its width per browser.

PRs: #249, #251, #273, #276, #332, #335.

**Tabs and split panes.** Everything opens in a tab. One click opens an italic preview tab
that the next click replaces; a double-click, or typing on the page, keeps it. A tab can be
pinned, closed (×, middle-click, Alt+W) or moved into a second pane beside the first, so
two pages show at once. Tabs and panes come back after a reload or restart, per browser.
Half-written messages are kept per Room. PRs: #147, then #148, #251.

**Top Bar.** A fixed bar across the window shows the trail of the focused tab (each crumb
navigates), the Global Search box, the light/dark switch, the Notifications bell and the
Right sidebar toggle. The whole UI is drawn at 90% zoom. PRs: #150 (breadcrumb), then #162.

**Global Search.** `Ctrl+K`, or `/`, opens one box that finds Teams, Agents, Library
entries (names and contents up to 1 MB, and note aliases), Tasks (title, assignee, Action
and the first 4,000 characters of the description, title hits first) and Rooms (down to the
matching Message). Chips or prefixes (`team:`, `agent:`, `lib:`, `task:`, `room:`) narrow
it, and a search starts at 3 characters. It keeps no index, so every search reads fresh.
`Enter` opens a result in the current tab; `Ctrl+Enter` opens it in a new tab. PRs: #167,
then #279 (aliases), #408.

**Right sidebar.** A panel on the right edge that shows things about the current page. It
is open by default, its state is remembered, and it can be resized from 240 to 560 px.

| On | Panels |
| --- | --- |
| A chat Room | **Members** (with each Agent's context fill), **Tasks in this chat**, **Documents in this chat** |
| A Markdown note | **Outline**, **Linked documents**, **Backlinks**, **History**, **Tags** |
| A Task opened from a List or Board | **Task**, the read-only details with **Edit** |
| A Task's edit page | **Activity**, **Blocked by**, **Blocks**, **Linked documents** |
| Anything, from the bell | **Notifications** |

The toggle pulses once when a panel becomes available. PRs: #190, then #193, #234, #242,
#247, #255, #263, #264.

## Agents

**An Agent is a Markdown file.** `Agents/<Name>/<Name>.md`: front matter (`name`, `title`,
`alias`, optional `teams`, `skills`, `adapter`, `watches`) and private instructions below.
The `name` in the front matter is the identity, not the file name. Huddle watches the folder
and picks up a hand edit within half a second. A file that fails validation is listed under
**Files that didn't load**. Model, Effort and Work mode live in `team.db`, not the file. A
Name or Alias may not change to the Human's own, and names Huddle draws skip it. PRs: #3
(identity), #70 (Settings > Personas tab), #368.

**The Agents page.** Every Agent, grouped by Team with a Team filter. Each card shows a
presence badge (online, starting, working, error, offline) and has **Message**, **Edit**,
**Open**, **Restart** and **Remove**. **New agent** starts with a random unused first name
and a random avatar colour. PRs: #70, #135, #153.

**Rename, restart and remove.** A rename moves the folder, avatar, session pointers and
`team.db` row, rewrites every Task's creator or assignee and every View filter, and renames
auto-named Rooms. The transcript keeps the old name. **Restart** forgets every Room
conversation; the next turn catches up from the transcript (see **Room Sessions**).
**Remove** deletes only the definition file. PRs: #50, then #292.

**Avatars.** An avatar is chosen per Agent, stored in `avatars.json` and not part of the
Persona. The editor offers a stack of choice cards: **Initials of the name**, **A short
label** (up to six characters, with an emoji picker), **Image from the library**, and
**Upload an image** (PNG, JPEG or WebP, up to 512 KB). Initials and labels sit on a
background colour from a palette, with strong monogram colours ringed in their own shade.
The editor opens from the avatar itself. PRs: #68, then #137, #152, #269, #272, #275,
#280.

**Avatar library.** 126 ready-made pixel-art pictures ship with the app, generated by
`tools/avatar-pixel-art`. They can be filtered by skin tone and tag, and searched by tag words. A pick is copied, so the Agent keeps it if the
library moves. `Team:AvatarLibrary:Dir` can point at another library, or at nothing to turn
it off. PRs: #137, then #152.

**Profile card.** The same card is drawn for an Agent in the roster and in a popover from
any avatar. It shows the title, Adapter, Model and Effort. An Agent's title also sits
beside its name in message headers. The Agent's card lists **What this Agent reads**:
every part of its next session's prompt, where each comes from and where to change it. From
a Room header's avatar, the popover also offers **Start afresh**. PRs: #187, then #257,
#268, #270, #292, #335.

**The Human's profile.** **Settings > Profile** holds the Human's Name, Alias (a second name
that @mentions them), Title, description and avatar, drawn as the same card an Agent gets. A
blank system opens on this page until a profile is saved. Renaming yourself moves your
avatar and Tasks with you. PRs: #200.

**The Chief of Staff.** A built-in Agent that helps assemble a Team: it proposes Agents and
nothing is created until the Human approves. Its card has **Reset to default** instead of
**Remove**. It carries the `team-building`, `team-rollup`, `ingest`, `wrap-up` and
`brain-check` Skills and summarises digests and declined Requests.

It is also the front door for quick captures. A Team's fact goes to that Team's Memory
through the Helper, a dated item or an undecided idea becomes a Task, a Huddle-wide fact goes
to Huddle-wide Memory, a rule becomes a proposed `AGENTS.md` line, and anything it cannot
place gets one question. A new Chief of Staff starts on Opus at medium effort, and the Human can change that.
At start-up an unedited older file updates to the shipped text and keeps its name, alias,
Model, Effort and Work mode; an edited file stays as it is, with one Notification per shipped
version. PRs: #71, then #136, #377, #385, #406.

## Adapters, models and work modes

**Adapters.** A Persona names the Adapter it runs on (Claude by default; an Adapter is
fetched from nuget.org). An Adapter carries its own environment, and a Turn that goes
silent past a timeout fails visibly. Changing the Adapter resets Model, Effort and Work
mode, and the card says so. An Adapter's advisory arrives as a **Notice** row in the Turn's
activity, never in the Agent's reply, and a sign-in that expires during a Turn tells the
Human to sign in again. PRs: #59, then #60, #336, #338.

**Model and Effort.** Picked per Agent on its card. A change restarts the Agent. A role in
a team type names a tier (strongest, balanced or fast), and
`Team:Teams:Tiers:<adapter>:<tier>` maps it to a model. PRs: #59, #136.

**Work mode.** How much the agent may do before asking, from the modes its Adapter
advertises. For Claude these are **Agent default**, **Manual** and **Accept edits**. Plan,
Auto and Bypass permissions are hidden by `Team:Acp:HiddenModes`. On an Adapter that
isolates user settings, an Agent with no usable Work mode runs in Manual at every open and
resume, whatever `defaultMode` the user's own settings name, so Huddle answers the
permission requests. Writes into the agent's own `~/.claude` folder are always refused.
Phase 1 is built; phases 2 and 3 are only designed. PRs: #106, then #415.

**Adapter commands.** `@nova /compact` (mention first, then the command) runs a command the
Adapter advertises, here compacting the Agent's conversation. The Agent then posts one line,
such as "Compacted my conversation: 50,624 → 2,964 tokens". Only the Human can run one, and
only listed commands work. **Stop** cancels one. PRs: #103.

## Rooms and chat

**Rooms, Messages and the reply rule.** A Room holds the Human and one or more Agents; each
transcript is an append-only `rooms/<roomId>.jsonl`. In a one-to-one Room the Agent answers
everything. In a group Room an Agent answers only when mentioned, and `@all` wakes every
Agent in the Room. `/invite @alias` or the **Invite agent** button adds an Agent. PRs: #57
(following), #274 (`@all`).

**Following a Room.** An Agent can follow a Room and be woken without being mentioned.
PRs: #57.

**Budget.** After a set number of Agent replies without the Human speaking, a Room pauses
and asks whether to **Continue** or **Leave paused**. The Human's next Message resets it.
PRs: #5.

**A Room says why it stayed quiet.** When a Message wakes nobody, the Room says why instead
of showing nothing. PRs: #56.

**Silent replies.** An Agent with nothing to add replies `NO_REPLY`. A reply that is only
the token posts nothing, and one that ends with it loses the token. Every Agent is told this,
and told that a promise of later work needs a way back (a Task, a Job or a Mention), or the
work is done now. PRs: #371, then #375.

**Streaming, Stop and visible failures.** A reply streams as it is written, **Stop** cancels
a Turn, and a failed Turn says so in the Room. PRs: #8.

**Turn detail and Spend.** While an Agent works, its reply shows its last six tool calls as
rows (Waiting, Running, Failed or done), with the file and line. An edit row opens an **Edit
preview**. The card shows **Spent since start** per currency, and any Agent with stored
spend also shows **Total spent**. Tool-call rows are not saved. PRs: #99, then #136, #367.

**Room Sessions.** Each Agent keeps a separate conversation per Room, opened on demand,
closed after 30 minutes idle, at most 3 open per Agent, and resumed where the Adapter
supports it. Otherwise it catches up from the transcript, up to 20 Messages and an estimated
100,000 tokens, dropping the Agent's own older Messages first and keeping its newest 5.
**Settings > Sessions** edits both limits without a restart. PRs: #72, then #292.

**Start afresh.** From a Room header's avatar, the Human can give an Agent a new session in
that Room while it keeps every Message. It applies between Turns only, and is offered only
for an Adapter with one session per Room. The new session rereads with Catch-up, the old one
is deleted in the background, and the Room shows a note with **See what it reread**, which
opens the text as sent. PRs: #292, then #338.

**Context fill.** The **Members** panel shows how full each Agent's context window is in
this Room, as a percentage and a bar that turns amber at 75% and red at 90%. Its tooltip ends
with the Model that answered. The reading survives a restart. PRs: #247, then #256, #338.

**Conversation layout.** The Human's Messages sit on the right in an accent-tinted bubble;
an Agent's text sits beside its avatar with no card. Day separators group the Messages, and
a Task change that wakes an Agent shows as one slim line. PRs: #74, then #135, #198, #208.

**The composer.** It starts at 3 lines and grows to 18, with a **Send** button inside it.
PRs: #163, #173 (small changes; listed here for completeness).

**@ and # references.** `@` picks an Agent, Room, Team or Library file (`@room:` and `@lib:`
narrow it); `#` picks a Task. A sent reference keeps a snapshot, so it later shows a
**changed** or **(unavailable)** chip. Agents get a Referenced items header telling them
what each reference is. **Copy link** on a Room makes the same reference. PRs: #172.

**Attaching files.** The paperclip, paste or drag-and-drop saves each file into the Library
at once: the **Raw** root, per Room, or the Team's `raw/` folder for a Team chat. The file's
path goes into the Message. The limits are 25 MiB per file and 10 per Message. SVGs and
programs are refused, checked by content, not name. PRs: #170, then #335.

**Rename, archive and delete.** Rename with the pencil (not for a Team Room). Archive hides
a Room, which still works. Delete removes it and permanently deletes the transcript, after a
confirmation. PRs: #49, #65.

**Team chats and the Team Room.** A Room belongs to the root or to one Team. Every Team has
a Team Room holding the Human and all its members; it is created with the first member and
cannot be renamed. A member's **Message** button opens that Team's own one-to-one. When a
Team goes away, its chats are archived. PRs: #169.

## What Agents can do from a chat

**Questions (`ask_human`).** An Agent asks the Human a question with options, drawn as a
card in the Room; the answer comes back as a Message. PRs: #110.

**Elicitation.** ACP elicitation requests (AskUserQuestion, retry dialogs and MCP forms) are
bridged to the same kind of card as `ask_human`. PRs: #113.

**Image and PDF prompt blocks.** A Library image the Human names reaches the Agent as an
image block, not a path. A named PDF goes as an embedded document to an Adapter with no file
tools, up to 2 PDFs and 10 MiB per Turn, and otherwise stays a path line, so an Adapter with
file tools, such as Claude, reads the file itself. PRs: #111, then #338.

**The `search` tool.** Agents can run Global Search. By default it covers only the Rooms the
Agent is in or that belong to its Teams; `scope: "all"` searches every Room and is logged. It
matches the exact phrase, and a multi-word search with no hits says so and suggests one or two
words. Rooms are searched like grep: every matching Message, with `regex`, `context`, `count`
and filters on Room, Team, sender and time, and a Room's latest Messages on request. The Chief
of Staff searches every Room by default and reads any Team's records. PRs: #260, then #408,
then the #534 PR.

**Agent tools in general.** Agents work through `mcp__team__*` tools: Tasks
(`create_task`, `update_task`, `close_task` and others), `add_evidence`, `read_skill`,
`log_change`, Requests (`file_request`, `triage_request`, `confirm_request`,
`list_teams`), `propose_sprint`, `read_records`, `propose_retro_changes`,
`mark_retro_row`, `memory`, `propose_team_memory`, `jobs`, `get_help` and `search`. Each is
listed under its feature.

## Teams and Projects

**Teams.** A Team is a label in each member's `teams:` list plus a folder `Teams/<Team>/`,
optionally with `_team.md` (type, manager, workspace, purpose, avatar, gates, schedules). An
Agent can belong to many Teams. **Duplicate** copies a Team's roster into a new Team, hiring
each member afresh. Team **Spent** totals cost per currency. Teams cannot be renamed or
deleted in the app, but can be archived. PRs: #96, then #136, #335.

**Team avatar.** A Team has its own avatar, drawn as a rounded square so it never reads as
a person, and stored in `_team.md`. It shows in the sidebar, the Team page, the Feed, search
and the Team Room header. PRs: #275.

**Projects.** Any ordinary sub-folder of a Team folder is a Project (`memory`, `raw`,
`outputs` and names starting with `_` or `.` are excluded). A Project has no members of its
own. PRs: #96, then #335.

**Team and Project pages.** Tabs: **Members** (role cards and roster), **Files** (the
Library scoped to the Team), **Tasks** (List or Board, one swimlane per Project),
**Process** (for a Team with a `_team.md`) and **Details** (type, name, purpose, Library
folder, archive). A Project page has Files and Tasks only. PRs: #96, then #100, #101, #151,
#185, #335.

**Build a team.** A four-step wizard at `/teams/build`: **Team type**, **Team details**
(name, purpose, Library folder with a folder picker, empty unless the type requires one),
**Hire** and **Approve & hire**. In **Hire**, each role card's **Add** drafts an Agent with a
drawn name and avatar, which can be re-rolled, edited or removed. A draft takes its Skills
from the type's role, and one not yet downloaded is marked and can be unticked. Custom roles
can be written and are saved in `custom-roles.json`. Approving copies the Skills the roster
needs, creates the Team, writes its process (with a **Process** choice when a type offers
several), hires the drafts and makes the manager. The Team page's **Members** tab reuses the
Hire step, with **Remove from team**, **Delete agent** and **Save changes**.

Hiring a typed Team starts a paid Turn: Huddle posts the onboarding steps in the Human's
one-to-one Room with the manager, who proposes a starter Task in its reply and creates it only
after the Human says yes. PRs: #185, then #201, #202, #339, #374.

**Team types and the hiring pool.** 26 built-in team types, with **Development Team** and
**Household** first, plus the user's own in `TeamTypes/`. A single hiring pool holds every
job, filtered by two tag clouds. **Settings > Team types** lists them and their
**Problems**. Hire limits: 8 Agents per Team and 16 live processes by default. PRs: #136,
then #154.

**Household team.** The `household` type runs a family's whole home. The Household Manager
is the only required role; School Liaison, Meal Planner, Home Keeper, Health Admin, Schedule
Keeper, Finance Tracker, Comparison Shopper and Retro Agent are optional. Its
household-basics process gives every job one owner, opens one Task per occurrence of a
recurring chore, gates every spend or signature behind the Human, and runs a weekly check-in
and a 28-day retro. A role whose connector (such as `email:read`) is not listed in
`connectors.json` shows **Not connected yet**, which never blocks the hire. PRs: #136, then #281, then #545.

## Running a Team

**Process and gates.** A Team's process is its `AGENTS.md` prose (steps, onboarding,
Definition of Done) plus settings in `_team.md`: gates, `checkCommand`, WIP limit and digest
and retro cadence. Build a team writes them, or the manager's card does, from the type's
template or blank, and neither overwrites what is there. Six gates exist:
`accept-before-merge`, `qa-evidence-before-done`, `checks-before-merge` (refuses an Agent's
`git merge` or `git push` until the check command's Evidence passes),
`spec-approved-before-build`, `failing-test-before-fix` and `blockers-done-before-start`
(refuses a move to In Progress, Needs You or Done while a blocker is not Done,
Cancelled, Duplicate or Rejected; the development processes have it, ADR-0085). In a Team without
`accept-before-merge`, the assignee of an open gated Task with no valid Accept is refused a
`git merge` of anything but `main`, and a `git push` to `main`. A generated hook makes the Claude Adapter ask before any `git merge`
or `git push`, even in its `auto` mode, so the gate is consulted. The **Process** tab shows
`AGENTS.md`, the Team's Needs You level and a switch per gate that is on (not `accept-before-merge`). Turning one off asks first, and only the Human
can override a gate, which is logged. PRs: #136, then #335, #397, #398.

**Accept before merge.** In every development process, a merge or push into `main` by any
Agent of the Team is refused until an open Task of that Team has the Human's Accept; merging
`main` into a feature branch still runs. The Team's process tells the Agent to name the merge as the
Task's Action, and a refused Agent sees only a plain denial. An Accept counts for 24 hours, or one hour when it took the Task straight to
Done, and another merge by the Team also passes in that time. It guards against an Agent's
ordinary mistake and is an early warning, not enforcement or a security boundary. PRs: #402.

**Needs You level.** A level per scope (Settings, a Team, a project; the most specific wins)
says how much waits for the Human. Under Ask me (the default) nothing changes. Under Run inside
the Team a local merge into `main` of a branch named after the Task's id, a Sprint proposal and
new Backlog Tasks go ahead without an Accept, each recorded on the Task and in the Feed; every
push Huddle sees in an Agent's command, an Action and a connector call still wait. The gate reads the
command line, so a script, a git alias saved earlier, a Task created straight into a project set to Run inside the
Team and the 24-hour push window get past it; the level control says so in a "What this cannot catch" caption, and
branch protection on the git server is the guarantee (issue #608 is the parked design for enforcement). The level is kept in `team.db` by scope id, and
a Task's level is fixed where Huddle placed it and can only get stricter. An optional spend cap per scope falls back to Ask me and asks Yes or No for one more cap.
The level replaces the Process tab's `accept-before-merge` switch and its start-up migration,
and only the Human can set it or the cap. PRs: pending (#533).

**Evidence and the tool-call log.** Agents append Evidence lines to a Task with
`add_evidence`. A line holds a command with its exit code, a note, or both, and a command with
no exit code is refused. Every tool call is logged to
`logs/tool-calls/<Agent>/<date>.jsonl`, so an Evidence line can point at the exact call. PRs:
#136, then #340, #379.

**Digests, retros and manager wake-ups.** Digests run on time and retros run after N Done
Tasks or N days, posted in the Team Room. The manager is woken about Task changes, and the
assignee of a blocked Task is woken once when its last blocker finishes (Done, Cancelled, Duplicate or Rejected). PRs: #136.

**Requests between Teams.** One Team files a bug, feature, feedback or question into
another's Backlog as a Task marked **Needs triage**. The receiving manager accepts it,
declines it with a reason, or asks for more information. The Request closes only when the
requesting Team's verifier confirms delivery. Reaching Done wakes the requesting manager with
a line naming who can confirm, and a refused close or confirm says what is needed. A
**Requests** filter and a chip mark them. PRs: #136, then #382.

**Retrospectives.** A Retro agent reads the Team's own records (`read_records`) and proposes
up to three exact-text changes to `AGENTS.md`, `_team.md` or a member file
(`propose_retro_changes`). The Human accepts or declines each from a card in the Room. Accepted
changes go into `Retro register.md`, and the next retro verifies them. PRs: #136, then #335.

**Sprint proposals.** A manager can `propose_sprint`; **Approve** creates the Sprint and
adds the Tasks. PRs: #136.

## Tasks

**Tasks are Markdown files.** `Teams/<Team>/[<Project>/]_tasks/<ID>.md`, with IDs such as
`BUSI-0001` (prefix from the Team, numbers never reused). There are eight statuses: Backlog,
To Do, In Progress, Needs you, Done, Cancelled, Duplicate and Rejected. Priority,
assignee, parent, blockers, duplicate-of, tags, start and due dates, a description and an
append-only Timeline of events, comments and Evidence (with day and time). Closing is separate from status and moves the
file to `_closed/`; there is no delete. Hand edits are noticed and logged, and two people
editing the same Task get a per-field merge. PRs: #79, then #245.

**Wake on change.** A change to an Agent's Task wakes it with a Message in the Task's origin
Room, or a fitting Room. Changes within 5 seconds are combined. A review Task is assigned to the reviewer, so the ordinary
wake reaches them (ADR-0096). Each Task allows 10 Agent-made wake-ups before the Human must **Allow 10 more**. A
hand-off refused by a spent Task budget or a paused Room waits: one Notification names who is
waiting, and **Allow 10 more** or the Room's **Continue** delivers it. After a restart the
Notification stays, with **Wake {Agent}** on its card. PRs: #79, then #340, #372, #412.

**Views, List and Board.** **All Tasks**, **My Tasks** and any saved View. A View can be a
List or a Kanban Board. The toolbar has a **Filter** menu (Team, Project, Assignee, Sprint,
Status, Priority, Blocked) whose set filters show as chips, a **Group by** chip, search and
**Dense**. Every change is saved to the View as it is made; there is no Save button and no
View editor panel. **Fields…** reorders columns, and List columns can be resized. Dates read
naturally ("Today", "Fri") and overdue dates are bold with a warning icon. PRs: #79, then
#81, #94, #108, #177, #181, #216, #271.

**Reading and editing a Task.** Clicking a row or card shows the Task, read-only, in the
Right sidebar's **Task** panel. **Edit** opens the Task in its own tab ("Edit BUSI-0001")
with sticky **Save** and **Cancel**, a dot for unsaved edits and a Markdown toolbar on the
description. That edit page is the only Task editor: **New task** and **Make a copy** open
it too. Closing the tab with unsaved edits asks first. PRs: #231, then #232, #246, #264.

**Task attachments.** Link a Library file into a Task, or upload one to the Task's own
folder under `raw/Tasks/`. PRs: #221, then #335.

**Sprints.** A Sprint is only a name in `sprints.md`, with no dates or state. It is created
by typing a new name on a Task and disappears with its last Task. Views filter, group and
sort by Sprint, with `@current` and `@none`. PRs: #132.

## The Human gate, Needs you and Helpers

**Needs you.** The sidebar card and the **Needs you** page list Rooms waiting on the Human
(an unanswered mention, an open Question or Form, a spent Budget, a Job for the Human) and
gated Tasks waiting for a decision. A Room leaves the list when the Human posts or presses ✓.
The Task rows survive restarts; the Room rows only partly do. PRs: #166, then #230, #334.

**The Human gate.** A Task can require the Human's acceptance. It must if it names an
**Action** that leaves the Team, such as sending, paying, signing, publishing or merging. Its
**Proof** checklist comes from the Team's **Definition of Done** in `AGENTS.md`, using four
rule kinds. When Evidence or an update completes the Proof, Huddle moves a gated Task from
In Progress to **Needs you** by itself and wakes nobody; setting an Action on a ready Task does the
same, unless the Human set it. The assignee may move the Task too, and must when the Proof
completes with no Task write. The Human **Accepts**, **Requests changes** or **Declines** from
a decision card. Only the Human can make a gated Task Done. Dragging a gated card to Done opens
the decision instead. PRs: #230, then #250, #335, #379, #402.

**Helpers.** Small background agents that make one stateless judgement, with no tools, Room
or memory, and let Huddle apply the verdict. There are three: **Needs Human**, **Team Memory**
and **AGENTS.md Changes**. **Needs Human** reads an Agent-written Task and turns the Human gate
on when the work leaves the Team, recording the step as the Task's Action if the Task states
one and none is set. It only adds caution, never judges the Human's own Tasks, and never looks
again at a Task where the Human turned the gate off. Each Helper has a daily call cap with
**Continue (+10 today)**. **Settings > Helpers** sets its on/off, Adapter, Model, Effort and
cap. PRs: #240, then #287, #376, #400.

## Scheduled Jobs

**Jobs.** An instruction that runs on a timetable for the Human or one Agent, in its own Room
named ⏰ and its title. It can run once, daily, on weekdays, on chosen days, monthly on a day
(a day the month lacks runs on its last day) or every few hours. **Scheduled** in the Activity
bar lists every Job, and **New job** previews the next runs. A Job's page has **Edit…**,
**Pause**, **Run now**, **Open Room** and **Delete job…**, which keeps the Room. An Agent's Job
Mentions it, so it takes a Turn; the Human's puts the Room on **Needs you** and raises a
Notification. With the `jobs` tool an Agent manages its own Jobs, a manager can also schedule
for its Team's members, and only the Chief of Staff can schedule for any Agent or the Human.
Jobs live in the owner's `schedule.md`, editable by hand, and a line Huddle cannot read is
listed on the page. A run more than two minutes late is skipped. `Team:Jobs:Enabled` turns it
all off. PRs: #334, then #369.

## Feed and notifications

**Feed.** One stored history of what Agents and Huddle did: Tasks created, Rooms started,
documents changed, asks raised and resolved. It never shows Message text and never changes
the work. The **Notifications** bell in the Top Bar counts items that need the Human and
opens the **Notifications** panel, with categories (Needs you, All, Tasks, Documents, Rooms,
Gates, Helpers). The **Feed** tab shows cards by day, with Team and Agent filters,
expandable previews and a live "↑ n new items" pill. Items are kept 30 days.
`Team:Feed:Enabled` turns it off. PRs: #242.

**Helpers in the Feed.** A Helper's gate change appears as a Notification with its reason. A
Helper that pauses at its cap or keeps failing is reported by **Huddle** in the Helpers
category. A Helper's wake-up Message is posted by Huddle, not by the Human. PRs: #244.

## Library

**Library.** A file browser and Markdown editor over the data folders. The built-in roots are
**Teams**, **Agents**, **Raw**, **Archive** and **Skill Sources** (the last two read-only) and
**Prompts**, plus any pinned folders (**Settings > Library**). Markdown opens in **Read**,
**Edit** or **Split**, saved with **Save** or `Ctrl+S`; images preview; a PDF opens as a card
with **Open in default app**; other text is read-only. Saves are atomic and keep encoding and
line endings, with a conflict prompt when the file changed on disk. Delete goes to the Recycle
Bin. Protected folders (roots, Teams, Projects, Agent folders) cannot be moved. PRs: #85, then
#87, #88, #92, #95, #335, #338, #340.

**Explorer and organising.** A tree with the search, **New note** and **New folder** in it.
Drag-and-drop or **Move…** relocates files and rewrites every `[[link]]` pointing at them.
The context menu opens on right-click. PRs: #85, then #164, #189.

**Links, Backlinks and the Outline.** `[[Note]]`, `[[folder/Note]]`, `[[Note|text]]` and
`[[Note#Heading]]` resolve within a root, Obsidian-style; a missing target offers to create
it. The Right sidebar shows the **Outline**, **Linked documents** and **Backlinks**, which
lists notes and also open Tasks that link to the note. PRs: #85, then #255, #258.

**Markdown toolbar and `[[` suggestions.** The editor has formatting buttons (bold, italic,
heading, code, lists, task list) with shortcuts, and suggests notes as you type `[[`. PRs:
#217.

**Reading styles and the document header.** Notes read at a comfortable measure with a
document header: the note's tags, then its first heading, then the history byline. PRs:
#192, #266 (small changes; listed here for completeness).

**Front matter properties.** A note's front matter is drawn as a typed **Properties** block:
text, list, number, checkbox, date, date and time, aliases, tags, unsupported and mismatch.
Types come from the root's `.obsidian/types.json`, then defaults, then inference. `[[links]]`
and URLs in properties are links and count as backlinks. Aliases are searchable, JSON front
matter is read too, and nothing is written back. PRs: #279.

**Tags.** Tags are read like Obsidian: inline `#tag` plus front matter `tags`, merged,
case-insensitive and nested on `/`. The **Tags** panel shows the note's tags and a tag tree
with counts. Huddle only reads tags. PRs: #263, then #266.

**Document history.** Who created, edited, reviewed or objected to a Markdown document.
Agents record entries with `log_change`, held to it by a Claude Code Stop hook on creates and
edits; only an Agent whose role verifies is reminded about reads. The Human's saves, creates,
renames and moves log themselves. It shows as a byline under the heading and a **History**
timeline in the Right sidebar. Renames carry the history with them. PRs: #220, then #255, #261,
#371.

## Skills and Skill Packs

**Skills.** A Skill is a folder of Markdown instructions (`Skills/<name>/SKILL.md`) that an
Agent reads on demand with `read_skill`. It is assigned on the Agent's card. Nine ship built
in: `team-building`, `team-rollup`, `team-management`, `request-confirmation`, `retrospective`, `ingest`, `wrap-up`,
`brain-check` and `browser-check`. A built-in Skill can be overridden file by file and restored. PRs: #71, then
#136, #335.

**Skill Packs fetched by your AI tool.** Huddle never downloads Skills itself. The user's AI
tool (Claude Code, Codex) fetches them and adds pointers to `SKILLS.md` in the Skill Sources
folder; Huddle checks every file and shows the state (Ready, Not downloaded, Update
available, Needs setup, Broken). **Settings > Skills** has three tabs: **Skills**, **Packs**
(named sets of Skills for roles, added to an Agent with one chip) and **Sources**. Each Skill
records its provenance in `.source.json`. **Copy prompt** hands the AI tool what it needs and
asks for a byte-for-byte fetch. A catalog entry can hold a `sha256` of the Skill's folder, and
a mismatch makes the Skill Broken with the reason; the shipped catalog holds no hashes yet, so
nothing is compared today. PRs: #215, then #407.

## Second Brain

**Folders for what came in, what lasts and what was made.** Every Team and Project has `raw/`
(originals, kept as they arrived), `memory/` (facts) and `outputs/` (documents worth reading
again), beside `_tasks/`. Attachments land in `raw/`, and a guard refuses and logs any Agent
change to an original, though an Agent may add a derived file beside it. Each session carries
an index of Memory and of each output's "Read when…" line, and every Agent is taught the
pattern in a Second Brain summary. PRs: #335.

**Rules in `AGENTS.md`.** Plain Markdown at three levels: `Teams/AGENTS.md`,
`Teams/<Team>/AGENTS.md` and `Teams/<Team>/<Project>/AGENTS.md`. An Agent reads every level
that applies, in full, each session, and the most specific wins a conflict. Only the Human or
an accepted retro change writes these files. A Team made from a type starts with one written
from that type. PRs: #335.

**Running Agents hear of an `AGENTS.md` edit.** At its next Turn in each Room, an Agent gets a
note of the rules added, changed or removed, and nobody restarts. The **AGENTS.md Changes**
Helper writes the summary once per edit; if it is off, paused or takes over 30 seconds, the
Agent gets the changed lines instead. A small edit it judges not meaningful reaches no Agent
and is listed in the Feed. PRs: #376.

**Archive.** A Team is archived from its row's menu or its **Details** tab, a Project from its row's menu or its page, and the whole
folder moves unchanged into the read-only **Archive** root. It leaves every prompt, its Rooms
are archived and its Tasks leave the Boards; Huddle first says how many open Tasks it would
hide. Old links open the file with an **Archived** chip. The **Archived** page (Teams, Projects and Chats tabs) lists what is archived, and **Restore** brings back members,
manager and Rooms. PRs: #335.

**Workspaces.** A Team's readable workspace, such as a code repository, is routed into its
members' prompts. The real work follows the workspace's own conventions and `outputs/` holds
the rest. Huddle does not restrict what Agents write there. PRs: #335.

**Upkeep.** The `ingest` Skill proposes facts, Tasks and documents from new originals, `wrap-up`
proposes what a session produced, and `brain-check` finds drift and files it as Tasks. The Chief
of Staff offers upkeep Jobs and creates each only on a yes. A Memory folder over its limit raises
a daily Feed notice, and the manager moves old facts into `memory/archive/`. PRs: #335.

## Memory and file changes

**Personal and Team Memory.** Plain Markdown, one topic per file, with a `description` saying
when to read it (an older file's first line stands in): `Agents/<Name>/work/memory/`,
`Teams/<Team>/memory/` and per-Project `memory/`. Each session's system prompt includes an index
of the Agent's own memory and its Teams' memory, one line per file; there is no `MEMORY.md` to
keep. PRs: #72, #96, then #287.

**The `memory` tool.** Agents list, read, write, append to, replace text in and delete memory
files. It requires a description, refuses a write over a version the Agent has not read
(showing the current text to merge into) and caps a file at 16 KB. A delete goes to the Recycle
Bin, and only when the Human asks. PRs: #287.

**Team Memory Helper.** The Team's manager and the Chief of Staff write Team Memory directly;
other members propose a fact with `propose_team_memory`. The Helper (Haiku, low effort, 30 calls
a day) decides whether to file it, and in which file, and Huddle writes it. The file's **History**
shows who proposed it and why. A proposal it does not file raises a Notification with the reason;
the proposer is not told. PRs: #287, then #377, #385, #413.

**Huddle-wide Memory.** `Teams/memory/` holds facts every Agent reads, and only the Chief of Staff
writes it. A new, changed or deleted file reaches each running Agent at its next Turn in each Room,
as the file's name and description, with no Helper and no Start afresh. PRs: #335, then #377, #380.

**File Changes.** At the start of each turn an Agent is told which files changed in its
watched folders since its last turn in that Room. The watched folders are its `work/`, its
Teams' folders and anything listed under `watches`. An `AGENTS.md` edit reaches it through its
own note instead. PRs: #72, then #376.

## Appearance and Settings

**Themes.** Four built-in Theme sets, each in light and dark: **Lumen** (the default),
**Fluent**, **Nordic** and **Cobalt Soft**, aligned with the HuddleUX mockups. A Theme is a
JSON skin (palette, `--hu-*` tokens and structural traits), so adding one means editing data.
The user picks one light and one dark Theme on **Settings > Appearance**, plus an accent
colour, and the Top Bar button switches between them. PRs: #9 (dark mode), then #58, #64,
#74, #81, #171, #212, #213, #284.

**Prompts.** Every piece of text sent to a model is configuration: defaults live in code,
each override is one file in the **Prompts** root (deleting it restores the default), and
**Settings > Prompts** edits them, picked up without a restart. PRs: #6, then #69, #335.

**Spending.** **Settings > Spending** is a read-only view of what the Adapters reported: a line
per Team, the Chief of Staff, each Agent in no Team and the Helpers, then a **Total** that counts
every Agent once. Currencies are never added together. It sets no budgets or limits. PRs: #367.

**Settings layout.** Sections in a left menu: **Profile**, **Prompts**, **Helpers**, **Spending**,
**Appearance**, **Personas**, **Skills**, **Team types**, **Library**, **Sessions** and **Remote
access**. PRs: #70, #204, #292, #367.

## Remote access

**Strict local by default.** Out of the box only the computer running Huddle can use it,
with no sign-in.

**With Strict local off**, Huddle follows OpenClaw's gateway model:

- a gateway token on every connection and an Ed25519 identity per browser;
- device pairing with approval and scopes (**Limited** or **Admin**), with **Request admin**;
- Tailscale Serve (recommended, encrypted, no token prompt) and Funnel;
- a LAN bind, labelled downgraded because it is unencrypted, with a QR setup link;
- trusted proxies and rate limits;
- a **Devices** list with Rotate, Revoke and Remove, and a **Security check**;
- `huddle auth-token` and `huddle devices` commands for when you are locked out.

Every 60 seconds a housekeeping worker deletes loopback- and bootstrap-paired devices unseen for
30 days and clears spent rate-limit entries, expired sessions and expired pairing requests.

PRs: #283, then #310.

## Developer tooling

**The seeder (`huddle-seed`).** A console app that builds a complete, repeatable test data
set. Its scenarios include `software-co` and `uat`, with Team Rooms, references, uploads,
Sprints, Requests, Task attachments, document history, tagged notes and front matter
examples. PRs: #98, then #196, #226, #265, #279, #282.

**Demo agents off by default.** The built-in demo agents no longer start unless asked.
PRs: #133.

## Replaced or removed

Behaviour that an earlier PR description promises but that no longer exists:

| Was | Now | Changed by |
| --- | --- | --- |
| The Visual Studio Code Theme catalog, "Soft Huddle" Themes and the Custom Accent theme pair | The four skin-based Theme sets above; old saved choices map to Lumen | #58, #74, #81 → #212, #213 |
| Nordic as a CSS "style pack" | Skin tokens and traits (ADR-0058 supersedes ADR-0057) | #212 → #213 |
| Settings > Hooks | Settings > Prompts | #6 → #69 |
| The docked Library pane | The Library is a tab like any other | #85 → #147 |
| A breadcrumb bar above each tab | One Top Bar for the window | #150 → #162 |
| The Task detail drawer (`T7`) and an in-drawer editor | The Right sidebar's **Task** panel, and the edit page in its own tab as the only editor | #231 → #232, #233, #264 |
| The View editor panel and its Save button | Filter chips, auto-saved Views, and Rename and Fields dialogs | #79, #94 → #177 |
| The in-page "Linked from N notes" block and the History under a note | The **Backlinks** and **History** panels in the Right sidebar | #85, #220 → #255 |
| Teams listed under a "Teams" group header, with chats and Projects mixed together | Teams as root rows with Rooms, Library and Tasks folders | #96 → #273, #276 |
| Teams' Library folder listing the Projects, and Rooms and Task Views that were Team-wide only | A selectable list of the Team and its Projects, each with its open Task count, and the Rooms, Tasks and Library link of the selected one below it; a Room may belong to one Project (ADR-0082) | ADR-0082 |
| A Chief of Staff-only team build | **Build a team** wizard, with the Chief of Staff as one route | #71 → #185 |
| Three radio buttons in the avatar editor | Choice cards | #68 → #272 |
| The MudToggleGroup | The animated segmented control | #93 |
| "Teammate" and "Team member" as names for an Agent | "Agent" everywhere | #267 |
| A Team's `Process.md` | Prose in `AGENTS.md`, settings in `_team.md`; an old file is no longer read and gets a start-up warning | #136 → #335 |
| `Instructions.md` files at Huddle, Team and Agent level | `AGENTS.md` at Huddle, Team and Project level | #287 → #335 |
| `prompts.json` | One file per changed prompt in the **Prompts** root; an old file is not migrated | #6, #69 → #335 |
| Team chat uploads in the Team's `Uploads/` folder | The Team's `raw/` folder; a root Room's files go to the **Raw** root | #170 → #335 |

## Designed but not built

These are specified in `docs/` but not on `main`, so no PR above delivers them:

- **More Adapters:** Codex, Antigravity and Copilot over ACP, with Gemini CLI as a
  secondary preset. Designed 2026-10-19.
- **Work mode phases 2 and 3.**
- **The rest of Long-running Rooms:** session retention, phase 2b, the single-session mode and
  phase 4. Closed 2026-10-31 and not pursued.
- **Adapter-side compaction:** advertising `session.compaction` from `claude-agent-acp`. Closed
  without being built, because a paid spike showed it removes the token figures `/compact` reports.
