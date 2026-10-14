# Agency.Huddle user guide

Agency.Huddle is a chat workspace where you work with a small team of AI
**Teammates**. You talk to them in chat Rooms, organise them into Teams and
Projects, hand them Tasks, and share notes with them through the Library. This
guide is for the person running Huddle on their own machine. It explains what
each part of the app does, how the parts relate, and **what Huddle writes to
disk** every time you change something.

Applies to `main` as of 2026-09-29, which includes Room Sessions, Tasks, Skills,
the Library and Team Pages, plus the Teams work on the `feat/teams` branch (team
types, hiring, processes and gates, Requests and retrospectives). This guide does not cover installing adapters or
configuring models. See [the chat-surface hub](Huddle.EngineeringGuide.md) for that.

## Contents at a glance

| If you want to… | Read |
| --- | --- |
| Get started with your own copy | [Install](#install) |
| Understand how Teammates, Teams, Tasks and memory relate | [The data model](#the-data-model) |
| Free a long-running Teammate's memory without restarting it | [Freeing a Teammate's memory with `/compact`](#freeing-a-teammates-memory-with-compact) |
| Know which file a change lands in | [Where your data lives](#where-your-data-lives) and [What happens when…](#what-happens-when-reference) |
| Add or change a Teammate | [Working with Teammates](#working-with-teammates) |
| Set up Teams and Projects | [Organising Teams and Projects](#organising-teams-and-projects) |
| Hire a Team from a type, or set its process and gates | [Hiring a Team from a type](#hiring-a-team-from-a-type) and [Process and gates](#process-and-gates) |
| Handle asks between Teams, or Team retrospectives | [Requests between Teams](#requests-between-teams) and [Retrospectives](#retrospectives) |
| Chat with one or several Teammates | [Chatting in Rooms](#chatting-in-rooms) |
| Chat inside a Team, or find its Team Room | [Team chats](#team-chats) |
| Track work | [Tracking work with Tasks](#tracking-work-with-tasks) |
| Share notes and documents | [Keeping notes in the Library](#keeping-notes-in-the-library) |
| Teach Teammates a procedure | [Giving Teammates Skills](#giving-teammates-skills) |

## Install

Get Agency.Huddle running on your machine with one command. No admin rights are needed, and
nothing has to be installed first.

**Windows:** open PowerShell (a normal window) and run:

```powershell
irm https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.ps1 | iex
```

**macOS (Apple Silicon or Intel):** open Terminal and run:

```sh
curl -fsSL https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.sh | sh
```

The installer checks your machine first and tells you plainly if something is missing. It then
downloads the app, sets it up in your own folders and starts it in your browser. Running the
same command again is safe: it only changes what is out of date.

**Signing in to Claude.** If you are not signed in yet, the installer prints a command to run
once. It opens your browser:

```sh
claude auth login
```

**Starting it later.** On Windows use the Start menu entry `Agency Huddle`, or type `huddle`
in a new PowerShell window. On macOS open `Agency Huddle` from your Applications folder in your
home folder, or type `huddle` in Terminal.

**Stopping it.** Run `huddle stop`. (Closing the browser tab does not stop the app; on Windows you can
also close the console window it opened.)

**Upgrading.** Run the install command again. Your rooms and settings are kept.

**Uninstalling.** Your data is kept unless you ask for it to be removed.

```powershell
$env:HUDDLE_UNINSTALL='1'; irm https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.ps1 | iex
```

```sh
curl -fsSL https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.sh | sh -s -- --uninstall
```

Add `$env:HUDDLE_PURGE_DATA='1'` (Windows) or `--purge-data` (macOS) as well to delete your data
too.

For the options, troubleshooting and how releases are made, see
[Installer](engineering/installer.md).


## A quick tour of the screen

The left sidebar is the whole app. From top to bottom:

| Sidebar group | What it holds |
| --- | --- |
| **Needs you** | A card above Chats, drawn only while a Room is waiting on you. See [Needs you](#needs-you). |
| **Chats** | Your root Rooms, plus **New chat**. A Team's chats are not listed here; see [Team chats](#team-chats). |
| **Teams** | One row per Team. Expand it to see its Team Room, its chats and its Projects. **New team** sits at the bottom. The `⋯` menu on a Team row has **New chat** and **New project**. |
| **Teammates** | The page listing every Teammate, grouped by Team. |
| **Tasks** | Your saved Views of Tasks: **All Tasks**, **My Tasks**, any you saved, and **New view**. |
| **Library** | One row per Library root (**Teams**, **Teammates**, and any folders you pinned), plus **Add Folder**. |
| Bottom | **Archived chats** and **Settings**. |

Every page has its own address, so you can bookmark it:

| Address | Page |
| --- | --- |
| `/rooms/{roomId}` | A chat Room |
| `/teammates` | Teammates |
| `/teams/{Team}`, `/teams/{Team}/files`, `/teams/{Team}/tasks` | A Team page and its tabs |
| `/teams/{Team}/projects/{Project}` | A Project page |
| `/tasks/{viewId}` | A saved Task View |
| `/tasks/item/{TaskId}` | One Task, opened over your last View, for example `/tasks/item/BUSI-0001` |
| `/library?scopeRoot=teams&scopePath=` | The Library, scoped to a root |
| `/settings/{tab}` | Settings: Prompts, Appearance, Personas, Skills, Library |

### Needs you

When several Rooms are running, **Needs you** at the top of the sidebar lists the ones waiting on you, longest wait first. A Room is listed when:

- a Teammate **mentions you** in a Message you have not answered (the row shows who and when);
- a Teammate has asked you a **Question** or opened a **Form** that is still waiting;
- the Room has used up its **Budget** and is paused until you speak.

Each row shows an icon for every reason, the Room's name, and who asked and when. Click it to open the Room.

A Room leaves the list when you **post in it**, or when you press the ✕ on its row. Opening a Room does not clear it, so you can read a Room and come back to it later. ✕ hides the Room until something new makes it wait again; it does not answer the Question or Form behind it. Pressing **Continue** on a paused Room clears only the paused reason. When nothing is waiting, the card is not drawn.

The list lives in memory. After a restart, Rooms with an unanswered mention (within the last 50 Messages) are listed again; a Question or Form that was waiting is gone, and so is any dismissal.

### Tabs

Everything you open from the left menu opens in a tab, in a strip above the page.
The Library is one tab like any other.

- **One click** on a menu item opens a *preview* tab. Its title is in italics, and
  the next menu item you click replaces it, so browsing does not pile up tabs.
- **Keep a tab** by double-clicking its menu item or the tab itself, or by typing in
  a message box on that tab. The title is no longer in italics, and the next click
  opens beside it.
- **Close a tab** with its **×**, by middle-clicking it, or with **Alt+W**. If you
  close the last tab, the page shows "No pages are open". **Alt+Page Down** and
  **Alt+Page Up** move to the next and previous tab. (Ctrl+W and Ctrl+Tab belong to
  the browser, which does not let a page take them.)
- **Rooms, Teams and Projects:** a Room's tab takes the Room's name when you rename
  the Room, and closes when the Room is archived or deleted. A Team's or Project's
  tab closes when that Team or Project is removed.
- **Half-written messages:** the text you have typed but not sent is kept for each
  Room while you switch between tabs, and it is still there after you reload the page.

**Right-click a tab**, or hover it and press the small chevron that appears beside its **×**, for its menu (hovering also shows quick Move Left and Move Right buttons when those apply):

- **Pin Tab** keeps the tab at the start of the strip as just its icon, with no ×
  (hover for its name). **Unpin Tab** puts it back.
- **Move Right** and **Move Left** move the tab to the other side of a split. The first
  time, they open a second pane beside the first, so you can see two pages at once. The
  item is greyed out when there is nowhere to move the tab, for example when it is the
  only tab or the window already holds two panes. A moved tab is kept.
- **Close** closes the tab.

With two panes, the one you last clicked or tabbed into is the one the address bar shows,
and what you open from the menu opens there. The other pane keeps its page exactly as you
left it. A page that is already open in one pane is never opened a second time in the
other: opening it takes you to it. Closing the last tab in a pane closes the pane.

Your open tabs and panes come back after you reload the page or restart the app, in the
same order, with the tab for the page you are on active. They are remembered by the
browser, so another browser starts with its own. Each tab is at least a fixed width, so with many open the strip scrolls sideways.

### Global Search

The search box sits in the Top Bar, above the left menu and every page.

- **Open the box** with `Ctrl+K` from anywhere, with `/` when no text field has focus, or by
  clicking it. `Esc` closes it and returns you to where you were.
- **Narrow the search** with the chips under the box: **All**, **Teams**, **Teammates**,
  **Library**, **Tasks** and **Rooms**. Picking a chip is the same as typing its prefix at the
  start of the box: `team:`, `teammate:`, `lib:`, `task:` or `room:`. `Alt+1` to `Alt+6` pick
  them from the keyboard, and `Ctrl+Left` and `Ctrl+Right` move between them.
- **A search starts at 3 characters**, after any prefix. `task:fi` waits; `task:fix` searches.
  Matching ignores case and looks for the exact phrase you typed.
- **Results come in groups:** Teams, Teammates, Library, Tasks and Rooms. With All, each group
  shows up to five results. With one chip picked, that group shows up to 50. Library matches
  its contents as well as its names, for entries up to 1 MB. Closed Tasks and Archived Rooms come
  last, dimmed.
- **Open a result** with `Enter` in the current Tab, or `Ctrl+Enter` to open it in a new Tab that
  stays. A Teammate opens its card. A Room opens at the matching Message, which is highlighted for
  a moment.
- **Every search reads again.** Global Search keeps no index of its own, so a new Message or file
  shows up on your next search, and nothing is kept after it. `Results may be incomplete.` means a
  limit was reached: narrow the search or pick one chip.

## The data model

Huddle has a small number of things, and almost all of them are plain files you
can open in any editor. This section names each one and explains how they
connect. Later sections explain how to work with them.

### The things in Huddle

| Entity | What it is | Stored as |
| --- | --- | --- |
| **You (the Human)** | The person using the app. Your display name is `Team:HumanName` (default `You`). You are in every Room. | Config, and a row in `team.db` |
| **Teammate** (also called a **Persona**) | One AI Teammate: its name, job title, @alias, Teams, Skills, and its instructions. | `Teammates/<Name>/<Name>.md` |
| **Agent** | A Teammate that is running and online in chat. There is one Agent per Teammate file. | A row in `team.db`, plus a running adapter process |
| **Team** | A named group of Teammates. Its membership is a label in each member's `teams:` list; a Team set up from a type also has a manager, a workspace and a process. | A label in each member's `teams:` list, plus a folder `Teams/<Team>/` holding `_team.md` (and `Process.md`, once set) |
| **Project** | A piece of work inside one Team. | A sub-folder `Teams/<Team>/<Project>/` |
| **Room** | A chat conversation between you and one or more Agents. | Rows in `team.db` |
| **Message** | One chat message, in one Room. | One line in `rooms/<roomId>.jsonl` |
| **Room Session** | A Teammate's private working conversation for one Room. | Only a resume pointer, in `room-sessions/<Name>.json` |
| **Task** | A unit of work filed under a Team or Project, optionally assigned to someone. | `Teams/<Team>/[<Project>/]_tasks/<ID>.md` |
| **View** | A saved list or board of Tasks with filters. | `views.json` |
| **Library item** | Any note, folder or file you browse in the Library. | The file itself, wherever it lives |
| **Personal memory** | What one Teammate remembers across chats. | `Teammates/<Name>/work/memory/*.md` |
| **Team Memory** | Facts shared by every member of a Team, or by a Project. | `Teams/<Team>/memory/*.md` and `Teams/<Team>/<Project>/memory/*.md` |
| **Skill** | A named, reusable set of instructions a Teammate can read on demand. | `Skills/<skill-name>/SKILL.md` plus extra `.md` files |

### How they relate

```mermaid
erDiagram
    TEAMMATE }o--o{ TEAM : "is a member of (teams: list)"
    TEAM ||--o{ PROJECT : "has sub-folders"
    TEAM ||--o{ TASK : "files tasks in _tasks/"
    PROJECT ||--o{ TASK : "files tasks in _tasks/"
    TASK }o--o| TEAMMATE : "assigned to"
    TASK }o--o| TASK : "parent / blocked by / duplicate of"
    TEAMMATE }o--o{ SKILL : "holds (skills: list)"
    ROOM }o--|{ TEAMMATE : "has members"
    ROOM ||--o{ MESSAGE : "transcript"
    TEAMMATE ||--o{ ROOM_SESSION : "one per Room it works in"
    ROOM ||--o{ ROOM_SESSION : ""
    TEAMMATE ||--|| PERSONAL_MEMORY : "work/memory/"
    TEAM ||--|| TEAM_MEMORY : "memory/"
    PROJECT ||--o| TEAM_MEMORY : "memory/"
```

The relationships that matter most:

- **A Teammate can belong to many Teams.** Membership is written on the
  Teammate, not on the Team. A Teammate file with
  `teams: ['Business', 'Household']` is a member of both, and appears under both
  on the Teammates page. A Teammate with no `teams:` line appears under **No
  team**.
- **A Team is a label plus a folder.** The list of Teams is every label used by
  any Teammate, plus every folder directly under `Teams/`. A folder with no
  members still shows, as **No members**. A label with no folder gets one
  created automatically. A Team's folder may also hold `_team.md`, which names its
  team type, manager and workspace, and `Process.md`, which says how it works.
- **A Project belongs to exactly one Team** and has no members of its own. The
  Team's members work on it. Any ordinary sub-folder of a Team folder is a
  Project, except `memory` and names starting with `_` or `.`.
- **A Task belongs to one Team, and optionally one Project.** Which one is
  decided by the folder the file sits in, not by a field. A Task has at most
  **one assignee**: a Teammate or you. A Task can have one parent Task
  (sub-tasks), a list of Tasks that block it, and can be marked a duplicate of
  another.
- **A Room always includes you**, plus one or more Agents. A Message belongs to
  exactly one Room.
- **A Teammate keeps a separate conversation per Room**: a Room Session. What it
  said in one Room is not visible in another. Only memory, Team Memory, Tasks and
  file changes cross between Rooms.
- **A Skill is attached to Teammates only**, by name, through the Teammate's
  `skills:` list. One Skill can be held by any number of Teammates. Skills are
  not attached to Teams, Projects or Rooms.

### Example: one Teammate file

A Teammate is a Markdown file with frontmatter. Everything below the frontmatter
is the Teammate's private instructions.

```markdown
---
name: 'Nova'
title: 'Coordinator'
alias: 'nova'
teams: ['Business', 'Household']
skills: ['team-building']
---
You are Nova. You keep the team's Tasks moving and summarise progress.
```

| Field | Required | Meaning |
| --- | --- | --- |
| `name` | Yes | The Teammate's identity. Must match the folder and file name. |
| `title` | Yes | Job title, shown on cards and to other Teammates. |
| `alias` | Yes | Short handle for mentions, as in `@nova`. |
| `teams` | No | The Teams this Teammate belongs to. |
| `skills` | No | Skills this Teammate may read. |
| `adapter` | No | Which AI adapter runs it. |
| `watches` | No | Extra folders it is told about when files change. |

**Model**, **Effort** and **Work mode** are not in the file. They are stored in
`team.db`, so you can change them without touching the Markdown. The **avatar**
is stored in `avatars.json`.

## Where your data lives

Everything Huddle keeps is under one **data root**, set by the configuration key
`Team:DataDir`. The default is `App_Data`, relative to where Huddle runs. In a
development checkout that is `src/Huddle.App/App_Data`.

```text
App_Data/
├── Teammates/                     Team:Acp:TeammatesDir
│   └── Nova/
│       ├── Nova.md                the Teammate definition
│       └── work/                  Nova's working folder (Team:Acp:WorkDir)
│           └── memory/            Nova's personal memory
├── Teams/                         Team:Teams:Dir
│   └── Business/                  a Team
│       ├── memory/                Team Memory
│       ├── _tasks/                open Tasks for the Team
│       │   ├── BUSI-0001.md
│       │   └── _closed/           closed Tasks
│       └── Website/               a Project
│           ├── memory/            Project memory
│           └── _tasks/
├── Skills/                        Team:Acp:SkillsDir; your own and overridden Skills
├── rooms/<roomId>.jsonl           one chat transcript per Room
├── room-sessions/<Name>.json      where each Teammate can resume each Room
├── file-state/<Name>.json         what each Teammate last saw of its folders
├── team.db                        SQLite: users, agents, rooms, members, models, task ID counters
├── views.json                     saved Task Views
├── library-roots.json             pinned Library folders (created on first change)
├── prompts.json                   prompt overrides from Settings
├── appearance.json                theme and accent colour
├── avatars.json, avatars/         avatars
└── logs/                          log files, when file logging is on
```

> [!WARNING]
> Some folders under the data root are managed by the app. Do not rename or move
> `rooms/`, `room-sessions/`, `file-state/`, `avatars/`, `logs/` or `team.db`.
> The Library refuses to open them, and hand edits can lose chat history.

## Working with Teammates

Open **Teammates** in the sidebar. The page lists every Teammate, grouped by
Team, with a **Team** filter at the top. A Teammate in two Teams is listed under
both. Each card shows a status of Offline, Starting, Online or Degraded.

Each Teammate has its own card with **Message**, **Edit**, **Open**,
**Restart** and **Remove**. With no Teammates the page says "No Teammates yet.
Choose New teammate."

### Adding a Teammate

1. Select **New teammate**.
2. **Name** starts with a random first name that no Teammate has yet. Keep it,
   edit it, or select the reset button (the circular arrow at the right of the
   box) for another one. Then fill in **Title** and **Alias**. Optionally add
   **Teams** as a comma-separated list, choose **Skills**, **Model**,
   **Effort**, **Work mode** and an avatar.
3. Write the Teammate's instructions in the text area.
4. Select **Add teammate**.

**In the background:**

- Huddle composes the frontmatter and writes
  `Teammates/<Name>/<Name>.md`.
- Model, Effort and Work mode are written to `team.db`.
- The Teammate starts as an Agent and appears in chat. Its `work/` and
  `work/memory/` folders are created.
- Each Team label you entered that has no folder yet gets
  `Teams/<Team>/` created.

Names and aliases are at most 64 characters, start with a letter or digit, and
may contain letters, digits, `_`, `-` and single spaces. No dots. Names and
aliases must be unique across all Teammates. How many Teammates a Team may hold,
and how many run at once, is limited; see [Hire limits](#hire-limits).

The built-in **Chief of Staff** is the first Teammate you meet. It helps you put
a team together: it proposes Teammates, and nothing is created until you select
**Approve**. It has **Reset to default** instead of **Remove**; the card asks
**Reset to default?** in place, and **Reset** confirms it.

### Editing a Teammate

Select **Edit** on the card, change the fields or the instructions, and select
**Save**.

**In the background:** Huddle rewrites `<Name>.md` in place. **Saving restarts
the Teammate and clears what it remembers of every conversation.** Its Rooms and
their transcripts stay. On its next turn in each Room it catches up by reading
up to the last 20 messages of that Room. Its personal memory files in
`work/memory/` are kept.

A restart also happens when you change Model, Effort, Work mode, Adapter, Skills
or Teams. Changing the avatar does not restart anything.

> [!NOTE]
> The Edit card has no **Teams** field. To change a Teammate's Teams after
> creating it, use the Team page's **Members** tab, or edit the `teams:` line in
> the text.

### Changing an avatar

Select the avatar at the top of a Teammate's card. It does this in every mode:
viewing, editing and creating. A small pencil appears on it when you point at it
or tab to it. A window titled **Avatar** opens with three choices:

- **Initials of the name** shows the letters Huddle makes from the Name.
- **A short label** shows up to three characters you type instead.
- **An image** shows a picture. **Choose an image** uploads a PNG, JPEG or WebP
  file of up to 512 KB, and **Choose from library** (see the next section) picks
  one of the ready-made pictures.

The round picture at the top of the window previews your choice as you make it.
Initials and a label sit on a **Background** colour. Choose it by pressing a swatch
in the palette in the window; the swatch for the current colour is highlighted. A
colour can be changed to another swatch but not cleared, so once a Teammate has a
colour it keeps one. An image covers the background, so the palette is hidden while
**An image** is selected. A colour you chose earlier is kept, and is there again if
you go back to initials or a label.

A new Teammate starts with a colour of its own: when you open **New teammate**,
Huddle picks a random swatch from the same palette, so a new card is not a row of
identical circles. It is written with the Teammate when you select **Add
teammate**, and you can change it in the window like any other. Teammates that
already exist, and any created some other way, keep the theme's colour until you
press a swatch.

What the window's last button does depends on where you opened it:

- **Creating or editing a Teammate:** it reads **Apply**. The new avatar shows on
  the card at once, but nothing is written until you select **Add teammate** or
  **Save** on the card. **Cancel** on the card throws it away.
- **Viewing a Teammate:** the card has no Save of its own, so the button reads
  **Save** and the avatar is written as soon as you select it.

**Cancel**, the close button and the **Esc** key leave the avatar as it was.

### Choosing an avatar from the library

Choosing **An image** in the **Avatar** window shows a second button,
**Choose from library**, beside the upload button. Press it to open a window of
pictures to pick from.

- **Narrow the pictures.** Press a skin tone (**All**, **Light**, **Tan** or
  **Dark**). Type a word in the search box, such as a profession or a tag like
  `glasses`. Press a profession or a tag to select it. Selections combine: two
  professions show either one, and two tags show only pictures with both.
- **Selected chips move to the left.** Each one has an **X**. Press the X to
  turn it off, and it returns to its place. **Clear all** turns everything off.
- **Pick a picture.** Click it, and the library window closes. The **Avatar**
  window shows the picture and says **New image selected (not yet saved)**.
  Select **Apply** (or **Save**, when you opened it from a viewed Teammate) to
  keep it, or **Cancel** to keep your old avatar. On your own avatar in
  **Settings → Appearance** the picture is saved at once.

The library comes with Huddle: its 324 pictures are in the repository, in
`src/Huddle.App/AvatarLibrary`, and are used as they are, with nothing to set up.
To use a library of your own instead, set `Team:AvatarLibrary:Dir` to its folder
(it needs a `master.json` and the pictures, like the bundled one). If you set it
to a folder that does not exist there is no library at all and no button, which
is how to switch the library off; uploading an image works either way. A
picked picture is copied, so the Teammate keeps it even if the library is later
moved or deleted.

### Choosing a Work mode

A **Work mode** is how much the Teammate's agent may do before it must ask first.
Select **Edit** on the card and pick one in **Work mode**, directly under
**Effort**. The choices come from the Teammate's Adapter, so they are the
Adapter's own names and the text under the select describes the one you picked.

For the Claude adapter the choices are:

| Choice | What it does |
| --- | --- |
| **Use the agent's default** | Huddle sends nothing and the agent starts in its own mode, which is Manual. |
| **Manual** | The agent asks before it makes changes. |
| **Accept edits** | The agent edits files without asking. |

**Plan**, **Auto** and **Bypass permissions** are not offered. **Plan** is hidden
because a Teammate in it ends its turn without posting a reply. **Auto** hands
permission decisions to the model and changes with the Model. **Bypass
permissions** removes every prompt. An operator who wants them back changes
`Team:Acp:HiddenModes`. A mode on the hidden list is never sent, even if one is
already stored for a Teammate.

**In the background:**

- The mode is stored in `team.db`, not in `<Name>.md`.
- It is applied every time the Teammate starts or resumes, after its Model and
  Effort.
- **Saving a different Work mode restarts the Teammate and clears what it
  remembers**, as a Model change does.
- Changing the **Adapter** resets Work mode to the agent's default and the card
  says so. Each Adapter advertises its own modes, so pick one again if you want
  one.
- An Adapter that advertises no modes shows no Work mode select.

> [!NOTE]
> Work mode changes what the agent asks, not who answers. Huddle still approves
> the agent's permission requests on its own; nothing here asks you before a tool
> runs. One thing is refused whatever the mode: a tool call that writes a file
> inside the agent's own `~/.claude` folder. A shell command that redirects into
> it is not caught.

### Renaming a Teammate

Change **Name** on the Edit card and save. Huddle warns you first:
messages already posted keep the old name, because the transcript records what
was said at the time.

**In the background:**

- The folder `Teammates/<Old>/` moves to `Teammates/<New>/` and the definition
  file is renamed with it.
- The avatar, `file-state/`, `room-sessions/` entries and the `team.db` row are
  renamed.
- Every Task whose `creator:` or `assignee:` was the old name is rewritten, open
  and closed, and saved Task Views that filtered on it are updated. No Change
  log line is added to those Tasks.
- Rooms that were auto-named after the Teammate are renamed. Rooms you named
  yourself are left alone.

### Removing a Teammate

Select **Remove**. The card asks **Remove {name}?** in place; select **Remove**
again to confirm, or **Cancel** to keep the Teammate.

**In the background:** only `<Name>.md` is deleted, along with its Model, Effort,
Work mode, avatar and resume entries. The Teammate goes offline. Its Rooms, their
transcripts and its `work/` folder stay on disk. Tasks still assigned to it keep
the name and no longer wake anyone.

### Restarting a Teammate

**Restart** deletes `room-sessions/<Name>.json` and restarts the Teammate. Every
Room then starts a fresh conversation, with catch-up from the transcript. Use it
when a Teammate seems confused.

### Editing the file by hand

You can open a Teammate's file with **Open** or through the Library and edit it
in any editor. Huddle watches the `Teammates/` folder and picks up a saved change
within half a second. It behaves exactly like an in-app edit, including the
restart. A file that fails validation is listed under **Files that didn't load**
on the Teammates page, with the reason.

### Packs, setup warnings and the Process step

A Teammate who was hired from a team type's role has a **Complete Setup** panel on
its card. It shows:

- **Role:** the role and version the Teammate was hired as.
- **Total spent:** the lasting cost of the Teammate, one line per currency, kept
  across restarts. This is different from **Spent since start**, which counts
  only the current run.
- **Packs:** named groups of Skills, such as **MSBuild Authoring**. Select a
  pack's chip to add its Skills to that Teammate, or select it again to take
  them off. A Skill the role itself carries stays when you remove a pack. Adding
  a pack also adds a short phrase to the Teammate's `consult_when` line, so the
  manager knows to route that kind of work to it.
- Each Skill's provenance line (see [Importing a Skill](#importing-a-skill)).

The panel shows a warning, as a full sentence, in two places:

- beside the **Model** choice, when the Teammate runs on the same vendor as a
  member whose role it must differ from (the Reviewer and QA, for example, against
  the Developer), such as "Reviewer Jane runs on the same vendor as Developer
  Joe.";
- beneath the packs, when the Teammate has more Skills than is advised, or when
  two or more of its Skills come from the same catalog sub-category, such as
  "msbuild-a and msbuild-b are both in Build tooling.".

A warning does not stop anything. It tells you where the Team's setup may be
weaker than you meant. A Teammate you added by hand, with no role, shows none of
this.

A manager's card also has a **Process** step, with **Use this process** for the
process its team type offers, or **Start blank**. Either one writes the Team's
`Process.md` once. An existing `Process.md` is never overwritten: the step says
"This Team already has a process. Edit it on the Process tab." See
[Process and gates](#process-and-gates).

> **Example (Scenario 2).** Emre hires a second Developer, Jane, and selects the
> MSBuild Authoring chip on her card. Her card now lists the pack's Skills, and
> if two of them share a sub-category, the card says so.

## Organising Teams and Projects

### Creating a Team

Select **New team** at the bottom of the Teams group, type a name, and select
**Create**. This makes an empty Team folder. To start a Team with a team type and
its hired members, use **New team from a type** instead; see
[Hiring a Team from a type](#hiring-a-team-from-a-type).

**In the background:** Huddle creates the folder `Teams/<Team>/`. Nothing else
is written and no Teammate restarts. The new Team shows **No members** until you
add someone.

Team names cannot contain `,` `;` `[` `]`, cannot start with `_` or `.`, and
must follow the Library file-name rules (no `/ \ : < > " | ? *`, no trailing dot
or space, no Windows device names like `CON`). Names are unique regardless of
case.

### Adding and removing members

Open the Team, then the **Members** tab.

- **Add member** opens a picker of Teammates not yet in the Team. Huddle
  tells you that adding restarts the Teammate.
- **Remove from Team** in a row's menu asks you to confirm, with the same
  warning.

**In the background:** Huddle rewrites only the `teams:` line in that
Teammate's `<Name>.md`, then saves. The Teammate restarts and loses its
conversation memory, and on restart it is told about the new Team's memory.
Removing the last member does **not** delete the Team folder. The Team stays,
marked **No members**.

> [!CAUTION]
> Close any open Teammate Edit card before adding or removing members. A card
> saves its whole text, so saving a card that was open during the membership
> change puts the old `teams:` line back.

### Creating a Project

On a Team row in the sidebar, open `⋯` and select **New project**. Or, in the
Team's **Files** tab, open a Team folder's menu and select **New Project**.

**In the background:** Huddle creates `Teams/<Team>/<Project>/`. `memory` is
reserved, and so are names starting with `_` or `.`.

Any folder you create directly inside a Team folder, from the Library or your
file manager, becomes a Project too. There is no separate Project record.

### Renaming or deleting Teams and Projects

Huddle does not offer this, and the Library refuses to rename, move or delete
Team and Project folders. To retire a Team, remove it from every member. The
folder and its Tasks remain on disk.

### The Team and Project pages

Select a Team or Project in the sidebar.

| Tab | On a Team page | On a Project page |
| --- | --- | --- |
| **Members** | Members, with status, title and alias; search; Add and Remove. Opening a row opens the Teammate card. | Not shown. Projects are worked by the Team. |
| **Files** | The Library, scoped to `Teams/<Team>/`, with **New note** and a file-name search. | Scoped to `Teams/<Team>/<Project>/`. |
| **Tasks** | The Team's Tasks as a List or a Board (one swimlane per Project), with the same toolbar as the Tasks page. **New task** fills in the Team. | The same, for the Project. **New task** fills in the Team and Project. |

Changes you make with the Tasks tab toolbar (filter, group, sort, search) last for
the current visit only, and always stay inside the Team or Project.

The Files tab is hidden when the Library is turned off (`Team:Library:Enabled`).
The Tasks tab is hidden when Tasks are turned off (`Team:Tasks:Enabled`).

A Team whose `_team.md` names a process also has a **Process** tab, which shows its
`Process.md` with an **Edit** button and a switch for each gate. See
[Process and gates](#process-and-gates).

Under a Team's title (not a Project's), the header has two more things:

- **Duplicate** copies the Team's roster into a new Team. A small dialog asks for
  the **New team name** and a **Workspace folder** (left blank, it means none,
  which is refused when the type requires one). The new Team has
  the same team type, and each member is hired afresh under a new name; no
  memory, Library file or Task is copied. A member whose role no longer exists
  in the type, or who was added without a role, is listed as skipped before you
  select **Open Team**. A title that is taken, or a workspace folder that does
  not exist, is refused in the dialog.
- **Spent** shows what the Team's members have cost in total, one amount per
  currency. It stays blank until something was spent, and it survives restarts.

### Building a team with the Chief of Staff

**Build a team** (`/teams/build`) walks you through making a Team in four steps:
**Team type**, **Team details**, **Review** and **Approve**. Only the first two exist
so far. Step 1 shows a card for each team type: its name, what it does in a line, how many
roles it has besides the manager, and **You have one** when you already run a Team of
that type. The cards sit in two rows; scroll to the right for the rest.

Select a card to choose that type; step 1 then shows a check and the type's name, and
**Team details** opens under it. There you set the **Team name** (it starts as the
type's name; a name an existing Team has is refused), **What it's for** (it starts as
the type's description) and the **Library folder**. Every type has the same field: it
starts as the Teams folder under your data folder, you may clear it, and a folder you
type must exist. The folder icon at the end of that field opens a folder picker: walk
the folders, or type a path and press Enter, then select **Select**. Nothing is
created yet; the later steps do that.

**Blank Team**, the first card, is for when you want to decide everything: it has no
roles and no process, and its name and purpose start empty.
Select it again to clear the choice. The choice stays in the address (`?type=`), so
it is still there when you come back to the tab. Open the page with **Build a team**,
the last link of the Teams group in the sidebar.

### Hiring a Team from a type

A team type is a template for a kind of Team: its roles, the Teammates that fill
them, its process and its workspace. The built-in types are **Development Team**
and **Household**. Your own types live in the `TeamTypes` folder under your data
folder, and a folder there with the same id replaces the built-in one.

The **Settings** page's **Team Types** tab lists every type Huddle can serve: its
name, id, version, whether it is **Built-in** or **Yours**, and how many roles it
has. **Reload** rereads the built-in types and your `TeamTypes` folder. Under
**Problems**, each type Huddle rejected is listed with its file and the reason,
so you can see why a folder you wrote never reached the Team type list.

To make a Team from a type:

1. Select **New team from a type** at the bottom of the Teams group. This opens
   the **New Team** screen (`/teams/new`).
2. Choose a **Team type** and type a **Team name**. If the type has a workspace,
   fill in its folder: a Development Team's workspace is a repository and is
   required; a Household's is a home folder and is optional. A title that is
   taken, or a folder that does not exist, is refused on the spot.
3. Select **Create**. Huddle creates `Teams/<Team>/`, writes its `_team.md`
   (which names the type, the manager and the workspace), and hires the type's
   manager at once if its card is Ready. The Team's setup screen
   (`/teams/<Team>/setup`) then opens. If the manager's card needs setup, the
   Team is still created; the setup screen lists the reasons and offers **Hire
   manager** once they are fixed. If hiring the manager is refused, the Team is
   kept and **Open setup** takes you to it.
4. In the **hiring pool**, the left side has one card per role: its tier, what it
   covers and the name the next hire would get. Select a card's **+** to add that
   role to the Team on the right. Two chip clouds above the cards narrow them:
   **Team types** (a job shows if it suits any selected type; it opens on this
   Team's own type) and **Tags and skills** (a job must carry every selected chip).
   Clear the type chip to browse every job in the pool; a job this type does not
   list can still be hired, with default settings. **Clear all** resets both clouds.
   There are no presets and no drag and drop. The manager is already there and is
   locked: it cannot be removed. A role that has reached its limit is greyed and
   says **Limit reached**. **Custom role** adds a role of your own; see
   [Custom roles](#custom-roles).
5. Each card on the right shows **Ready** when every default resolves, or **Needs
   setup** with the reasons, such as "no logged-in adapter for tier balanced" or
   "connector email:read not connected". A card that needs a connector says
   "Connect <connector> to finish setup" and where to list it; see
   [Household teams](#household-teams). Select **I connected it** to check
   again. Select the **x** on a card to take it off.
6. The **team check bar** shows one chip per coverage step of the type (filled
   when a hired role covers it), how many vendors and processes the Team would
   run, and each reason **Start Team** is disabled: a card that needs setup, a
   missing manager, or a required role that is not hired.
7. The **Process** step on the same screen lets you pick the process the team
   type offers and preview it, then **Use this process** or **Start blank**. If
   you skip it, **Start Team** gives the Team the type's default process when
   it has none.
8. Select **Start Team** once the bar has no blockers. Huddle installs the
   Skills the members need, hires and starts the manager first, waits until it is
   running, then hires the rest in order. The manager's first Turn arrives as a
   message from you in its direct Room, with the onboarding steps of the Team's
   process. That message is posted once per Team, never again after a restart. A
   failure stops the start with an error on the screen and tells you who already
   started; nobody is removed.

If a Team you open on the setup screen has no `_team.md`, the screen says
**This Team has no setup file.**

**Skills during a hire.** Hiring installs any Skill a member needs that is not
already on disk, by importing it from the catalog. If a Skill's review shows more
than a missing licence (scripts, shell commands, network calls), Huddle does not
install it on its own and the start stops with "Skills not installed: ...". Import
it yourself from **Settings › Skills**; see [Importing a Skill](#importing-a-skill).

### Custom roles

**Custom role** in the hiring pool opens a small dialog. **Start from** is
**Blank** or one of the type's roles, which pre-fills the **Title**,
**Consult when** and **Stance**. **Save** stores the role as a candidate in your
copy of the team type and stages it for the Team. A custom role is always saved
for next time: the **Save for next time** box is shown ticked and cannot be
cleared, and the role appears in the pool of every later Team of that type.

### Hire limits

Two limits apply whenever Teammates are hired, whether from the setup screen or by
approving a Chief of Staff proposal:

| Setting | Default | What it limits |
| --- | --- | --- |
| `Team:Acp:MaxTeammatesPerTeam` | `8` | Teammates in one Team. |
| `Team:Acp:MaxLiveProcesses` | `16` | Agent processes running at once, across every Team. |

A hire that would pass either is refused with a plain sentence, such as "Team
'Web Team' is full: 8 of 8 Teammates." or "16 of 16 agent processes are running, so
no more Teammates can be hired." A value of zero turns a limit off. The old
`Team:Acp:MaxTeammates` is retired: it is still read, so an old configuration
loads, but nothing enforces it.

### Models for a role

A role names a **tier** (strongest, balanced or fast), never a model. Huddle picks
an adapter that is available, preferring one that keeps a role on a different
vendor from the role it must differ from, and then looks for the model with
`Team:Teams:Tiers:<adapter>:<tier>` in your configuration. With no entry for that adapter and tier, the adapter's default model is used. A
mapped model the adapter does not list is ignored in the same way.

> **Example (Scenario 1).** Emre picks **Development Team**, names it **Web Team**,
> points it at his repository. Joe, the
> Development Manager, is hired first. Emre adds a Developer, a Reviewer and QA,
> with the Reviewer and QA on a different vendor from the Developer, and selects
> **Start Team**.

### Process and gates

A Team's **process** says how its work is done. It is the file `Process.md` in the
Team folder, which the Team's manager and members read. Its frontmatter names the
process's settings:

- `gates:` lists the gates the Team enforces, from the four below.
- `checkCommand:` names the command whose passing Evidence the merge gate looks for.
- `wip:` sets a limit on work in progress.
- `retro:` and `digest:` set when a retrospective and a digest run, such as
  `retro: { every: 10, unit: 'done-tasks' }`. See
  [Digests and retrospectives on a schedule](#digests-and-retrospectives-on-a-schedule).

A Team gets its `Process.md` from the **Process** step (on the setup screen, or on
the manager's card), from **Start Team**, or by writing the file yourself.
**Start blank** writes an empty outline; **Use this process** copies the type's
template. Neither overwrites an existing `Process.md`. The team types ship several
templates, such as a lightweight flow, spec-driven, sprints and maintenance for a
Development Team, and household basics for a Household.

### The Process tab

A Team whose `_team.md` names a process has a **Process** tab on its page. It shows
`Process.md` as a document, with an **Edit** button that opens the file in the
Library. Above the document there is one switch for each gate the process turns
on. Turning a switch off asks first, naming the gate and its risk (for example,
"A Task can then be marked Done with no verified Evidence."). Only **Turn off**
rewrites the `gates:` line; **Cancel** leaves the file alone and the switch on.
The tab only lists gates that are on; to turn one on, edit the file. A Team with
no `Process.md` says "This Team has no process yet."

### The four gates

| Gate | Refuses |
| --- | --- |
| `qa-evidence-before-done` | A Task moving to **Done** until a member whose role verifies (for example QA) has added an Evidence line with a command. |
| `checks-before-merge` | A `git merge` or `git push` by a Teammate in the Team until an Evidence line shows the Team's `checkCommand` ending with exit code 0. |
| `spec-approved-before-build` | A Task moving to **In Progress** until its parent spec Task is approved. |
| `failing-test-before-fix` | A bug Task moving to **Review** until its Evidence shows a failing run followed by a passing one. A Task is a bug when it has the `bug` label or is a bug Request. |

A Task in a Team with gates shows a line such as `Gates: qa-evidence-before-done`
in its panel.

**What you see when a move is refused.**

- In the **Task panel**, saving a status change that a gate refuses shows a
  warning that names the gate, with a **Move anyway** button. It asks you to
  confirm ("This skips the ... gate and is logged in the Change log"). Only you
  can override a gate, and the override is written to the Change log as `gate
  overridden by Human: <gate>`.
- On the **board**, a card you drag into a refused column snaps back and a message
  says "Could not move <Task>: " and the gate's reason. The card does not move,
  and there is no override on the board; open the Task panel instead.
- **Creating** a Task directly in **In Progress**, **Review** or **Done** is
  refused in the same way when a gate would refuse it. In the Task panel you get the
  same warning with a **Move anyway** button, and confirming it creates the Task and
  logs the override. A Teammate's `file_request` or create call cannot override.
- When a **Teammate** is refused, the refusal comes back to it as the result of its
  `update_task` or `close_task` call, and it cannot override.
- When a Teammate or you **edit a Task file by hand** to jump to a gated status,
  Huddle puts the status back, keeps the rest of your edit, and adds `outside edit
  refused by gate: <reason>` to the Change log. Evidence-only edits are kept.

**Evidence.** A Task file can have an `## Evidence` section above its Change log.
Each line is the time, the author, the command, and optionally the exit code and a
pointer to the tool-call log, separated by ` | `; a plain line of text is a note.
Teammates add lines with the `add_evidence` tool, and lines are only ever appended.
The Task panel does not have a separate Evidence view; you read it in the Task file
in the Library.

**Tool-call log.** Huddle keeps a lasting record of every tool call a Teammate
makes, one JSON line each, in `logs/tool-calls/<Teammate>/<date>.jsonl` in your data
folder. An Evidence line can point to a line of it, such as
`logs/tool-calls/Joe/2026-10-08.jsonl#L12`, so you can check that the command was run.

**How the merge gate works, and its limits.** When a Teammate asks to run a shell
command, Huddle reads it. If it is a `git merge` or `git push` and the Team has
`checks-before-merge` and a `checkCommand`, Huddle refuses it until Evidence on one
of the Team's open Tasks shows the latest run of that exact command passing. The
Teammate is told only that the request was denied; the reason is written to the log,
not shown to it. This is a guard against honest mistakes, not a sandbox. It only
works in work modes where the Adapter asks before running a command, and it does not
catch a git alias, `FOO=1 git push`, `sudo`, a command wrapped in `bash -c`, a script
that pushes, or a push from an IDE or another tool. Your own approval of a prompt is
never intercepted.

> **Example (Scenario 4).** The Team's process lists `failing-test-before-fix`, so a
> bug Task cannot move to Review until its Evidence shows the test failing and then
> passing. The retrospective in a later section is where a rule like this is
> proposed.

### Digests and retrospectives on a schedule

If `Process.md` has a `digest:` or `retro:` line, Huddle checks each Team once a
minute. A **digest** is time-based (`digest: { every: 1, unit: 'days' }`): when the
time is up, the Team's manager gets a message in its Room asking it to post the
digest. A **retro** runs after so many Done Tasks (`unit: 'done-tasks'`) or so many
days (`unit: 'days'`) and goes to the Team's Retro agent, or to the manager with a
note that no Retro agent is hired. The first time Huddle sees a Team it only starts
the count, so nothing fires at once. The last run is remembered across restarts.

### Wake-ups for managers

Besides the assignee, Huddle wakes the Team's **manager** about a Task change, unless
the manager made the change, is the assignee, or is you. When the last Task that was
blocking another is finished, Huddle wakes the blocked Task's assignee (or the
manager when it has none) once, however many blockers finished together. These wake-ups
use the same per-Task wake budget as any other.

### Sprint proposals

A manager who holds the `team-management` Skill can propose a Sprint with `propose_sprint`: a name and the
Team's Tasks that are not yet Done. A card appears in the manager's Room with the name and each
Task's id, title and assignee. **Approve** creates the Sprint and adds the Tasks;
**Decline** sends your typed reason back to the manager. Nothing is created until you
approve. Tasks that cannot be added are listed as skipped in the reply the manager
sees.

### The Chief of Staff roll-up

The Chief of Staff holds the `team-rollup` Skill. When a manager posts a digest or
a Team declines a Request, it is woken, summarises the Teams for you as decisions and
risks with links, and uses `list_teams` to include each Team's open Requests.

### Requests between Teams

One Team can ask another for a **bug**, a **feature**, **feedback** or a
**question**. Such an ask is a **Request**: a Task in the receiving Team's own
Tasks, which names the Team that filed it. The receiving Team's Tasks have a
**Requests** filter, and a Request's Task shows a chip such as
**Request · bug · from Web Team**.

- In the **Task panel**, a Request shows a chip such as **Request · bug · from Web
  Team**, a second chip with its triage state (**Needs triage**, **Needs info**,
  **Accepted** or **Declined**), and, once they exist, **Requested by**, **Decline
  reason** and **Confirmed by**.
- In the Task toolbar, the **Requests** chip filters the view to Requests only.
- A new Request lands in the receiving Team's **Backlog** as **Needs triage**.
  Its manager **accepts** it (it moves to **To Do**, optionally assigned),
  **declines** it with a reason (it moves to **Rejected**), or asks the requester
  for more information (**Needs info**, still in Backlog). A Request cannot leave
  Backlog any other way: an agent that tries is refused, and only you can move it
  from the Task panel with the usual override.
- Work then follows the Task statuses: To Do, In Progress, Review. When the
  receiving QA attaches evidence in Review the Request moves to **Done**.
- **Done is not closed.** The Request closes only when the requesting Team's QA
  confirms it was delivered as asked. An agent that tries to close a Request nobody
  confirmed, with `close_task` or `update_task`, is refused. You can still close one
  yourself.
- Four tools do this, each granted by a Skill the Teammate holds. The built-in
  `team-management` Skill grants all of them, and the manager of a Development Team
  or a Household carries it, as does the Development QA Engineer, so the QA can
  confirm a Request without any setup. Holding the Skill does not make the QA a
  manager: `file_request`, `triage_request` and `list_teams` still refuse a caller
  who is not the right manager, and so does `propose_sprint`. The tools are
  `file_request` (a manager files a bug, feature, feedback or question into another
  Team; the body needs the sections the kind asks for, such as **Steps to
  reproduce** and **Expected and actual** for a bug), `triage_request` (the
  receiving manager), `confirm_request` (the requesting Team's QA) and `list_teams`
  (every Team with its type, manager, what it owns and its open Requests).
- **Who is woken.** Filing a Request wakes the receiving manager. A needs-info,
  accept or decline wakes the requesting manager; a decline also wakes the Chief of
  Staff. Work and the QA's evidence wake the requesting manager; the
  confirmation wakes the receiving manager. One event wakes each person once, not
  once as the Request's assignee and again as a manager.

> **Example (Scenario 3).** The Web Developer hits a 500 from the API. Joe files a
> bug Request into the API Team's backlog with the steps and logs. The API Team's
> manager accepts it. The Request closes only after the Web Team's QA confirms the
> fix.

### Retrospectives

A retrospective runs on the cadence in `Process.md`: after every so many Done Tasks,
or every so many days (see
[Digests and retrospectives on a schedule](#digests-and-retrospectives-on-a-schedule)).
The Team's retro role, the Development Team's **Retro agent**, reads the Team's
records and proposes up to three changes. The Retro agent is in the hiring pool but
is not hired for you, so add it with its **+**. It needs the
built-in `retrospective` Skill, which the role carries.

**What it may read.** With `read_records` the Retro agent (or the Team's manager)
reads only its own Team: a compact bundle of the Team's records, one Task's Change
log and Evidence, or the last 200 messages of a Room. A Room is readable only if
the Team's members posted in it and everyone who posted there is a member of that
Team or you. A Room with an outsider in it is refused. A Teammate who is not a
member of the Team, or who lacks the Skill, is refused.

**What it proposes.** The proposals arrive as a card in the Retro agent's Room.
Each change shows its title, the file it targets and a diff Huddle computes from the
file as it is now. A change is an exact-text replacement in one file: `Process.md`,
`_team.md`, or one member's file. Select **Accept** to apply a change, or **Decline**
(type the reason in **Reason if you decline** first). Huddle refuses an Accept, and
changes nothing, when:

- the text to replace is not in the file exactly once;
- the edit would leave `_team.md` unreadable (it is checked before it is written);
- the edit would change a member's name (a retro change cannot rename a Teammate).

A refusal appears as a notice and the change stays on the card. An applied or
rejected change shows as a message in the Room, which also wakes the proposer.

**The register.** Each Team keeps a `Retro register.md` in its folder, and its
retrospective reports in `_retros/`. Every accepted change adds a row to the
register with the status **Done** and a note to verify it at the next retro. A row's
status is one of **Open** (found, nothing changed), **Done** (changed, not yet
checked), **Partial** (only partly worked), **Verified** (later evidence shows it
worked) or **Changed** (replaced by another change). At the next retro the Retro
agent checks Done and Partial rows with `mark_retro_row`; **Verified** needs
evidence from after the change, linking a Task, a `logs/...#L12` line or a
`_retros/` file.

> **Example (Scenario 4).** After a sprint, the Retro agent finds three Tasks sent
> back from Review for missing tests. It proposes one change to `Process.md`: write
> the failing test before the fix. You accept it.

### Household teams

Household is a second team type, with a Household Manager and a School Liaison.
A card whose connector is not yet connected shows **Needs setup** and "Connect
<connector> to finish setup", with the path of the file to edit. A connector counts
as connected when it is listed in `connectors.json` in your data folder, such as
`{"connected": ["email:read"]}`. Huddle rereads the file each time, so an edit
shows at once; a missing or malformed file means nothing is connected. Once the
connector is listed there, select **I connected it** on the card to check again.
Huddle does not connect anything itself. A Household has no workspace requirement,
and its default process runs a retro every 14 days.

> **Example (Scenario 5).** Emre picks **Household** and hires a School Liaison. The
> card shows **Needs setup** until he connects email. After that the liaison's card
> is Ready.

## Chatting in Rooms

### Starting a chat

Select **New chat** and pick one or more Teammates. Choosing a single Teammate
reopens your existing one-to-one Room with it, if there is one. In the Chats
group this makes a root Room. From a Team's menu it makes a chat inside that
Team; see [Team chats](#team-chats).

**In the background:** Huddle adds the Room and its members to `team.db`. The
Room gets a name derived from its members. Its transcript file is created in
`rooms/<roomId>.jsonl` when the first message is posted.

### Who answers

| Room | Behaviour |
| --- | --- |
| You and one Teammate | The Teammate answers every message. |
| You and two or more Teammates | A Teammate answers only when you mention it, as in `@nova`. |

Type `/invite @alias` in the message box, or use the **Invite teammate** button in the Room's header, to add a
Teammate to the current Room.

Teammates can reply to each other. To stop runaway conversations, a Room has a
**Budget**: after a number of Agent replies without you speaking, Agents pause
and Huddle asks whether to **Continue** or **Leave paused**. Your next message
resets it.

### What happens when you send a message

1. Huddle appends one line of JSON to `rooms/<roomId>.jsonl`. Transcripts are
   append-only and are never rewritten.
2. The Teammates who should answer each take a turn in **their own Room Session
   for this Room**. A Teammate runs one turn at a time across all its Rooms.
3. At the start of the turn, the Teammate is told about any files that changed
   in its folders since its last turn in this Room (see
   [Memory and file changes](#memory-and-file-changes)).
4. When the turn ends, Huddle saves the session's resume pointer to
   `room-sessions/<Name>.json`.

Room Sessions are opened on demand and closed after 30 minutes idle. At most 3
are open per Teammate at once. A closed session resumes where it left off if the
adapter supports it. Otherwise it starts fresh and catches up from the last 20
messages of the transcript.

### Referencing things in a Message

Type `@` in the message box to find a Teammate, a Room, a Team or a Library file. The
list opens at the start of a line, after a space or after an opening bracket, so an
email address such as `me@example.com` does not open it. Type `@room:` or `@lib:` to
narrow the list to Rooms or Library files. A bare `@` lists the Room's Members first.

Type `#` to find a Task. Picking a Task writes its plain id, such as `GROW-0002`, and
the id becomes a link in the sent Message.

What a pick writes:

| You pick | The Message gets |
| --- | --- |
| A Teammate | `@` and the Teammate's name. It is addressed exactly as if you had typed `@Name`. |
| A Task | The Task's id, shown as a link. |
| A Room | A link to the Room. A Room in a Team says which Team in its label. |
| A Team | A link to the Team's page. |
| A Library file | The file's path in a code span, the same text an attached file produces. |

The keys work the same for every kind of pick. While the list is open:

| Key | What it does |
| --- | --- |
| Up and Down | Move the highlight. It wraps from the last row to the first, and back. |
| Enter or Tab | Pick the highlighted row. |
| Enter | Never sends while the list is open, even when no row matches. |
| Esc | Closes the list and keeps the text you typed. It does not reopen for the same word. |

Typing keeps narrowing the list. Ctrl+Z after a pick undoes it in one step and brings back
the text you typed. The **Send** button always sends the text exactly as you typed it, even
while the list is open.

A Teammate who is not a Member of the Room is dimmed and marked **Not in this Room**.
Choose **/invite @** in the message box, or pick the Teammate after inviting it, to add
it to the Room first.

When a referenced item changes, the link shows the change. A **changed** chip means
the item was renamed or retitled since the Message was sent; the Message keeps the name
it had. An **(unavailable)** chip means the item no longer exists, for example a deleted
Room or a Library file that was renamed or moved. An archived Room still exists, so its
references show no chip at all. A Task id or a Library path that names nothing when you
send is not a reference: it stays plain text, with no chip.

Agents are told what a reference is when you send one, so they can read the item. To
stop that, set `Team:References:ExpandInPrompt` to `false`.

### Copying a Room's link

Use **Copy link** to put a Room's reference on the clipboard. It is the link button in
the Room's header, and *Copy link* in the `…` or right-click menu of a Room in the
sidebar, both for Chats and for a Team. Paste it into any message box to make the same
reference the picker writes. The Room opens when a reader selects it.

A plain address from the address bar opens the Room, but it is not a reference: it
shows as a link and nothing more.

### Watching a Teammate work

While a Teammate is answering, its reply shows its last six tool calls as rows.
Each row has a status (**Waiting**, **Running**, **Failed** or done), and shows
the file name and line when the call touches a file. When the call is an edit,
select the arrow on its row to see an **Edit preview** of the change in place.

A Teammate's card also shows **Spent since start**, one line per currency, when
its adapter reports a cost. It is a figure for this run, not a ledger.

> [!NOTE]
> Tool-call rows are not saved. They disappear when the reply is finished or
> the app restarts, and are never written to the transcript.

### How the conversation reads

- Your messages sit on the right in a bubble tinted with the accent colour; a Teammate's sit on a light card beside its avatar.
- A separator marks each day ("Today", "Yesterday", "Fri 2 Oct"), and consecutive messages from one Teammate share a single name and avatar.
- When a Task changes and wakes its assignee, the Room shows one slim line (such as "PLAT-0004 changed by You") instead of a message from you. Choose the line to see the changes.
- A Task link carries a small dot in its status colour, the same colour as the status on the List and Board.

### Attaching files

Click the paperclip beside Send, paste a screenshot, or drop files on the
composer. Each file is saved at once into the Library, and its chip shows above
the text box. Remove a chip with its close button before you send if you change
your mind; the saved file is then deleted and nothing is posted. When you send,
each file's path is added to your Message, so the Teammates who answer can open
it with their own tools.

- The limits are 25 MiB per file and 10 files per Message. A file over the size
  limit, or one more than the Message can carry, is refused with the reason in
  the red line under the text box and no chip is added.
- SVG files and programs (executables) are refused: the chip turns red and shows
  the reason. Huddle identifies a file by its contents, not its name.
- Send waits while a file is still uploading, and the text you typed stays in the
  box.
- The paperclip, paste and drop appear only when the Library is on.
- Files go to **Uploads** in the Library, one folder per Room. In a Team's chat
  they go to that Team's Library instead, under `Uploads`.
- Deleting a Room sends its uploaded files to the Recycle Bin. Archiving keeps
  them.
- The Message that carries a file's path is the only notice of it. File Changes
  does not report uploads, so a Teammate is not told of one by a file change.
- Pending chips do not survive a page reload. Files already saved stay in the
  Room's folder; remove them from the Library if you do not want them.

### Linking to Tasks and files

- Type `#` at the start of a word to pick a Task. A Task ID such as `BUSI-0001`
  in a message becomes a link to the Task.
- An absolute path to a Library file becomes a link that opens the file in the
  Library, in its own tab. The Teammates who answer are also given the list of those files,
  or, for adapters that cannot read files, the text of small ones.

### Freeing a Teammate's memory with `/compact`

A Teammate that has worked in a Room for a long time carries a long conversation.
**Compacting** summarises it, which frees space and usually speeds and cheapens
later turns, without the **Restart** that forgets the conversation entirely. To
compact one Teammate, **mention it and then write the command**:

```text
@nova /compact
```

You can add guidance after the name, which is passed to the Teammate as written:
`@nova /compact keep the decisions and the open questions`.

When it finishes, the Teammate posts one line in the Room, for example
`Compacted my conversation: 50,624 → 2,964 tokens in 9 s.` It still knows the
conversation, but as a summary: a detail you care about may not survive, so put
lasting facts in memory files.

- **The mention comes first, and is required.** This works in a Room of two as
  well. A bare `/compact` is refused with *Unknown command*, because a message
  that starts with a slash is one of Huddle's own commands (such as `/invite`).
- **Only you can run one.** The same text from another Teammate is an ordinary
  message.
- **Only listed commands work.** `/compact` is the one offered to Teammates on the
  standard Claude adapter. Anything else you write after a mention, such as
  `@nova /config`, is sent as ordinary text and runs nothing. A Teammate's
  card lists what it offers: open **Teammates**, select the Teammate, and look for
  lines like `/compact — Free up context by summarizing the conversation so far`.
  The line is absent while the Teammate is offline.
- **It costs a little.** A compaction is a model call, and Huddle does not show it
  in the **Spent since start** line. The Teammate's token budget may also count
  slightly fewer tokens afterwards than were really used.
- **Stop works.** Use **Stop** to cancel one in progress; a stopped command posts
  nothing.

If a Teammate has just started and you write the command straight away, it may be
treated as an ordinary message, because it has not yet told Huddle what it
offers. Try again a moment later.

**In the background:** your message is stored like any other, in
`rooms/<roomId>.jsonl`. Huddle sends only the command to the Teammate's adapter for
that turn, without the Room context, and keeps any messages the Teammate missed
for its next ordinary turn. The Teammate's own summary lives in its adapter
session; nothing new is written to disk for the compaction itself.

### Renaming, archiving and deleting Rooms

| Action | In the background |
| --- | --- |
| **Rename** (pencil icon beside the Room name) | Updates `team.db`. A name you type is kept even when members change. Not offered for a Team Room, whose name is its Team's. |
| **Archive** | Adds the Room to the archived list in `team.db`. It only hides the Room; it still works and can still be woken. Find it under **Archived chats**. |
| **Delete** | Removes the Room from `team.db` and **permanently deletes** `rooms/<roomId>.jsonl`. Asks for confirmation. |

> [!WARNING]
> Deleting a Room cannot be undone. The transcript file is deleted, not moved to
> the Recycle Bin. Archive the Room instead if you might want it later.

### Team chats

A Room is either a root Room or belongs to one Team. The two look the same in
the chat; they differ in where they are listed.

- **Chats** lists the root Rooms: the chats you start from the Chats group, and
  your one-to-one Rooms with Teammates.
- **A Team's chats** are listed under the Team in the sidebar, below its Team
  Room and above its Projects. Open the Team's `⋯` menu and select **New chat**.
  The panel is headed *New chat in Marketing* and lists that Team's members
  first, but you can still tick a Teammate from another Team.
- **The Team Room** is a chat named after the Team, holding you and every member
  of the Team. Every Team has one. It is created when the Team first has a
  member, and a Teammate who joins the Team later is invited into it; the
  Transcript records the join like any invitation. Leaving a Team does not take
  the Teammate out of the Team Room. The Team Room cannot be renamed, so it has
  no pencil icon.
- **Message** on a Teammate's card in the Team's **Members** tab opens that
  Team's own one-to-one with the Teammate, created if it does not exist. It is
  separate from the one-to-one under Chats, so the two conversations do not mix.
- **Digests and retrospectives** for a Team post in its Team Room, with a
  mention of the Teammate they are for, so it replies. If the Team Room is
  archived, they post in that Teammate's one-to-one under Chats instead. A Task
  whose original chat is gone wakes its assignee in a chat of that Team.
- **A Team that goes away** (no folder and no Teammate's label names it any more,
  which is what renaming its folder in Explorer does while no Teammate still
  carries the old name) has its chats archived, with their history kept. Find them under **Archived chats**. If you unarchive one of them
  while its Team is still gone, it is listed under Chats with *(Marketing)* after
  its name.

**In the background:** when a chat is created inside a Team, Huddle writes a
row to the `room_teams` table in `team.db`, saying which Team the Room belongs
to. A Team Room has its flag set there too. Huddle creates and fills Team Rooms
when the list of Teams changes, so a Team made by hand, or from a Teammate's
`teams:` line, gets one as well. A Team Room that you archive stays archived;
Huddle does not recreate it.

## Memory and file changes

Teammates do not keep conversations between restarts, so anything worth
remembering goes into files. There are two kinds of memory, both plain Markdown
folders.

| Memory | Folder | Who sees it |
| --- | --- | --- |
| **Personal memory** | `Teammates/<Name>/work/memory/` | That Teammate only. Created when the Teammate starts. |
| **Team Memory** | `Teams/<Team>/memory/` | Every member of the Team. |
| **Project memory** | `Teams/<Team>/<Project>/memory/` | Every member of the Team. |

The convention is **one file per fact, with the fact on the first line**. For
example, `Teams/Business/memory/invoice-day.md`:

```markdown
Invoices go out on the last working day of each month.

Use the template in Business/templates/invoice.md.
```

Teammates write memory with their own file tools. You can write it too: open the
Team's **Files** tab, select the `memory` folder, and select **New note**.
Huddle creates the `memory/` folder the first time something is written there.

**How Teammates learn about it:**

- **When a session opens**, each Teammate's system prompt includes an index of
  its personal memory and of the Team Memory of every Team it belongs to. Each
  entry is the file's first line, cut at 200 characters. The Team index lists at
  most 50 entries in total (`Team:Teams:MaxMemoryEntries`).
- **At the start of each turn**, a Teammate is told which files were added,
  changed or deleted in its **watched folders** since its last turn in that
  Room. Watched folders are its own `work/` folder, the folder of each of its
  Teams (excluding `_tasks`), and any folders it chose to watch. Huddle keeps
  what it last saw in `file-state/<Name>.json`.

Memory and file changes require `Team:FileChanges:Enabled` (on by default) and an
adapter that can read files. Without them, Teammates get no memory index.

> [!NOTE]
> The `memory/` folder is not protected in the Library the way Team and Project
> folders are. You can rename or delete it, and Teammates will lose the index.

## Tracking work with Tasks

A **Task** is a Markdown file under a Team or Project. You and your Teammates
see the same Tasks, and a change to an assigned Task wakes the assignee.

### What a Task file looks like

`Teams/Business/_tasks/BUSI-0002.md`:

```markdown
---
id: BUSI-0002
title: Second task for the Board
status: To Do
priority: Medium
creator: You
assignee: Nova
tags:
  - billing
due_date: 2026-10-15
---
Draft the October invoice run and check it against last month.

## Change log
- 2026-09-25T08:30:00Z | You | created
- 2026-09-25T09:12:41Z | You | assignee: — → Nova
```

| Field | Meaning |
| --- | --- |
| `id` | `<PREFIX>-<number>`. The prefix is the first four letters of the Team name, upper-cased, chosen once and stored in `team.db`. Numbers are never reused. |
| `title` | One line, 1–200 characters. |
| `status` | One of the eight statuses below. |
| `priority` | Low, Medium, High or Urgent. |
| `creator`, `assignee` | A Teammate's name, or your name. One assignee at most. |
| `origin` | The Room the Task was created from, if any. |
| `parent`, `blocked_by`, `duplicate_of` | Links to other Tasks. |
| `tags`, `start_date`, `due_date` | Dates are `yyyy-MM-dd`. |
| `request`, `from_team`, `requester`, `triage`, `decline_reason`, `confirmed_by` | Present on a Request from another Team; see [Requests between Teams](#requests-between-teams). |
| Body | The description, in Markdown. |
| `## Evidence` | Optional, only where a Team's gates look for it. Lines of evidence a Teammate added; append-only. See [The four gates](#the-four-gates). |
| `## Change log` | An append-only history. Huddle adds lines and never edits old ones. |

The Team and Project are not fields. They come from the folder the file is in.

### Statuses

| Status | Group |
| --- | --- |
| Backlog, To Do, In Progress, Review | Open |
| Done | Finished |
| Cancelled, Duplicate, Rejected | Won't do |

You can move a Task from any status to any other. **Duplicate** requires naming
the original Task. **Cancelled** and **Rejected** accept an optional reason.

**Closed is separate from status.** Closing a Task hides it from active Views and
boards; it does not change its status. Huddle never closes Tasks automatically.
There is no delete.

### Creating and editing Tasks

Select **New task** on the Tasks page or a Team page's Tasks tab. The panel has
Title, Status, Priority, Assignee, Team / Project, Parent, blockers, Tags, Start
and Due date, and a description with **Edit** and **Preview**. Select **Save**.
If the Task is assigned to a Teammate, the button reads **Save & Notify
{Name}**.

| Action | In the background |
| --- | --- |
| **Create** | Writes a new `<ID>.md` in the Team's or Project's `_tasks/` folder, with a `created` Change log line. Defaults are Backlog and Medium. |
| **Edit any field** | Rewrites the frontmatter and appends one Change log line summarising what changed, such as `status: To Do → In Progress`. |
| **Drag a card on the Board** | Saves immediately, same as an edit. |
| **Change Team or Project** | Moves the file to the other folder's `_tasks/`, and logs `moved: A → B`. A new Project name typed here creates the Project folder. |
| **Close task** / **Reopen** | In the panel's **⋮** menu. Moves the file into or out of `_tasks/_closed/` and logs `closed` or `reopened`. |
| **Make a copy** | In the panel's **⋮** menu. Opens an unsaved copy titled `Copy of …`. Nothing is written until you save. |

Every write goes to a temporary file first and is then moved into place, so a
Task file is never half-written. If someone else (a Teammate, or you in another
window) changed the same Task while you were editing, Huddle merges changes to
different fields silently and asks **Keep mine** or **Take theirs** for a field
you both changed.

### How Task changes reach Teammates

When a Task assigned to a Teammate changes, Huddle **wakes** the assignee by
posting a message in a Room, as the person who made the change:

```text
@nova Task BUSI-0002 "Second task for the Board" (In Progress, Business) was changed by You:
status: To Do → In Progress
Call get_task with taskId BUSI-0002 for the full task.
```

- The Room is, in order of preference: the Task's origin Room; a Room with
  exactly you, the creator and the assignee; otherwise a new Room.
- Changes by the same person within 5 seconds are combined into one message.
- No one is woken for their own changes, for Tasks assigned to you, or for
  assignees that are not known Teammates.
- Teammates changing each other's Tasks could loop, so each Task allows 10
  Teammate-made wake-ups. After that, changes are still saved but nobody is
  woken until you select **Allow 10 more** in the Task panel. Any change you make
  resets the count.

The panel tells you what will happen before you save, for example "Nova is
offline, so this change won't reach them."

Teammates use the tools `create_task`, `get_task`, `list_tasks`, `update_task`,
`close_task`, `reopen_task` and `add_evidence`. Their changes follow the same rules
and write the same files, and they are also subject to a Team's
[gates](#the-four-gates) and to the [Request](#requests-between-teams) rules, which
you can override from the Task panel and they cannot.

### Views: lists and boards

A **View** is a saved way of looking at Tasks. **All Tasks** and **My Tasks**
are built in. Select **New view** to make your own: it is created at once under
the name **New view** (or **New view 2** and so on), and you rename it from its `⋯` menu.

| Setting | Options |
| --- | --- |
| Kind | **List** or **Kanban** |
| Filters | Team, Project, Assignee, Sprint, Status, Priority, and Blocked / Unblocked / All |
| Grouping | The **Group by** chip: None, Team, Project, Assignee, Sprint, or Status in a List |
| Fields shown | **Fields…** in the View's `⋯` menu: drag to reorder, switch a field off, remove it or add it back |
| Column widths | Drag the right edge of a List column header |
| Dense | Compact or roomier rows in a List. On by default. |

In a List, Tags show as label chips and the Assignee as a chip with their
avatar.

The toolbar above a View has a **Filter** menu, a chip for each filter that is set
(click its **×** to clear it), a search box, and **Dense**. There is no Save button:
**every change is saved to the View as you make it**, apart from the search text.
**Reset filters** clears the filter and the search; it is the button on an empty result.

A View's `⋯` menu has **Rename**, **Fields…** and **Delete view**. **Rename** opens a
small dialog (1 to 60 characters, not the name of another View). **Delete view** asks
`Delete 'name'?` and says that Tasks are not affected.

A Task's `⋯` menu is titled **Actions for {id}**. It has **Copy id** and **Copy link**
(the toast reads **Copied link to {id}**). A Task's panel shows **Created**, **Updated**
and **Closed** dates. A Task a Teammate is working on shows a **Teammate working**
chip, and a Task that woke a Teammate links to **Woken in Room {name}**.

**In the background:** Views are saved to `views.json` in the data root. If that
file has a syntax error, Huddle shows the line and column and makes Views
read-only until you fix it.

### Editing Task files by hand

You can edit a Task file in any editor. Huddle watches the `Teams/` folder:

- **While Huddle runs**, it notices the save within half a second, appends
  `edited outside Huddle: <what changed>` to the Change log, and wakes the
  assignee.
- **While Huddle is stopped**, it notices at the next start, logs the same line,
  and wakes no one.
- **Deleting a Task file** makes the Task disappear without a log entry.

A file that fails to load is shown under **Tasks that didn't load**, with the
reason.

### Sprints

A **Sprint** groups Tasks in time. It has a name, nothing else — no dates, no state, no screen.
Type a new Sprint name in the Sprint field on the Task panel to create one; pick an existing
Sprint from the list, or leave it blank. A Sprint disappears when the last Task leaves it.

In a View, you can filter by Sprint (including **@current** for the newest, or **@none** for
Tasks with no Sprint), group by it, or sort by it. Teammates can set a Task's Sprint by name
or id, or use **current** to mean the newest. Only you can create a new Sprint by typing a
name; Teammates cannot — they must pick from the existing ones.

## Keeping notes in the Library

The **Library** is a file browser and Markdown editor over your data folders. It
is how you and your Teammates share documents.

### Library roots

A **root** is a top-level folder shown in the Library:

| Root | Folder |
| --- | --- |
| **Teams** | `Teams/`: Team and Project folders and their notes. Task folders (`_tasks`) are hidden. |
| **Teammates** | `Teammates/`: each Teammate's definition and `work/` folder. |
| Pinned folders | Any folder on your machine you add with **Add Folder** or in **Settings › Library**. |

Pinned folders are saved in `library-roots.json` in the data root. The file is
created the first time you add, remove or hide a root. **Reset pinned folders**
deletes it and goes back to the configured list (`Team:Library:Roots`). Hand
edits to `library-roots.json` are only read at startup.

### Reading and editing notes

Select a file to open it. Markdown files open with **Read**, **Edit** and
**Split** modes; `Ctrl+S` saves. Text and code files are read-only. Images are
previewed. Files larger than 2 MiB are read-only.

**In the background:** a save writes a temporary file beside the note and moves
it into place, keeping the file's original encoding and line endings. If the
file changed on disk since you opened it, Huddle warns you and offers **Reload**
or **Keep mine**. Saving a Teammate's definition file restarts that Teammate.

Teammates are told about your saves at their next turn if the file is in one of
their watched folders.

### Links between notes

Write `[[Note name]]`, `[[folder/Note]]`, `[[Note|shown text]]` or
`[[Note#Heading]]`. Links resolve within the same root, in the style of
Obsidian. Each note shows its **Backlinks**, the notes that link to it. A link to
a missing note offers to create it.

### Organising files

Open the `⋯` menu on any item, or right-click it.

| Action | In the background |
| --- | --- |
| **New note** | Creates an empty file (`.md` added if you give no extension). |
| **New folder** | Creates the folder. Inside a Team folder, this is a new Project. |
| **Rename** / **Move…** | **Move…** opens a folder picker with **Cancel** and **Move**. Moves the file or folder, then rewrites `[[links]]` in every note in the root that pointed at it. The dialog tells you how many links will change. Notes it could not update are listed. Moves stay within one root. |
| **Copy path** | Copies the full path, which you can paste into chat. |
| **Delete** | Asks `Delete 'name'?`, then sends the item to the **Windows Recycle Bin**. Refused on network or removable drives, where the Recycle Bin is not available. |

Roots, Team folders, Project folders, Teammate folders, definitions and `work/`
folders cannot be renamed, moved or deleted here. Folders that hold Tasks cannot
be moved. File names follow Windows rules: no `/ \ : < > " | ? *`, no trailing
dot or space, no device names such as `CON` or `NUL`.

The search box matches file names, not content, and shows at most 200 results.

## Giving Teammates Skills

A **Skill** is a folder of Markdown instructions that a Teammate reads only when
it needs them, such as how to build a team or how to write a release note. It
keeps the Teammate's main instructions short.

### Assigning a Skill

On a Teammate's Edit card, pick Skills in the **Skills** list and save.

**In the background:** Huddle writes `skills: ['a', 'b']` into the Teammate's
`<Name>.md` and restarts it. The Teammate's system prompt then lists each Skill's
name and description, and it gets the `read_skill` tool to read them.

### Writing your own Skill

Skills have no editor in the app. Create a folder under `Skills/` in the data
root:

```text
App_Data/Skills/release-notes/
├── SKILL.md
└── examples.md
```

```markdown
---
name: release-notes
description: How to write release notes for the Business team's product updates.
---
Start with what the user can now do. One bullet per change. ...
```

| Rule | Detail |
| --- | --- |
| `name` | Lower-case words joined by `-`, equal to the folder name. |
| `description` | Required. Keep it under 300 characters; over 500 is an error. |
| Files | Only `.md` files directly in the folder count, up to 64 KB each. Sub-folders are ignored. |

Huddle watches `Skills/` and picks up a new or changed Skill within a second.
Editing a Skill does not restart anyone. Teammates read the current text the
next time they call `read_skill`. Check **Settings › Skills** for problems: an
invalid Skill is listed under **Problems** with the reason.

### Changing a built-in Skill

Huddle ships four Skills: `team-building` and `team-rollup`, used by the Chief of
Staff, `team-management`, used by Team managers to see other Teams, handle Requests
and propose Sprints, and `retrospective`, used by the Retro agent. To change one,
create `Skills/<name>/` (for example `Skills/team-building/`) and put a file of the
same name in it, for example `onboarding.md`. Your file replaces that one file; the
others still come from the built-in version. **Settings › Skills** marks the Skill
as **Overridden**. **Restore default** deletes your whole `Skills/<name>/` folder,
including any extra files you added. It asks **Restore default?** first; the
dialog's **Restore default** button confirms.

### Importing a Skill

On the **Settings** page's **Skills** tab, select **Import skill**. The dialog
takes either an entry from the catalog, found by name, publisher or category, or
a GitHub tree URL you paste in.

Select **Review** to fetch a preview. The URL must be an `https://github.com/<owner>/<repo>/tree/...`
folder; Huddle pins it to a commit. The preview lists the publisher, the repository,
the commit, the licence, and any flags, grouped under **Scripts**, **Shell
commands**, **Network calls**, **Paths outside the Work Dir** or **No license**.
A flag is a warning for you to read, not a block. A skill is refused, with the
reason, when its folder is over 5 MB or 200 files, holds a symbolic link or an unsafe
path, has no valid `SKILL.md`, or is larger than the download limit. Nothing is
written until you select **Install**. The buttons run **Cancel**, **Back**, **Install**,
and **Back** and **Cancel** write nothing. After an
install the dialog links to the Skills tab.

Two cases are safe to repeat or refused:

- Importing the **same** skill again, from the same source and commit, is a no-op:
  it reports success and changes nothing.
- A name that clashes with a built-in Skill, or with a Skill you wrote yourself or
  imported from somewhere else, is refused, and the existing one is left alone.

An installed Skill keeps a `.source.json` file in its folder, which records where
it came from. Wherever the Skill appears (the Skills tab, a Teammate's card), its
provenance line shows the publisher, repository, commit and licence. If the folder
has changed since the import, the line also shows a **modified locally** chip. A
Skill you wrote yourself shows **Yours**, and a shipped one shows **Built in**.

Hiring a Team installs any Skill its members need that is missing, by the same
import, without this dialog; see [Hiring a Team from a type](#hiring-a-team-from-a-type).

> **Example (Scenario 2).** Emre adds the MSBuild Authoring pack to Jane's card. The
> pack's Skills join hers, and the card's warnings, described under
> [Packs, setup warnings and the Process step](#packs-setup-warnings-and-the-process-step),
> show whether the set now crowds or overlaps.

## Settings

| Tab | What it changes | Saved to |
| --- | --- | --- |
| **Prompts** | The text Huddle sends to Teammates, such as the wake-up message. **Reset all to defaults** (beside the sticky **Save**) asks "Reset every prompt to its default?" and **Reset all** confirms. | `prompts.json` |
| **Appearance** | Theme, light/dark and accent colour. | `appearance.json` |
| **Personas** | The Teammate definitions. | `Teammates/…` |
| **Skills** | List of Skills with where each came from, and their problems; **Import skill**; **Restore default**. | `Skills/…` |
| **Team Types** | Read-only list of team types, built-in or yours, with **Reload** and a **Problems** list. | `TeamTypes/…` |
| **Library** | Pinned folders; hide the built-in roots. | `library-roots.json` |

## What happens when… (reference)

| You… | Huddle writes | Teammate effect |
| --- | --- | --- |
| Add a Teammate | `Teammates/<Name>/<Name>.md`, `team.db`, `Teams/<label>/` if missing | Comes online |
| Edit a Teammate, or change Model, Effort, Work mode, Skills or Teams | Rewrites `<Name>.md` (Model, Effort and Work mode in `team.db`) | Restarts; conversation memory cleared |
| Rename a Teammate | Moves `Teammates/<Old>/`; renames entries in `room-sessions/`, `file-state/`, `avatars.json`, `team.db`; rewrites Task creators and assignees and View filters | Restarts |
| Remove a Teammate | Deletes `<Name>.md` only | Goes offline; Rooms, transcripts and `work/` stay |
| Restart a Teammate | Deletes `room-sessions/<Name>.json` | Every Room starts fresh, with catch-up |
| Create a Team | `Teams/<Team>/` | None |
| Create a Team from a type | `Teams/<Team>/_team.md`, then the manager's Teammate file | The manager starts; **Start Team** hires and starts the rest and posts the onboarding message to the manager once |
| Duplicate a Team | A new `Teams/<Team>/` and new Teammate files; no memory, notes or Tasks | New Teammates start |
| Use a process | `Teams/<Team>/Process.md`, never over an existing one | Members read it |
| Import a Skill | `Skills/<name>/` with `.source.json` | Read on next `read_skill` |
| Turn a gate off | The `gates:` line of `Process.md` | None |
| Approve or decline a Sprint proposal | Approve creates the Sprint and adds the Tasks | The manager reads the outcome |
| Accept or reject a retro change | Accept edits the target file and adds a row to `Retro register.md` | The proposer reads the outcome; an edited Teammate file restarts that Teammate |
| Override a gate | The Task, with `gate overridden by Human` in its Change log | Assignee woken |
| Add or remove a Team member | The `teams:` line in `<Name>.md` | That Teammate restarts |
| Create a Project | `Teams/<Team>/<Project>/` | Members see it as a file change |
| Start a chat | `team.db` | None until you post |
| Post a message | One line appended to `rooms/<roomId>.jsonl` | Addressed Teammates take a turn |
| Write `@name /compact` | One line in `rooms/<roomId>.jsonl`, then the Teammate's outcome line | That Teammate compacts its conversation; other Teammates only read the outcome |
| Archive a Room | `team.db` | None; Room still works |
| Delete a Room | `team.db`; deletes `rooms/<roomId>.jsonl` permanently | Resume entry pruned at next start |
| Create or edit a Task | `Teams/<Team>/[<Project>/]_tasks/<ID>.md` with a Change log line | Assignee woken |
| Close or reopen a Task | Moves the file to or from `_tasks/_closed/` | Assignee woken |
| Save a View | `views.json` | None |
| Save a Library note | The note, atomically | Watchers see a file change next turn |
| Rename or move a Library item | The item, plus every note linking to it | Watchers see file changes |
| Delete a Library item | Moved to the Recycle Bin | Watchers see a deletion |
| Pin a Library folder | `library-roots.json` | None |
| Add or change a Skill folder | Nothing; you write `Skills/<name>/` | Read on next `read_skill` |
| Edit any of these files by hand | — | Same as the in-app action; Tasks log `edited outside Huddle` |

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| `@nova /compact` got an ordinary reply instead of compacting | The Teammate does not offer that command (check its card), it was not yet online, or another Teammate wrote it. Only you can run a command, and only those listed on the card. |
| `/compact` on its own says *Unknown command* | Write the mention first: `@nova /compact`. |
| A Teammate forgot our conversation | It was restarted: an edit, a membership change, or **Restart**. Put lasting facts in memory files. |
| A Teammate file does not appear | See **Files that didn't load** on the Teammates page. Common causes: missing `name`, `title` or `alias`, a duplicate alias, or `name` not matching the file. |
| All Teammates show **Offline** | The agent host is off, for example when Huddle is started with `--Team:Acp:Enabled=false`. |
| A Task did not wake its assignee | The assignee is you, the change was made by the assignee, the assignee is offline or no longer exists, or the Task's wake budget is spent (**Allow 10 more**). |
| A Task file is missing from every View | It is under **Tasks that didn't load**, or it is in `_closed/` and the View's scope is Active. |
| A Team still appears after I removed everyone | Removing members never deletes the folder. The Team shows **No members**. |
| A membership change was undone | A Teammate Edit card that was open during the change saved its old text. Re-add the member. |
| Delete in the Library says the Recycle Bin isn't available | The file is on a network or removable drive. Delete it in your file manager. |
| A new Skill doesn't show | Check **Settings › Skills › Problems**. The `name` must match the folder name. |
| A Team type I wrote is missing from the Team type list | Check **Settings › Team Types › Problems** for the file and reason, fix it, then select **Reload**. |
| **Start Team** is disabled | The team check bar lists why: a card that needs setup, no manager, or a required role not hired. |
| A hire says a Team is full, or that agent processes are running | A hire limit was reached; see [Hire limits](#hire-limits). |
| A Task would not move, with a message naming a gate | The Team's process has that gate on. Add the Evidence it asks for, or use **Move anyway** in the Task panel. |
| A Teammate's `git push` was denied | The Team's `checks-before-merge` gate wants an Evidence line showing the `checkCommand` passing. The reason is in the log, not shown to the Teammate. |
| A Task I edited by hand went back to its old status | A gate refused the new status; the Change log says `outside edit refused by gate`. |
| An imported Skill says it already exists | The name belongs to a built-in Skill or to one from another source. Nothing was changed. |
| A card says **Needs setup** for a connector | List it in `connectors.json` in the data folder, then select **I connected it**. |
