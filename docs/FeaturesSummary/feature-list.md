# Agency.Huddle feature list

What Huddle does **today**, on `main` at `0e4f72e1` (PR #684, 2026-11-18). It is built
from the feature log (`private/feature-logs/Prs-Feature.md`, not in the public mirror), but each entry describes the
feature as it works now, after every later PR that changed it, not as it first shipped.

Each entry ends with **PRs**: the PR that added the feature first, then the ones that
changed it. Features that were replaced or removed are listed at the end, so an old PR
description is not read as current behaviour.

To bring this page up to date, follow `docs/FeaturesSummary/UPDATING.md`.

Sources: `docs/Huddle.UserGuide.md`, the status column of `docs/Index.md`, the PR
descriptions, and a check of `src/Huddle.App` where those disagreed.

## Contents

- [1. Install and run](#1-install-and-run)
- [2. The shell: Activity bar, sidebars, tabs and search](#2-the-shell-activity-bar-sidebars-tabs-and-search)
- [3. Agents](#3-agents)
- [4. Adapters, models and work modes](#4-adapters-models-and-work-modes)
- [5. Rooms and chat](#5-rooms-and-chat)
- [6. What Agents can do from a chat](#6-what-agents-can-do-from-a-chat)
- [7. Teams and Projects](#7-teams-and-projects)
- [8. Running a Team](#8-running-a-team)
- [9. Tasks](#9-tasks)
- [10. The Human gate, Needs you and Helpers](#10-the-human-gate-needs-you-and-helpers)
- [11. Scheduled Jobs](#11-scheduled-jobs)
- [12. Feed and notifications](#12-feed-and-notifications)
- [13. Library](#13-library)
- [14. Skills and Skill Packs](#14-skills-and-skill-packs)
- [15. Second Brain](#15-second-brain)
- [16. Memory and file changes](#16-memory-and-file-changes)
- [17. Appearance and Settings](#17-appearance-and-settings)
- [18. Remote access](#18-remote-access)
- [19. Developer tooling](#19-developer-tooling)
- [20. Replaced or removed](#20-replaced-or-removed)
- [21. Designed but not built](#21-designed-but-not-built)

## 1. Install and run

**1.1 One-line installer.** A single PowerShell (Windows) or `curl | sh` (macOS) command
checks the machine, downloads the app into the user's own folders and opens it in the
browser. No admin rights are needed. Running it again upgrades, and keeps Rooms and
settings. The `huddle` command starts the app and `huddle stop` stops it; an uninstall flag
keeps the data unless a purge flag is added too. A release workflow builds the packages, and
a release version only ever goes up. Windows gets a native build for x64 and for ARM64 PCs;
macOS is Apple Silicon only, and an Intel Mac fails the installer's machine check. A smoke
test installs the build for the PC it runs on and fails if the installed build is another.
PRs: #128, then #641, #659.

**1.2 Health endpoint and per-run logs.** The app answers `/health`, and each run can write
its own log file under `logs/`. The console shows only errors by default; `--log-level LEVEL`
(`Trace` to `None`) raises Huddle's own categories for one run. A second
copy of Huddle on the same machine stops at start-up with a message naming `Team:PipeName`.
PRs: #67, then #335, #510.

## 2. The shell: Activity bar, sidebars, tabs and search

**2.1 Activity bar.** A VS Code-style strip at the far left. The **Feed** icon is always first.
Below it, its buttons choose what the sidebar shows: **Rooms**, **Teams**, **Agents**,
**Tasks**, **Library** and **Scheduled**. **Archived** and **Settings** sit at the bottom;
Remote access is a tab in **Settings**. Only the last tile clicked is lit. Selecting the
active button folds the sidebar away. PRs: #206, then #334, #517, #531.

**2.2 Team shortcuts.** Every Team gets an avatar tile in the Activity bar, between the Feed
link and the view buttons, on by default. Clicking one shows that Team in the sidebar: a
selectable list of itself and its Projects, with the selected one's **ROOMS**, **TASKS** and
**LIBRARY** below. ROOMS splits into **Needs you** and **All** while a Room is waiting, and
its "+" starts a new Room. Right-click removes a shortcut or turns them all off; the Team
page has **Show shortcut** / **Hide shortcut** beside **Edit avatar...**. The choice is
remembered per browser. PRs: #277, then #278, #517.

**2.3 Sidebar views.**

- **Rooms** shows the **Needs you** card, then **Group by: All | Team**. **All** has a
  **Needs you** folder above an **All** folder; **Team** puts every Room in a folder per
  Team, in name order. Waiting Rooms wear a **Needs you** pill. A
  one-to-one Room's row shows the Agent's picture and title.
- **Teams** shows one Team at a time, the one whose page is open. A card at the top names it,
  and **Switch team** lists every Team and **Build a team**. **PROJECTS** lists its Projects,
  each with a square avatar and its open Tasks, plus a **No project** row once a Project
  exists. **ROOMS**, **TASKS** (every View, narrowed to the scope at `/teams/{Team}/tasks/{View}`)
  and **LIBRARY** follow for the Team, the selected Project or **No project**. Archived Teams
  and Projects are restored from the **Archived** page.
- **Tasks** lists **All Tasks**, **My Tasks**, the saved Views and **New view** at the top level.
- **Library** holds the tree of every root, with the search box, **New note** and **New
  folder**. A file opens in a tab that shows only the document.
- The sidebar is resizable, 200 to 480 px, and remembers its width per browser.

PRs: #249, #251, #273, #276, #332, #335, #508, #516, #522, #531.

**2.4 Tabs and split panes.** Everything opens in a tab. One click opens an italic preview tab
that the next click replaces; a double-click, or typing on the page, keeps it. A tab can be
pinned, closed (×, middle-click, Alt+W) or moved into a second pane beside the first, so
two pages show at once. Tabs and panes come back after a reload or restart, per browser.
Half-written messages are kept per Room. PRs: #147, then #148, #251.

**2.5 Top Bar.** A fixed bar across the window shows the trail of the focused tab (each crumb
navigates), the Global Search box, the light/dark switch, the Notifications bell and the
Right sidebar toggle. The whole UI is drawn at 90% zoom. PRs: #150 (breadcrumb), then #162.

**2.6 Global Search.** `Ctrl+K`, or `/`, opens one box that finds Teams, Agents, Library
entries (names and contents up to 1 MB, and note aliases), Tasks (title, assignee, Action
and the first 4,000 characters of the description, title hits first) and Rooms (down to the
matching Message). Chips or prefixes (`team:`, `agent:`, `lib:`, `task:`, `room:`) narrow
it, and a search starts at 3 characters. It keeps no index, so every search reads fresh.
`Enter` opens a result in the current tab; `Ctrl+Enter` opens it in a new tab. PRs: #167,
then #279 (aliases), #408.

**2.7 Right sidebar.** A panel on the right edge that shows things about the current page. It
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

## 3. Agents

**3.1 An Agent is a Markdown file.** `Agents/<Name>/<Name>.md`: front matter (`name`, `title`,
`alias`, optional `teams`, `skills`, `adapter`, `watches`) and private instructions below.
The `name` in the front matter is the identity, not the file name. Huddle watches the folder
and picks up a hand edit within half a second. A file that fails validation is listed under
**Files that didn't load**. Model, Effort and Work mode live in `team.db`, not the file. A
Name or Alias may not change to the Human's own, and names Huddle draws skip it. PRs: #3
(identity), #70 (Settings > Personas tab), #368.

**3.2 The Agent card.** There is no Agents page; an Agent is opened from its Team's Members tab or from search. The card shows a
presence badge (online, starting, working, error, offline) and has **Message**, **Edit**,
**Open**, **Restart** and **Remove**. **New agent** starts with a random unused first name
and a random avatar colour. PRs: #70, #135, #153.

**3.3 Rename, restart and remove.** A rename moves the folder, avatar, session pointers and
`team.db` row, rewrites every Task's creator or assignee and every View filter, and renames
auto-named Rooms. The transcript keeps the old name. **Restart** forgets every Room
conversation; the next turn catches up from the transcript (see **Room Sessions**).
**Remove** deletes only the definition file. PRs: #50, then #292.

**3.4 Avatars.** An avatar is chosen per Agent, stored in `avatars.json` and not part of the
Persona. The editor offers a stack of choice cards: **Initials of the name**, **A short
label** (up to six characters, with an emoji picker), **Image from the library**, and
**Upload an image** (PNG, JPEG or WebP, up to 512 KB). Initials and labels sit on a
background colour from a palette, with strong monogram colours ringed in their own shade.
A picture sits on a neutral fill, and cards and chat draw avatars at 50 px. The editor opens
from the avatar itself. PRs: #68, then #137, #152, #269, #272, #275, #280, #515.

**3.5 Avatar library.** 126 ready-made pixel-art pictures ship with the app, generated by
`tools/avatar-pixel-art`. They can be filtered by skin tone and tag (hair, glasses, top
colour), and searched by tag words, and none carries a job title. A pick is copied, so the
Agent keeps it if the library moves. `Team:AvatarLibrary:Dir` can point at another library,
or at nothing to turn it off. PRs: #137, then #152, #506, #511.

**3.6 Profile card.** The same card is drawn for an Agent in the roster and in a popover from
any avatar. It shows the title, Adapter, Model and Effort. An Agent's title also sits
beside its name in message headers. The Agent's card lists **What this Agent reads**:
every part of its next session's prompt, where each comes from and where to change it. From
a Room header's avatar, the popover also offers **Start afresh**. PRs: #187, then #257,
#268, #270, #292, #335.

**3.7 The Human's profile.** **Settings > Profile** holds the Human's Name, Alias (a second name
that @mentions them), Title, description and avatar, drawn as the same card an Agent gets. A
blank system opens on this page until a profile is saved. Renaming yourself moves your
avatar and Tasks with you. PRs: #200.

**3.8 The Chief of Staff.** A built-in Agent that helps assemble a Team: it proposes Agents and
nothing is created until the Human approves. Its card has **Reset to default** instead of
**Remove**. It carries the `team-building`, `team-rollup`, `ingest`, `wrap-up` and
`brain-check` Skills and summarises digests and declined Requests.

It is also the front door for quick captures. A Team's fact goes to that Team's Memory
through the Helper, a dated item or an undecided idea becomes a Task, a Huddle-wide fact goes
to Huddle-wide Memory, a rule becomes a proposed `AGENTS.md` line, and anything it cannot
place gets one question. A new Chief of Staff starts on Opus at medium effort, and the Human can change that.
At start-up an unedited older file updates to the shipped text and keeps its name, alias,
Model, Effort and Work mode; an edited file stays as it is, with one Notification per shipped
version. It reads every Room when it searches, and a team manager's `reported.md` before it
summarises a digest. PRs: #71, then #136, #377, #385, #406, #569, #580.

**3.9 The Agent's browser.** Every Agent has a browser of its own, on from the start, through
Playwright MCP and an Edge or Chrome already installed. Reading a page runs at once; every
click, key press, form step, upload, dialog answer or script asks the Human first, on a
card in the Room that names the Agent and shows the step. The choices are **Allow once** or
**Refuse**, there is no "always allow", and a card unanswered for ten minutes is refused along
with every further step of that Turn. The first use opens a window on this computer, where the
Human types any password or code. The sign-ins stay with that Agent's own profile, which
**Remove** deletes and a rename keeps. A switch under **Account connectors** on the card turns
it off or on at once with no restart; it is not offered on another Adapter than Claude, with no
Edge or Chrome installed, or in a Work mode that never asks. An Agent can browse in one Room at
a time. Each switch is recorded in the Feed. PRs: #650.

**3.10 Account connectors.** The user's claude.ai account connectors (mail, calendar, files)
are off for every Agent, the Chief of Staff included, and an Agent cannot turn them on. A
per-Agent switch on the card, offered for the Claude Adapter only, asks "Give {Name} access to
all your connected accounts?" and restarts the Agent. Each use then asks the Human on a card
that names the Agent and the tool and shows the request as sent, with **Allow once** or
**Refuse**; ten minutes unanswered refuses it and every further connector call of that Turn.
A rename, **Reset to default** on the Chief of Staff and a Work mode that never asks switch it
off. The Feed records each switch. PRs: #433.

## 4. Adapters, models and work modes

**4.1 Adapters.** A Persona names the Adapter it runs on (Claude by default; an Adapter is
fetched from nuget.org). An Adapter carries its own environment, and a Turn that goes
silent past a timeout fails visibly. Changing the Adapter resets Model, Effort and Work
mode, and the card says so. An Adapter's advisory arrives as a **Notice** row in the Turn's
activity, never in the Agent's reply, and a sign-in that expires during a Turn tells the
Human to sign in again. PRs: #59, then #60, #336, #338.

**4.2 Model and Effort.** Picked per Agent on its card. A change restarts the Agent. A role in
a team type names a tier (strongest, balanced or fast), and
`Team:Teams:Tiers:<adapter>:<tier>` maps it to a model. PRs: #59, #136.

**4.3 Work mode.** How much the agent may do before asking, from the modes its Adapter
advertises. For Claude these are **Agent default**, **Manual** and **Accept edits**. Plan,
Auto and Bypass permissions are hidden by `Team:Acp:HiddenModes`. On an Adapter that
isolates user settings, an Agent with no usable Work mode runs in Manual at every open and
resume, whatever `defaultMode` the user's own settings name, so Huddle answers the
permission requests. Writes into the agent's own `~/.claude` folder are always refused.
Phase 1 is built; phases 2 and 3 are only designed. PRs: #106, then #415.

**4.4 Adapter commands.** `@nova /compact` (mention first, then the command) runs a command the
Adapter advertises, here compacting the Agent's conversation. The Agent then posts one line,
such as "Compacted my conversation: 50,624 → 2,964 tokens". Only the Human can run one, and
only listed commands work. **Stop** cancels one. PRs: #103.

## 5. Rooms and chat

**5.1 Rooms, Messages and the reply rule.** A Room holds the Human and one or more Agents; each
transcript is an append-only `rooms/<roomId>.jsonl`. In a one-to-one Room the Agent answers
everything. In a group Room an Agent answers only when mentioned, and `@all` wakes every
Agent in the Room. `/invite @alias` or the **Invite agent** button adds an Agent. PRs: #57
(following), #274 (`@all`).

**5.2 Following a Room.** An Agent can follow a Room and be woken without being mentioned.
PRs: #57.

**5.3 Budget.** After a set number of Agent replies without the Human speaking, a Room pauses
and asks whether to **Continue** or **Leave paused**. The Human's next Message resets it.
PRs: #5.

**5.4 A Room says why it stayed quiet.** When a Message wakes nobody, the Room says why instead
of showing nothing. PRs: #56.

**5.5 Silent replies.** An Agent with nothing to add replies `NO_REPLY`. A reply that is only
the token posts nothing, and one that ends with it loses the token. Every Agent is told this,
and told that a promise of later work needs a way back (a Task, a Job or a Mention), or the
work is done now. PRs: #371, then #375.

**5.6 Streaming, Stop and visible failures.** A reply streams as it is written, **Stop** cancels
a Turn, and a failed Turn says so in the Room. PRs: #8.

**5.7 Turn detail and Spend.** While an Agent works, its reply shows its last six tool calls as
rows (Waiting, Running, Failed or done), with the file and line. An edit row opens an **Edit
preview**. The card shows **Spent since start** per currency, and any Agent with stored
spend also shows **Total spent**. Tool-call rows are not saved. PRs: #99, then #136, #367.

**5.8 Room Sessions.** Each Agent keeps a separate conversation per Room, opened on demand,
closed after 30 minutes idle, at most 3 open per Agent, and resumed where the Adapter
supports it. Otherwise it catches up from the transcript, up to 20 Messages and an estimated
100,000 tokens, dropping the Agent's own older Messages first and keeping its newest 5.
**Settings > Sessions** edits both limits without a restart. PRs: #72, then #292.

**5.9 Start afresh.** From a Room header's avatar, the Human can give an Agent a new session in
that Room while it keeps every Message. It applies between Turns only, and is offered only
for an Adapter with one session per Room. The new session rereads with Catch-up, the old one
is deleted in the background, and the Room shows a note with **See what it reread**, which
opens the text as sent. PRs: #292, then #338.

**5.10 Context fill.** The **Members** panel shows how full each Agent's context window is in
this Room, as a percentage and a bar that turns amber at 75% and red at 90%. Its tooltip ends
with the Model that answered. The reading survives a restart. PRs: #247, then #256, #338.

**5.11 Conversation layout.** The Human's Messages sit on the right in an accent-tinted bubble;
an Agent's text sits beside its avatar with no card. Day separators group the Messages, and
a Task change that wakes an Agent shows as one slim line. PRs: #74, then #135, #198, #208.

**5.12 The composer.** It starts at 3 lines and grows to 18, with a **Send** button inside it.
PRs: #163, #173 (small changes; listed here for completeness).

**5.13 @ and # references.** `@` picks an Agent, Room, Team or Library file (`@room:` and `@lib:`
narrow it); `#` picks a Task. A sent reference keeps a snapshot, so it later shows a
**changed** or **(unavailable)** chip. Agents get a Referenced items header telling them
what each reference is. **Copy link** on a Room makes the same reference. PRs: #172.

**5.14 Attaching files.** The paperclip, paste or drag-and-drop saves each file into the Library
at once: the **Raw** root, per Room, or the Team's `raw/` folder for a Team chat. The file's
path goes into the Message, and the file's document history logs it as created. Removing the
chip before sending deletes the file and its history. The limits are 25 MiB per file and 10 per
Message. SVGs and programs are refused, checked by content, not name. PRs: #170, then #335, #447.

**5.15 Rename, archive and delete.** Rename with the pencil (not for a Team Room). Archive hides
a Room, which still works. Delete removes it and permanently deletes the transcript, after a
confirmation. PRs: #49, #65.

**5.16 Team chats and the Team Room.** A Room belongs to the root or to one Team. Every Team has
a Team Room holding the Human and all its members; it is created with the first member and
cannot be renamed. A member's **Message** button opens that Team's own one-to-one, which is
labelled with the Team's icon and name (the Agent's name, then the Team) and listed under
**All**, separate from the Agent's one-to-one under **Rooms**. A Room may belong to one
Project of its Team. When a Team goes away, its Rooms are archived. PRs: #169, then #508, #639.

**5.17 Waiting Messages survive a restart.** A Room's waiting Turns are kept in `team.db`, so a
Message an Agent had not answered survives Huddle closing, crashing or the computer
restarting. The Room shows "Waiting for Nova: 2 Messages" with **Don't answer**, and **Stop**
also calls off what waits behind a Turn. On the next start each Agent answers once it is
online and the Room says "Picked up after a restart."; a Turn cut off half way runs once more,
and the Agent is told to check what it already did. A Message more than a day old is not
picked up (`Team:TurnQueue:ResumeWithin`), a Turn the Human stopped stays stopped, and a Room
whose Budget is spent answers on **Continue**. A Question asked with option buttons is still
waiting after a restart. PRs: #656.

**5.18 Folded Turns.** In an Agent's one-to-one Room, every Message the Human sends while the
Agent is busy, or that waited through a restart, is answered in one Turn and one reply. A group
Room answers each Mention on its own, and a command such as `@nova /compact` ends the group.
PRs: #658.

## 6. What Agents can do from a chat

**6.1 Questions (`ask_human`).** An Agent asks the Human a question with options, drawn as a
card in the Room; the answer comes back as a Message. PRs: #110.

**6.2 Elicitation.** ACP elicitation requests (AskUserQuestion, retry dialogs and MCP forms) are
bridged to the same kind of card as `ask_human`. PRs: #113.

**6.3 Image and PDF prompt blocks.** A Library image the Human names reaches the Agent as an
image block, not a path. A named PDF goes as an embedded document to an Adapter with no file
tools, up to 2 PDFs and 10 MiB per Turn, and otherwise stays a path line, so an Adapter with
file tools, such as Claude, reads the file itself. PRs: #111, then #338.

**6.4 The `search` tool.** Agents can run Global Search. By default it covers only the Rooms the
Agent is in or that belong to its Teams; `scope: "all"` searches every Room and is logged. It
matches the exact phrase, and a multi-word search with no hits says so and suggests one or two
words. Rooms are searched like grep: every matching Message, with `regex`, `context`, `count`
and filters on Room, Team, sender and time, and a Room's latest Messages on request. The Chief
of Staff searches every Room by default and reads any Team's records. PRs: #260, then #408,
#580.

**6.5 Agent tools in general.** Agents work through `mcp__team__*` tools: Tasks
(`create_task`, `update_task`, `close_task` and others), `add_evidence`, `read_skill`,
`log_change`, Requests (`file_request`, `triage_request`, `confirm_request`,
`list_teams`), `propose_sprint`, `read_records`, `propose_retro_changes`,
`mark_retro_row`, `memory`, `propose_team_memory`, `jobs`, `get_help`, `search` and
`goldfish`. Each is listed under its feature.

**6.6 Goldfish mode.** An Agent hands one Room to a clean copy of itself for a fresh-eyes
look. The copy has the same Name, Persona, Model and rules but no Room history, no Memory and no
`AGENTS.md`; it sees only a brief of at most 1,000 characters (`Team:Goldfish:MaxBriefChars`) that the
Agent writes with `goldfish(instructions)` and the files the brief names. The Human asks in a
Message ("@Ruth, goldfish docs/spec.md: can it be built as written?"), or a Team's process asks for
it, as the spec-driven process does for spec reviews. The Room shows a row with the brief word for
word, a header chip "Ruth in goldfish mode" with the minutes and a **Call back** button, and a
fish on the avatar; each Message from the copy carries the Agent's name, a fish and the word goldfish for good. Any Mention
of Ruth in that Room goes to the copy, which asks by posting, while Ruth answers normally in
her other Rooms. The copy leaves with `goldfish_done`; **Call back**, **Stop** or a Message that is
only "@Ruth comeback" ends it at once, and a restart ends it as interrupted and never resumes
it. A row "Ruth is back" gives the Turns and the cost, and the result arrives as a Message from
Huddle. A comment or review the copy writes is marked "goldfish" on the Task and in the document
history. The copy cannot read Memory, and `memory`, `propose_team_memory`, `search` and
`read_records` refuse in it. It is offered only to Agents on a Claude Adapter, needs
`Team:Acp:MaxConcurrentTurns` at 1 and has no time or spend limit. `Team:Goldfish:Enabled` turns it
off. PRs: #670.

## 7. Teams and Projects

**7.1 Teams.** A Team is a label in each member's `teams:` list plus a folder `Teams/<Team>/`,
optionally with `_team.md` (type, manager, workspace, purpose, avatar, gates, schedules). An
Agent can belong to many Teams. **Duplicate** copies a Team's roster into a new Team, hiring
each member afresh. Team **Spent** totals cost per currency. A Team can be renamed or
archived but not deleted in the app. PRs: #96, then #136, #335, #523.

**7.2 Team avatar.** A Team has its own avatar, drawn as a rounded square so it never reads as
a person, and stored in `_team.md`. It shows in the sidebar, the Team page, the Feed, search
and the Team Room header. PRs: #275.

**7.3 Projects.** Any ordinary sub-folder of a Team folder is a Project (`memory`, `raw`,
`outputs` and names starting with `_` or `.` are excluded). A Project has no members of its
own, and has a square avatar rather than an icon. Its own **Spent** adds up the Turns that ran in
its Rooms, one amount per currency, and deleting a Room keeps what it spent. A Room
started with the "+" under **ROOMS** while a Project is selected belongs to that Project, and
cannot be moved. PRs: #96, then #335, #520, #634.

**7.4 Team and Project pages.** Tabs: **Members** (role cards and roster), **Library** (the
Library scoped to the Team), **Tasks** (List or Board, one swimlane per Project),
**Process** (for a Team with a `_team.md`), **Playbooks** and **Details** (type, name, purpose, Library
folder, rename, archive). The header holds **Show shortcut** / **Hide shortcut**, **Edit
avatar...**, **Duplicate** and **Archive**. A Project page has Library and Tasks only, with
**Edit avatar...** and **Archive** in its header. PRs: #96, then #100, #101, #151, #185, #335, #516.

**7.5 Build a team.** A four-step wizard at `/teams/build`: **Team type**, **Team details**
(name, purpose, Library folder with a folder picker, empty unless the type requires one),
**Hire** and **Approve & hire**. In **Hire**, each role card's **Add** drafts an Agent with a
drawn name and avatar, which can be re-rolled, edited or removed. A draft takes its Skills
from the type's role, and one not yet downloaded is marked and can be unticked. A draft whose Adapter reports nobody signed in
shows a warning with **Check again**; it never blocks the hire. Custom roles
can be written and are saved in `custom-roles.json`. Approving copies the Skills the roster
needs, creates the Team, writes its process (with a **Process** choice when a type offers
several), hires the drafts and makes the manager. The Team page's **Members** tab reuses the
Hire step, with **Remove from team**, **Delete agent** and **Save changes**.

Hiring a typed Team starts a paid Turn: Huddle posts the onboarding steps in the Human's
one-to-one Room with the manager, who proposes a starter Task in its reply and creates it only
after the Human says yes. PRs: #185, then #201, #202, #339, #374, #633.

**7.6 Team types and the hiring pool.** 26 built-in team types, with **Development Team** and
**Household** first, plus the user's own in `TeamTypes/`. A single hiring pool holds every
job, filtered by two tag clouds. **Settings > Team types** lists them and their
**Problems**. Hire limits: 8 Agents per Team and 16 live processes by default. PRs: #136,
then #154.

**7.7 Household team.** The `household` type runs a family's whole home. The Household Manager
is the only required role; School Liaison, Meal Planner, Home Keeper, Health Admin, Schedule
Keeper, Finance Tracker, Comparison Shopper and Retro Agent are optional. Its
household-basics process gives every job one owner, opens one Task per occurrence of a
recurring chore, gates every spend or signature behind the Human, and runs a weekly check-in
and a 28-day retro. A role whose connector (such as `email:read`) is not listed in
`connectors.json` shows **Not connected yet**, which never blocks the hire. PRs: #136, then #281, then #545.

**7.8 Rename a Team or a Project.** **Rename team...** and **Rename project...** in a row's
More menu, and **Rename** on a Team's **Details** tab, ask for the new name (checked by the
same rules as a new one) and then show a confirmation of what can go wrong: members restart and
lose their working context (a member mid-Turn is named), Agents hear of the new name only on
their next Turn, their own notes keep the old name, links outside Huddle break, and the old
name stays reserved. Only the Human can rename; Agents have no tool for it. The folder, Rooms,
Tasks, Requests, saved Views and links inside Huddle move with it, a journal lets a rename
interrupted by a stop finish at the next start, and the Team Room gets one line saying so. The
old name still works: a link or Reference to it opens the renamed Team, an Agent that reads by it
is forwarded and told, and one that writes by it is refused with the new name. A step that keeps
failing raises a Feed notice. PRs: #523.

**7.9 Project scope in the sidebar.** In the **Teams** sidebar a Team's Projects are a
selectable list beside a **Switch team** selector. Selecting a Project, or **No project** (the
Rooms, Tasks and files that belong to no Project; it appears once a Project exists), narrows the
**ROOMS**, **TASKS** and **LIBRARY** below to it, with routes such as
`/teams/{Team}/projects/{Project}/tasks/{View}` and `/teams/{Team}/no-project/tasks/{View}`. A
Room belongs to at most one Project; one whose Project is later archived shows among the Team's
own Rooms. PRs: #508, then #516.

## 8. Running a Team

**8.1 Process and gates.** A Team's process is its `AGENTS.md` prose (steps, onboarding,
Definition of Done) plus settings in `_team.md`: gates, `checkCommand`, WIP limit and digest
and retro cadence. Build a team writes them, or the manager's card does, from the type's
template or blank, and neither overwrites what is there. Six gates exist:
`accept-before-merge`, `qa-evidence-before-done`, `checks-before-merge` (refuses an Agent's
`git merge` or `git push` until the check command's Evidence passes),
`spec-approved-before-build`, `failing-test-before-fix` and `blockers-done-before-start`
(refuses a move to In Progress, Needs You or Done while a blocker is not Done,
Cancelled, Duplicate or Rejected; the development processes have it, ADR-0085). A review or QA
check is not a state: it is a child Task tagged `review` or `verify`, assigned to the reviewer
and listed in the work Task's blockers, so this gate holds the work Task while it is open.
`accept-before-merge` is a gate no Team switches: the Needs You level decides which merges wait.
A generated hook makes the Claude Adapter ask before any `git merge`
or `git push`, even in its `auto` mode, so the gate is consulted. The **Process** tab shows
`AGENTS.md`, the Team's Needs You level and a switch per gate that is on (not `accept-before-merge`). Turning one off asks first, and only the Human
can override a gate, with **Move anyway** on the Task, which is logged. PRs: #136, then #335, #397, #398, #588, #672.

**8.2 Accept before merge.** At the default level, a merge into `main` by any Agent of any
Team, and any push Huddle sees, is refused until an open Task of that Team has the Human's
Accept, whatever the Team's `gates:` line says; merging `main` into a feature branch still
runs. A Team whose process lists `accept-before-merge` (every development process) tells the Agent
to name the merge as the Task's Action, and a refused Agent sees only a plain denial. An Accept counts for 24 hours, or one hour when it took the Task straight to
Done, and another merge by the Team also passes in that time. It guards against an Agent's
ordinary mistake and is an early warning, not enforcement or a security boundary. PRs: #402.

**8.3 Needs You level.** A level per scope (Settings, a Team, a project; the most specific wins)
says how much waits for the Human. Under Ask me (the default) nothing changes. Under Run inside
the Team a local merge into `main` of a branch named after the Task's id, a Sprint proposal and
new Backlog Tasks go ahead without an Accept, each recorded on the Task and in the Feed; every
push Huddle sees in an Agent's command, an Action and a connector call still wait. The gate reads the
command line, so a script, a git alias saved earlier, a Task created straight into a project set to Run inside the
Team and the 24-hour push window get past it; the level control says so in a "What this cannot catch" caption, and
branch protection on the git server is the guarantee (issue #608 is the parked design for enforcement). The level is kept in `team.db` by scope id, and
a Task's level is fixed where Huddle placed it and can only get stricter. An optional spend cap per scope falls back to Ask me and asks Yes or No for one more cap.
The level replaces the Process tab's `accept-before-merge` switch and its start-up migration,
and only the Human can set it or the cap. PRs: #601, then #605.

**8.4 Evidence and the tool-call log.** Agents attach Evidence to a Task with `add_evidence`,
which adds an `evidence` entry to the Task's Timeline. An entry holds a command with its exit
code, a note, or both, and a command with no exit code is refused. Every tool call is logged
with its arguments, and an App Tool's answer, to `logs/tool-calls/<Agent>/<date>.jsonl`, so an
Evidence entry can point at the exact call. PRs: #136, then #340, #379, #591.

**8.5 Digests, retros and manager wake-ups.** Digests run on time and retros run after N Done
Tasks or N days, posted in the Team Room. The manager is woken about Task changes, and the
assignee of a blocked Task is woken once when its last blocker finishes (Done, Cancelled, Duplicate or Rejected). PRs: #136.

**8.6 Requests between Teams.** One Team files a bug, feature, feedback or question into
another's Backlog as a Task marked **Needs triage**. The receiving manager accepts it,
declines it with a reason, or asks for more information. The Request closes only when the
requesting Team's verifier confirms delivery, with `confirm_request`, which the built-in
`request-confirmation` Skill grants and every role that verifies carries. Reaching Done wakes the requesting manager with
a line naming who can confirm, and a refused close or confirm says what is needed. A
**Requests** filter and a chip mark them. PRs: #136, then #382, #589.

**8.7 Retrospectives.** A Retro agent reads the Team's own records (`read_records`) and proposes
up to three exact-text changes to `AGENTS.md`, `_team.md` or a member file
(`propose_retro_changes`). The Human accepts or declines each from a card in the Room. Accepted
changes go into `Retro register.md`, and the next retro verifies them. A proposal may also target a Team's
customized Playbook (8.10), never the Huddle rules. PRs: #136, then #335, #678.

**8.8 Sprint proposals.** A manager can `propose_sprint`; **Approve** creates the Sprint and
adds the Tasks. When every Task it names runs inside the Team, Huddle approves it at once
and tells the manager. PRs: #136, then #601.

**8.9 Checking a web page.** When a Task's acceptance names a web page or a UI, a Development
Team's QA Engineer verifies it with the built-in `browser-check` Skill; the manager does it when no
QA Engineer is hired. The Skill grants no tools and is separate from the Agent's own browser. The
Agent writes a script in a scratch folder outside the repository, installs Playwright there, and
drives the Edge installed on Windows or Chrome elsewhere. It opens the app on `localhost` only
(the Skill says so; Huddle does not enforce it), acts as a person would and checks what changed
after each step, and a step that does nothing counts as a failure. The Evidence names the
command, its exit code and the browser, and lists the steps driven. PRs: #553.

**8.10 Team Playbooks.** Every Team has two Playbooks, a **Member Playbook** every member reads and a **Manager
Playbook** the manager reads as well, shown on the Team page's **Playbooks** tab (Manager first). Each is sent in full at the
start of every session, after the `AGENTS.md` levels, and an edit reaches a running Agent at its next Turn. Each has a
read-only **Huddle rules** part, the mechanics the code enforces, which is always sent. The default of each is a Prompt
(`systemPrompt.managerPlaybook` and `systemPrompt.memberPlaybook`, edited under **Settings > Prompts > Playbooks**).
**Customize for this Team** writes `Teams/<Team>/_manager-playbook.md` or `_member-playbook.md` from the default and
opens it in the Library; that file then replaces the default whole for the Team. An uncustomized Playbook is not a
file, so an improved default reaches every such Team. When the default changes after a customization, the tab shows
**Default updated** with **Show changes**, **Keep my version** and **Reset to default...**. A body over the limit
shows a warning and Agents read only the first part. An Agent cannot edit a Playbook; a retro may propose a change.
The built-in process files no longer repeat the generic text the Huddle rules now carry. A Project's **mission** is a
Task the Human assigns the manager; the manager keeps it in the Project's Team Memory as `mission.md`, plans child
Tasks and works through them, and only the Human creates a Project. Team-building files the goal it collected as a
Task for the new Team's manager. PRs: #678, #681.

## 9. Tasks

**9.1 Tasks are Markdown files.** `Teams/<Team>/[<Project>/]_tasks/<ID>.md`, with IDs such as
`BUSI-0001` (prefix from the Team, numbers never reused). There are eight statuses: Backlog,
To Do, In Progress, Needs you, Done, Cancelled, Duplicate and Rejected. Priority,
assignee, parent, blockers, duplicate-of, tags, start and due dates, a description and an
append-only Timeline of events, comments and Evidence (with day and time); Evidence is an
`evidence` entry on it, and the Human and Agents add comments there. A review or QA check is
not a status: it is a child Task tagged `review` or `verify`, assigned to the reviewer and listed
in the work Task's blockers. The reviewer approves by moving it to Done, or leaves it open with
findings as a comment. A Task an Agent creates is filed in the Project of the Room it was asked
for in, then its parent's, unless one is named. Closing is separate from status and moves the
file to `_closed/`; there is no delete. Hand edits are noticed and logged, and two people
editing the same Task get a per-field merge. PRs: #79, then #245, #591, #615, #672.

**9.2 Wake on change.** A change to an Agent's Task wakes it with a Message in the Task's origin
Room, or a fitting Room; a Project's Task wakes its Agent in a Room of that Project. Changes
within 5 seconds are combined. A review Task is assigned to the reviewer, so the ordinary
wake reaches them (ADR-0096). Each Task allows 10 Agent-made wake-ups before the Human must **Allow 10 more**. A
hand-off refused by a spent Task budget or a paused Room waits: one Notification names who is
waiting, and **Allow 10 more** or the Room's **Continue** delivers it. After a restart the
Notification stays, with **Wake {Agent}** on its card. PRs: #79, then #340, #372, #412, #615, #672.

**9.3 Views, List and Board.** **All Tasks**, **My Tasks** and any saved View. A View can be a
List or a Kanban Board. The toolbar has a **Filter** menu (Team, Project, Assignee, Sprint,
Status, Priority, Blocked) whose set filters show as chips, a **Group by** chip, search and
**Dense**. Every change is saved to the View as it is made; there is no Save button and no
View editor panel. **Fields…** reorders columns, and List columns can be resized. Dates read
naturally ("Today", "Fri") and overdue dates are bold with a warning icon. The **Tasks**
sidebar lists **All Tasks**, **My Tasks** and the saved Views at its top, and a Team's, a
Project's or **No project**'s Tasks open the same Views narrowed to it. PRs: #79, then
#81, #94, #108, #177, #181, #216, #271, #522.

**9.4 Reading and editing a Task.** Clicking a row or card shows the Task, read-only, in the
Right sidebar's **Task** panel. **Edit** opens the Task in its own tab ("Edit BUSI-0001")
with sticky **Save** and **Cancel**, a dot for unsaved edits and a Markdown toolbar on the
description. That edit page is the only Task editor: **New task** and **Make a copy** open
it too. Closing the tab with unsaved edits asks first. The panel and the edit page of a saved
Task both end with the **Timeline** and an **Add a comment** box; a comment saves at once, up to
4,000 characters, and nothing on the Timeline can be edited or deleted. PRs: #231, then #232,
#246, #264, #591.

**9.5 Task attachments.** Link a Library file into a Task, or upload one to the Task's own
folder under `raw/Tasks/`. PRs: #221, then #335.

**9.6 Sprints.** A Sprint is only a name in `sprints.md`, with no dates or state. It is created
by typing a new name on a Task and disappears with its last Task. Views filter, group and
sort by Sprint, with `@current` and `@none`. PRs: #132.

## 10. The Human gate, Needs you and Helpers

**10.1 Needs you.** The sidebar card and the **Needs you** page list Rooms waiting on the Human
(an unanswered mention, an open Question or Form, a spent Budget, a Job for the Human) and
gated Tasks waiting for a decision, and a question from a Team or Project that reached its
Needs You spend cap. A Room leaves the list when the Human posts or presses the check mark.
The Task rows survive restarts; a Room's unanswered mention and a waiting Question are listed
again after one, and a Form and any check mark are not. PRs: #166, then #230, #334, #601, #656.

**10.2 The Human gate.** A Task can require the Human's acceptance. It must if it names an
**Action** that leaves the Team, such as sending, paying, signing, publishing or merging. Its
**Proof** checklist comes from the Team's **Definition of Done** in `AGENTS.md`, using four
rule kinds. When Evidence or an update completes the Proof, Huddle moves a gated Task from
In Progress to **Needs you** by itself and wakes nobody; setting an Action on a ready Task does the
same, unless the Human set it. The assignee may move the Task too, and must when the Proof
completes with no Task write. The Human **Accepts**, **Requests changes** or **Declines** from
a decision card. Only the Human can make a gated Task Done. Dragging a gated card to Done opens
the decision instead. PRs: #230, then #250, #335, #379, #402, #672.

**10.3 Helpers.** Small background agents that make one stateless judgement, with no tools, Room
or memory, and let Huddle apply the verdict. There are four: **Needs Human**, **Team Memory**,
**AGENTS.md Changes** and **Memory Tidy** (see 16.7). **Needs Human** reads an Agent-written Task and turns the Human gate
on when the work leaves the Team, recording the step as the Task's Action if the Task states
one and none is set. It only adds caution, never judges the Human's own Tasks, and never looks
again at a Task where the Human turned the gate off. Each Helper has a daily call cap with
**Continue (+10 today)**. **Settings > Helpers** sets its on/off, Adapter, Model, Effort and
cap. PRs: #240, then #287, #376, #400, #657.

## 11. Scheduled Jobs

**11.1 Jobs.** An instruction that runs on a timetable for the Human or one Agent, in its own Room
named ⏰ and its title. It can run once, daily, on weekdays, on chosen days, monthly on a day
(a day the month lacks runs on its last day) or every few hours. **Scheduled** in the Activity
bar lists every Job, and **New job** previews the next runs. A Job's page has **Edit…**,
**Pause**, **Run now**, **Open Room** and **Delete job…**, which keeps the Room. An Agent's Job
Mentions it, so it takes a Turn; the Human's puts the Room on **Needs you** and raises a
Notification. With the `jobs` tool an Agent manages its own Jobs, a manager can also schedule
for its Team's members, and only the Chief of Staff can schedule for any Agent or the Human.
Jobs live in the owner's `schedule.md`, editable by hand, and a line Huddle cannot read is
listed on the page. A run Huddle sees more than two minutes late, after a stop or a sleep,
runs once, marked "ran late" with its due time; it is never run again for the other times
missed. A repeating Job whose next run is within an hour skips the missed one, a one-off more
than 24 hours late (`Team:Jobs:OverdueOneOffWithin`) is removed with a Feed line, and a failed
run is retried each minute up to three times. A paused Job's next run moves forward, so
resuming never finds one waiting. `Team:Jobs:RunOverdue` set to false skips every late run, and
with `Team:TurnQueue:Enabled` off only the Human's own Jobs run late. A shared due-time rule
(`DueCheck`) decides all of it. `Team:Jobs:Enabled` turns it all off. PRs: #334, then #369,
#652, #665.

**11.2 Follow-ups.** An Agent that tells the Human it will check something later ("I'll look at
the build in an hour") sets a Follow-up: a one-off Job that posts into the same Room, which may
be archived and stays so. The Room shows a row such as "Nova will follow up Mon 16 Nov, 15:40:
Check the build" with **Cancel**, and Follow-ups are also listed under **Scheduled**, where
**Delete job...** cancels one. At the time Huddle mentions the Agent in that Room and it carries on
the conversation. A Follow-up due while its Room is paused waits until the Human types or presses
**Continue** and is dropped after a day; one whose Agent or Room is gone is dropped with a Feed
line. An Agent may have 5 waiting at once (`Team:Jobs:MaxFollowUpsPerAgent`; 0 forbids them).
PRs: #667.

## 12. Feed and notifications

**12.1 Feed.** One stored history of what Agents and Huddle did: Tasks created, Rooms started,
documents changed, asks raised and resolved. It never shows Message text and never changes
the work. The **Notifications** bell in the Top Bar counts items that need the Human and
opens the **Notifications** panel, with categories (Needs you, All, Tasks, Documents, Rooms,
Gates, Helpers). The **Feed** tab shows cards by day, with Team and Agent filters,
expandable previews and a live "↑ n new items" pill. Items are kept 30 days.
`Team:Feed:Enabled` turns it off. PRs: #242.

**12.2 Helpers in the Feed.** A Helper's gate change appears as a Notification with its reason. A
Helper that pauses at its cap or keeps failing is reported by **Huddle** in the Helpers
category. A Helper's wake-up Message is posted by Huddle, not by the Human. PRs: #244.

## 13. Library

**13.1 Library.** A file browser and Markdown editor over the data folders. The built-in roots are
**Teams**, **Agents**, **Raw**, **Archive**, **Skill Sources** and **Memory backups** (these three read-only) and
**Prompts**, plus any pinned folders (**Settings > Library**). Markdown opens in **Read**,
**Edit** or **Split**, saved with **Save** or `Ctrl+S`; images preview; a PDF opens as a card
with **Open in default app**; other text is read-only. Saves are atomic and keep encoding and
line endings, with a conflict prompt when the file changed on disk. Delete goes to the Recycle
Bin. Protected folders (roots, Teams, Projects, Agent folders) cannot be moved. PRs: #85, then
#87, #88, #92, #95, #335, #338, #340.

**13.2 Explorer and organising.** The tree is in the left sidebar under **Library**, with the
search, **New note** and **New folder** above it; a file opens in a tab that shows only the
document. A Team's or Project's page has a **Library** tab for its own folder. Drag-and-drop or **Move...** relocates files and rewrites every `[[link]]` pointing at them.
The context menu opens on right-click. PRs: #85, then #164, #189.

**13.3 Links, Backlinks and the Outline.** `[[Note]]`, `[[folder/Note]]`, `[[Note|text]]` and
`[[Note#Heading]]` resolve within a root, Obsidian-style; a missing target offers to create
it. The Right sidebar shows the **Outline**, **Linked documents** and **Backlinks**, which
lists notes and also open Tasks that link to the note. PRs: #85, then #255, #258.

**13.4 Markdown toolbar and `[[` suggestions.** The editor has formatting buttons (bold, italic,
heading, code, lists, task list) with shortcuts, and suggests notes as you type `[[`. PRs:
#217.

**13.5 Reading styles and the document header.** Notes read at a comfortable measure with a
document header: the note's tags, then its first heading, then the history byline. PRs:
#192, #266 (small changes; listed here for completeness).

**13.6 Front matter properties.** A note's front matter is drawn as a typed **Properties** block:
text, list, number, checkbox, date, date and time, aliases, tags, unsupported and mismatch.
Types come from the root's `.obsidian/types.json`, then defaults, then inference. `[[links]]`
and URLs in properties are links and count as backlinks. Aliases are searchable, JSON front
matter is read too, and nothing is written back. PRs: #279.

**13.7 Tags.** Tags are read like Obsidian: inline `#tag` plus front matter `tags`, merged,
case-insensitive and nested on `/`. The **Tags** panel shows the note's tags and a tag tree
with counts. Huddle only reads tags. PRs: #263, then #266.

**13.8 Document history.** Who created, edited, reviewed or objected to a Markdown document.
Agents record entries with `log_change`, held to it by a Claude Code Stop hook on creates and
edits; only an Agent whose role verifies is reminded about reads. The Human's saves, creates,
renames and moves log themselves. It shows as a byline under the heading and a **History**
timeline in the Right sidebar. Renames carry the history with them. A file attached in a chat
or to a Task logs as created when it is saved to the Library, and a review an Agent makes in
goldfish mode reads "Goldfish review by" its name. PRs: #220, then #255, #261, #371, #447,
#670.

## 14. Skills and Skill Packs

**14.1 Skills.** A Skill is a folder of Markdown instructions (`Skills/<name>/SKILL.md`) that an
Agent reads on demand with `read_skill`. It is assigned on the Agent's card. Nine ship built
in: `team-building`, `team-rollup`, `team-management`, `request-confirmation`, `retrospective`, `ingest`, `wrap-up`,
`brain-check` and `browser-check`. `request-confirmation` is held by every role that verifies,
and `browser-check` by a Development Team's QA Engineer and manager. A built-in Skill can be
overridden file by file and restored. PRs: #71, then #136, #335, #553, #589.

**14.2 Skill Packs fetched by your AI tool.** Huddle never downloads Skills itself. The user's AI
tool (Claude Code, Codex) fetches them and adds pointers to `SKILLS.md` in the Skill Sources
folder; Huddle checks every file and shows the state (Ready, Not downloaded, Update
available, Needs setup, Broken). **Settings > Skills** has three tabs: **Skills**, **Packs**
(named sets of Skills for roles, added to an Agent with one chip) and **Sources**. Each Skill
records its provenance in `.source.json`. **Copy prompt** hands the AI tool what it needs and
asks for a byte-for-byte fetch. Every shipped catalog entry but one holds a `sha256` of the
Skill's folder, so a fetched Skill that differs from the pinned commit is Broken with the
reason. Line endings do not count, so a clone made by Git for Windows still matches. The
exception, `garrytan/gstack/gstack`, is the whole repository and holds a symlink, so nothing is
compared for it; an install that already split its catalog into Sources keeps its older entries,
which have no hash. PRs: #215, then #407, #430.

## 15. Second Brain

**15.1 Folders for what came in, what lasts and what was made.** Every Team and Project has `raw/`
(originals, kept as they arrived), `memory/` (facts) and `outputs/` (documents worth reading
again), beside `_tasks/`. Attachments land in `raw/`, and a guard refuses and logs any Agent
change to an original, though an Agent may add a derived file beside it. Each session carries
an index of Memory and of each output's "Read when…" line, and every Agent is taught the
pattern in a Second Brain summary. PRs: #335.

**15.2 Rules in `AGENTS.md`.** Plain Markdown at three levels: `Teams/AGENTS.md`,
`Teams/<Team>/AGENTS.md` and `Teams/<Team>/<Project>/AGENTS.md`. An Agent reads every level
that applies, in full, each session, and the most specific wins a conflict. Only the Human or
an accepted retro change writes these files. A Team made from a type starts with one written
from that type. PRs: #335.

**15.3 Running Agents hear of an `AGENTS.md` edit.** At its next Turn in each Room, an Agent gets a
note of the rules added, changed or removed, and nobody restarts. The **AGENTS.md Changes**
Helper writes the summary once per edit; if it is off, paused or takes over 30 seconds, the
Agent gets the changed lines instead. A small edit it judges not meaningful reaches no Agent
and is listed in the Feed. PRs: #376.

**15.4 Archive.** A Team is archived from its row's menu or its **Details** tab, a Project from its row's menu or its page, and the whole
folder moves unchanged into the read-only **Archive** root. It leaves every prompt, its Rooms
are archived and its Tasks leave the Boards; Huddle first says how many open Tasks it would
hide. Old links open the file with an **Archived** chip. The **Archived** page (Teams, Projects and Rooms tabs, each Room showing where it belongs)
lists what is archived, and **Restore** brings back members, manager and Rooms; it refuses when
something now has the same name. PRs: #335, then #513.

**15.5 Workspaces.** A Team's readable workspace, such as a code repository, is routed into its
members' prompts. The real work follows the workspace's own conventions and `outputs/` holds
the rest. Huddle does not restrict what Agents write there. PRs: #335.

**15.6 Upkeep.** The `ingest` Skill proposes facts, Tasks and documents from new originals, `wrap-up`
proposes what a session produced, and `brain-check` finds drift and files it as Tasks. The Chief
of Staff offers upkeep Jobs and creates each only on a yes. A Memory folder over its limit raises
a daily Feed notice, and the manager moves old facts into `memory/archive/`. PRs: #335.

## 16. Memory and file changes

**16.1 Personal and Team Memory.** Plain Markdown, one topic per file, with a `description` saying
when to read it (an older file's first line stands in): `Agents/<Name>/work/memory/`,
`Teams/<Team>/memory/` and per-Project `memory/`. Each session's system prompt includes an index
of the Agent's own memory and its Teams' memory, one line per file; there is no `MEMORY.md` to
keep. PRs: #72, #96, then #287.

**16.2 The `memory` tool.** Agents list, read, write, append to, replace text in and delete memory
files. It requires a description, refuses a write over a version the Agent has not read
(showing the current text to merge into) and caps a file at 16 KB. A delete goes to the Recycle
Bin, and only when the Human asks. PRs: #287.

**16.3 Team Memory Helper.** The Team's manager and the Chief of Staff write Team Memory directly;
other members propose a fact with `propose_team_memory`. The Helper (Haiku, low effort, 30 calls
a day) decides whether to file it, and in which file, and Huddle writes it. The file's **History**
shows who proposed it and why. A proposal it does not file raises a Notification with the reason;
the proposer is not told. PRs: #287, then #377, #385, #413.

**16.4 Huddle-wide Memory.** `Teams/memory/` holds facts every Agent reads, and only the Chief of Staff
writes it. A new, changed or deleted file reaches each running Agent at its next Turn in each Room,
as the file's name and description, with no Helper and no Start afresh. PRs: #335, then #377, #380.

**16.5 File Changes.** At the start of each turn an Agent is told which files changed in its
watched folders since its last turn in that Room. The watched folders are its `work/`, its
Teams' folders and anything listed under `watches`. An `AGENTS.md` edit reaches it through its
own note instead. PRs: #72, then #376.

**16.6 `reported.md`, what an Agent already told the Human.** An Agent that reports to the Human
keeps `reported.md` in its own Memory: one line for each finding, status, result, decision or
warning it told them, however they asked, with the Room and a few words; Huddle adds the date.
Before it reports it reads the file, so it says "no change since Monday" instead of repeating
itself. Lines older than 14 days (`Team:Memory:Reported:KeepDays`; 0 or less keeps all) are dropped
weekly, and the oldest go first when the file grows too big. An Agent that only does Task work
for a manager has none, the Chief of Staff reads a team manager's before it summarises a digest,
and the Human can edit it in the Library. PRs: #653.

**16.7 Memory Tidy.** A Helper that runs weekly (`Team:Memory:Tidy:When`, Sunday 03:00 local time
by default, or `daily`) over each Agent's own Memory folder, then over Huddle-wide Memory. It skips
a folder with fewer than 10 topic files or one unchanged since its last pass; otherwise it may
merge two files on one topic, shorten one, give one a missing description or remove one that is
out of date, and adds and creates nothing. Before any file changes or goes, Huddle copies it to
the read-only **Memory backups** root in a folder for that pass (deleted after 90 days,
`Team:Memory:Tidy:KeepBackupDays`), and the change does not happen if the copy fails. Huddle
refuses a change that adds a number, date, link or e-mail address the files did not hold, a merge
that loses one, a rewrite that makes a file longer, or a description that needed no change, and
a pass drops at most 3 files from one folder (`Team:Memory:Tidy:MaxDropsPerFolder`). A pass that
changes anything raises one Feed Notification that opens the list of changes, reasons and
refusals; undoing is by hand from the copy. A file with `tidy: keep` beside its `description` is
never seen, and `reported.md` and `MEMORY.md` are never shown. **Settings > Helpers** has a
**Memory Tidy** card with the next and last pass, **Tidy now** and **Open the last list**; it
starts on Claude, Sonnet, low effort, with a daily cap of 20 calls. A missed pass runs once at the
next start. PRs: #654, then #655, #657.

## 17. Appearance and Settings

**17.1 Themes.** Four built-in Theme sets, each in light and dark: **Lumen** (the default),
**Fluent**, **Nordic** and **Cobalt Soft**, aligned with the HuddleUX mockups. A Theme is a
JSON skin (palette, `--hu-*` tokens and structural traits), so adding one means editing data.
The user picks one light and one dark Theme on **Settings > Appearance**, plus an accent
colour, and the Top Bar button switches between them. PRs: #9 (dark mode), then #58, #64,
#74, #81, #171, #212, #213, #284.

**17.2 Prompts.** Every piece of text sent to a model is configuration: defaults live in code,
each override is one file in the **Prompts** root (deleting it restores the default), and
**Settings > Prompts** edits them, picked up without a restart; the default Manager and Member Playbooks (8.10) are
listed in a **Playbooks** group. PRs: #6, then #69, #335, #678.

**17.3 Spending.** **Settings > Spending** is a read-only view of what the Adapters reported: a line
per Team, the Chief of Staff, each Agent in no Team and the Helpers, then a **Total** that counts
every Agent once. Currencies are never added together. It sets no budgets or limits. PRs: #367.

**17.4 Settings layout.** Sections in a left menu: **Profile**, **Prompts**, **Helpers**, **Spending**,
**Appearance**, **Needs You level**, **Personas**, **Skills**, **Team types**, **Library**,
**Sessions** and **Remote access**. PRs: #70, #204, #292, #367, #601.

## 18. Remote access

**18.1 Strict local by default.** Out of the box only the computer running Huddle can use it,
with no sign-in.

**18.2 With Strict local off**, Huddle follows OpenClaw's gateway model:

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

## 19. Developer tooling

**19.1 The seeder (`huddle-seed`).** A console app that builds a complete, repeatable test data
set. Its scenarios include `software-co` and `uat`, with Team Rooms, references, uploads,
Sprints, Requests, Task attachments, document history, tagged notes and front matter
examples, Jobs with their Rooms and schedule sections Huddle cannot read, and an archived Team,
Project and Team Rooms for the **Archived** page. PRs: #98, then #196, #226, #265, #279, #282,
#519, #521.

**19.2 Demo agents off by default.** The built-in demo agents no longer start unless asked.
PRs: #133.

**19.3 Built on .NET 11.** Every project targets `net11.0` on SDK 11, with 11.0.0 packages. PRs:
#504.

**19.4 `run.ps1` development runs.** Each run takes its own Agent pipe, `team-<Port>` by default
(`-PipeName` overrides it), so two ports run side by side. It builds Release without the code
analyzers unless `-RunAnalyzers` is given, and `-LogLevel` sets the console level for the run. PRs:
#524, then #527, #510.

**19.5 Diagrams in the User Guide.** The User Guide draws a built-in diagram inline from a
catalog id, starting with the layers of the Second Brain. The SVG is inlined from the app and never
served as a file. PRs: #598.

## 20. Replaced or removed

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
| Teams' Library folder listing the Projects, and Rooms and Task Views that were Team-wide only | A selectable list of the Team and its Projects, each with its open Task count, and the Rooms, Tasks and Library link of the selected one below it; a Room may belong to one Project (ADR-0082) | #508, ADR-0082 |
| A Chief of Staff-only team build | **Build a team** wizard, with the Chief of Staff as one route | #71 → #185 |
| Three radio buttons in the avatar editor | Choice cards | #68 → #272 |
| The MudToggleGroup | The animated segmented control | #93 |
| "Teammate" and "Team member" as names for an Agent | "Agent" everywhere | #267 |
| A Team's `Process.md` | Prose in `AGENTS.md`, settings in `_team.md`; an old file is no longer read and gets a start-up warning | #136 → #335 |
| `Instructions.md` files at Huddle, Team and Agent level | `AGENTS.md` at Huddle, Team and Project level | #287 → #335 |
| `prompts.json` | One file per changed prompt in the **Prompts** root; an old file is not migrated | #6, #69 → #335 |
| Team chat uploads in the Team's `Uploads/` folder | The Team's `raw/` folder; a root Room's files go to the **Raw** root | #170 → #335 |
| "Chats" as the name of a conversation in the UI (the Activity bar button, sidebar and archive tab) | "Rooms"; "chat" stays a verb | #531 |
| Teams that could not be renamed | **Rename team...** and **Rename project...** | #523 |
| A Team page's **Files** tab and a Library tree on its own page | A **Library** tab on the Team page, and the Library tree in the left sidebar under **Library** | #516, #522 |
| The Tasks views grouped in the sidebar under a heading | **All Tasks**, **My Tasks** and the Views listed at the top, with a Team's, a Project's or **No project**'s Tasks at their own routes | #522 |
| A **Remote access** button in the Activity bar | A **Remote access** tab in **Settings** | #283 (the move is in the shell; the PR was not identified) |
| A Task's separate **Change log** and `## Evidence` section | One immutable **Timeline** of events, comments and Evidence | #79, #136 -> #591 |
| The Review status, the Board's Review column and the wake "into Review" | A review is a child Task tagged `review` or `verify`, assigned to the reviewer and listed as a blocker of the work Task (ADR-0096) | #672 |
| The Process tab's `accept-before-merge` switch and its start-up migration | The Needs You level, set in **Settings**, on a Team's **Process** tab or on a Project | #402, #398 -> #601 |
| A Job more than two minutes late is skipped | It runs once, marked "ran late" | #369 -> #665 |
| The earlier bundled avatar pictures | 126 generated pixel-art avatars; a Project's icon becomes a square Project avatar | #137 -> #506, #520 |
| Intel macOS builds | Apple Silicon only; an Intel Mac fails the installer's machine check | #128 -> #659 |

## 21. Designed but not built

These are specified in `docs/` but not on `main`, so no PR above delivers them:

- **21.1 More Adapters:** Codex, Antigravity and Copilot over ACP, with Gemini CLI as a
  secondary preset. Designed 2026-10-19.
- **21.2 Work mode phases 2 and 3.**
- **21.3 The rest of Long-running Rooms:** session retention, phase 2b, the single-session mode and
  phase 4. Closed 2026-10-31 and not pursued.
- **21.4 Adapter-side compaction:** advertising `session.compaction` from `claude-agent-acp`. Closed
  without being built, because a paid spike showed it removes the token figures `/compact` reports.
