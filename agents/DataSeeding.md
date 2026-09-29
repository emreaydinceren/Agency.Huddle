# Seeding Huddle with test data

This guide is for an AI agent that turns a written **scenario**, such as "a
two-person business with a website project running late", into a complete,
testable Huddle data folder. It covers Teammates and their personalities, Teams,
Projects, Tasks, Views, Library notes, memory, Skills and chat Rooms. Follow the
phases in order; each one depends on the one before.

The procedure was run end to end on 2026-09-29 against `main`. For what each
entity means to a user, read [the user guide](../docs/Huddle.UserGuide.md).

## The rules

1. **Write only Markdown (`.md`) and JSON (`.json`, `.jsonl`) files by hand.**
   Everything that lives in SQLite (`team.db`) is created through the web UI:
   Teammates coming online, chat Rooms and their members, Room names, archiving,
   Model and Effort. Never open `team.db` for writing, and do not read it either.
   Get the IDs you need from the UI and from transcript files, as described
   below.
2. **Never seed into the user's own data folder.** Always run Huddle against a
   fresh, absolute `Team:DataDir` of your own.
3. **Write files while Huddle is stopped**, except where a step says otherwise.
   Huddle watches its folders. A file written while it runs is treated as an
   edit made outside Huddle: Tasks get a log line and a new Updated date, and
   Teammates restart.
4. **Do not spend money.** Teammates run on the free mock adapter
   (`mock-acp`), never on a paid model.
5. **If the scenario needs another file type**, such as images, `.txt` or `.csv`
   files in the Library, or uploaded avatars, ask the user before adding it.

## Overview

| Phase | Huddle | What you do |
| --- | --- | --- |
| 1. Plan | — | Turn the scenario into a seed manifest |
| 2. Prepare | Stopped | Build, choose folders, write the launch config |
| 3. First start | Running, then stopped | Let Huddle create its database and folders |
| 4. Write files | Stopped | Teammates, Skills, Tasks, notes, memory, Views, settings |
| 5. Bring online | Running | Teammates connect; check the load |
| 6. Web UI steps | Running | Rooms, empty Teams and Projects, Model and Effort, sender IDs |
| 7. Write chat history | Stopped | Append messages to Room transcripts |
| 8. Verify | Running | Walk the checklist; finish the manifest |

## Phase 1: Plan the scenario

Before writing anything, turn the scenario into a **seed manifest**: a Markdown
file that lists every entity you will create and the facts a tester will assert.
Save it as `<seed-root>/manifest.md`, next to the data folder, not inside it.

The manifest has one table per entity type:

| Table | Columns |
| --- | --- |
| Teammates | Name, alias, title, Teams, Skills, one-line personality |
| Teams | Name, members, Projects, has memory? |
| Tasks | ID, Team/Project, title, status, priority, assignee, closed?, due, blocked by, parent, notes |
| Rooms | Name, members, message count, archived? |
| Views | Name, kind, filters, expected Task IDs |
| Library notes | Path, links to, expected backlinks |

Fix every value in advance: names, IDs, dates, timestamps. Deterministic data
lets tests assert exact counts, and you can regenerate the same seed later.

### Cover the cases a tester needs

A seed is "fully testable" when every visible state appears at least once.
Unless the scenario forbids it, include:

| Area | Include |
| --- | --- |
| Teammates | One in **two or more Teams**; one in **no Team**; one holding a **Skill**; distinct personalities |
| Teams | One with Projects; one with **no members**; one with **no Projects**; a Project with **no Tasks** |
| Tasks | Each of the eight statuses; at least one **closed**; one **overdue** (due date past, status not finished); one **blocked** by an open Task; one blocked by a **finished** Task (it is not shown as blocked); a **sub-task**; a **Duplicate**; one **unassigned**; one **assigned to the Human**; Tasks at the Team root and inside a Project; tags; a long Markdown description |
| Change logs | Entries by the Human and by Teammates; a `status:` change; a `moved:` entry; a closed Task with a `closed` entry |
| Views | A List and a Board; a filter by Team, by assignee and by status; one grouped by Project |
| Library | Notes linking with `[[wikilinks]]`; one note with several **backlinks**; one **unresolved** link; a note in a Teammate's `work/` folder |
| Memory | Team Memory facts; a Project memory fact; a Teammate's personal memory |
| Rooms | A one-to-one Room; a group Room with mentions; an **archived** Room; a Room with a custom name; messages that mention Task IDs |

Negative cases, such as a Task file that fails to load, belong in a
separate seed. Mixing them in makes the "didn't load" lists noisy for every other
test.

## Phase 2: Prepare

### Build and choose the folders

Build the solution once. The build produces the mock adapter.

```powershell
dotnet build Huddle.slnx
Test-Path src\Huddle.MockAdapter\bin\Debug\net10.0\mock-acp.exe
```

```text
True
```

Pick a **seed root** outside the repository, with the data folder inside it:

```text
<seed-root>/
├── manifest.md       your seed manifest (Phase 1)
├── data/             Team:DataDir: Huddle's data folder, empty at first
└── pinned/           optional: a folder to pin in the Library
```

Do not name the data folder `data` if it sits inside the repository, and do not
put the seed root inside `src/`.

### Write the launch configuration

Run Huddle with these settings. Every one of them matters:

| Setting | Why |
| --- | --- |
| `--Team:DataDir=<absolute path>` | Isolates your data. A relative path resolves against the process working directory. |
| `--urls http://localhost:5199` | A port that does not clash with the user's instance on 5100. |
| `--Team:PipeName=huddle-seed` | Instances sharing a pipe name cross-connect their agents. |
| `--Team:DemoAgent:Enabled=false` | Removes the `echo` and `alpha` demo agents from Rooms and pickers. |
| `--Team:Tasks:WakeEnabled=false` | Stops Task changes from posting wake-up messages. |
| `--Team:Acp:Enabled=true` plus the four `Adapters:0` keys | Runs every Teammate on the free mock adapter. Set it explicitly: `appsettings.Development.json` changes the default. |

In Claude Code's desktop app, add an entry to `.claude/launch.json` and start it
with `preview_start`. Remove the entry when you are done, because that file is
committed.

```json
{
  "name": "huddle-seed",
  "runtimeExecutable": "dotnet",
  "runtimeArgs": [
    "run", "--project", "src/Huddle.App", "--",
    "--urls", "http://localhost:5199",
    "--Team:DataDir=C:\\seeds\\website-late\\data",
    "--Team:PipeName=huddle-seed",
    "--Team:DemoAgent:Enabled=false",
    "--Team:Tasks:WakeEnabled=false",
    "--Team:Acp:Enabled=true",
    "--Team:Acp:Adapters:0:Id=mock",
    "--Team:Acp:Adapters:0:Command=E:\\Repos\\Huddle\\src\\Huddle.MockAdapter\\bin\\Debug\\net10.0\\mock-acp.exe",
    "--Team:Acp:Adapters:0:Args:0=--mock",
    "--Team:Acp:Adapters:0:UsesToolNamePrefix=false"
  ],
  "port": 5199
}
```

`Args:0` must be present even though the mock ignores it. Without it Huddle
reports that no adapter is installed. Because `mock` is the only adapter, every
Teammate uses it, and the Teammate files need no `adapter:` line.

If the scenario names the Human, add `--Team:HumanName=<name>` and use the same
name as `creator` in Task files and as `senderName` in transcripts. The default
is `You`.

## Phase 3: First start on an empty folder

Start Huddle once with the empty data folder. Wait until the Teammates page
loads, then stop it.

> [!WARNING]
> Do not write any files before this first start. On a folder without the
> marker `Teammates/.layout-migrated`, Huddle runs a one-time layout migration
> that moves every `.md` file it finds under `Teams/`, other than Task files,
> into `Teammates/_unsorted/`. Your notes and memory end up in the wrong place.

The first start creates:

| Item | Notes |
| --- | --- |
| `team.db` | The database, with the Human's row |
| `Teammates/.layout-migrated` | The marker that stops the migration running again |
| `Teammates/Chief of Staff/Chief of Staff.md` | The built-in Teammate, always present |
| `Teams/`, `Skills/`, `avatars/`, `logs/` | Empty folders |
| A Room for the Chief of Staff, and `rooms/<id>.jsonl` | Holds its first "greeting". On the mock adapter the greeting is the prompt echoed back; you replace it in Phase 7 |

## Phase 4: Write the files

Stop Huddle first. Write UTF-8 files without a byte-order mark. Either LF or
CRLF line endings work.

### Teammates and their personalities

Each Teammate is `Teammates/<Name>/<Name>.md`. The folder, the file name and
`name:` must be identical.

```markdown
---
name: 'Nova'
title: 'Finance Analyst'
alias: 'nova'
teams: ['Business', 'Household']
skills: ['month-end-close']
specialty: 'VAT, invoicing, cash-flow forecasts'
---
You are Nova, the finance analyst for a two-person design studio.

Voice: precise and calm. You answer in short paragraphs and always give numbers
with their currency. You never guess a figure; you say what you would need to
check.

How you work:
- You own anything to do with invoices, VAT and bank reconciliation.
- When work is agreed, you create a Task for it in the Business team and assign
  it to whoever will do it.
- You record lasting facts about the business as Team Memory, one fact per file.

Boundaries: you do not give legal advice. You hand design questions to Kai.
```

| Rule | Detail |
| --- | --- |
| Required | `name`, `title`, `alias`, all non-blank |
| Name and alias | 1–64 characters; start with a letter or digit; then letters, digits, `_`, `-` and single spaces; no dots |
| Uniqueness | Names are unique, aliases are unique, and no alias may equal another Teammate's name. Case is ignored |
| `teams`, `skills`, `watches` | `['A', 'B']`, a block list, or `A, B` all work |
| Extra keys | Any other key, such as `specialty:`, is shown to other Teammates as part of this Teammate's job description |
| `model:` / `effort:` keys | Have no effect. Model and Effort live in `team.db`; set them in Phase 6 |
| Folder contents | Only the definition file at the top level. Anything else goes in `work/` |
| Chief of Staff | Leave its file alone. Huddle writes a new one at every start if none is marked `_builtin: 'chief-of-staff'` |

**Writing a good personality.** The body is the Teammate's private system
prompt; other Teammates never see it. Make each Teammate recognisably different
so a tester can tell replies apart on a real adapter:

- **Open with identity and role** in one sentence.
- **Give a voice**: sentence length, tone, habits ("always ends with one
  question").
- **Say what it owns** and how it uses Tasks and memory, so tool behaviour is
  predictable.
- **Set boundaries and hand-offs** to named colleagues. This exercises
  mentions in group Rooms.
- Keep it under about 300 words. Put procedures in a Skill instead.

A Teammate's `teams:` labels are enough to create its Teams. Huddle creates
`Teams/<label>/` for each one when the Teammate loads.

### Skills

A Skill is `Skills/<skill-name>/SKILL.md`, plus any extra `.md` files beside it.

```markdown
---
name: month-end-close
description: The steps Nova follows to close the books at the end of each month.
---
1. Reconcile the bank account against the invoices list.
2. Create a Task for every unmatched payment.
```

`name` is lower-case words joined by `-`, identical to the folder name.
`description` is required and at most 500 characters; keep it under 300. Files
over 64 KB are skipped, and sub-folders are ignored.

### Teams, Projects and Library notes

A Project is any folder directly inside a Team folder. Creating a file inside it
creates the Project.

```text
data/Teams/Business/
├── memory/
│   ├── invoice-day.md              Team Memory
│   └── studio-hours.md
├── Clients.md                      a Team note
└── Website/                        a Project
    ├── memory/
    │   └── launch-date.md          Project memory
    ├── Brief.md
    └── Research/
        └── Competitors.md
```

- **Memory files** hold one fact each. The first non-blank line is the fact,
  and it is the only line Teammates see in their index.
- **Wikilinks**: `[[Brief]]`, `[[Research/Competitors]]`, `[[Brief#Goals]]`,
  `[[Brief|the brief]]`. Links resolve within one Library root, by file name,
  case-insensitively. Add one link to a note that does not exist to test the
  unresolved state.
- **Reserved names**: never create `memory` as a Project, or any folder starting
  with `_` or `.` under `Teams/`. Those are not Projects.
- A Team with no members, or a Project with no files, is created in the UI
  (Phase 6), not by making an empty folder.

**Personal memory and working files** go in `Teammates/<Name>/work/`, for
example `Teammates/Nova/work/memory/prefers-euros.md`, or
`Teammates/Nova/work/Cash-flow.md`.

### Tasks

Each Task is `Teams/<Team>/[<Project>/]_tasks/<ID>.md`. Closed Tasks go in the
`_closed/` folder inside `_tasks/`. The folder decides the Team and Project.

```markdown
---
id: BUSI-0001
title: Send the quarterly VAT return
status: In Progress
priority: High
creator: You
assignee: Nova
tags:
  - finance
  - tax
start_date: 2026-09-01
due_date: 2026-09-15
---
Collect the Q3 invoices and send them to the accountant.

## Change log
- 2026-09-01T09:00:00Z | You | created
- 2026-09-01T09:05:00Z | You | assignee: — → Nova
- 2026-09-02T10:30:00Z | Nova | status: To Do → In Progress
```

| Key | Rule |
| --- | --- |
| `id` | `<PREFIX>-<number>`, 4-digit padded. Unique across **all** Teams, including closed Tasks. A duplicate rejects both files |
| `title` | Required, one line, 1–200 characters |
| `status` | `Backlog`, `To Do`, `In Progress`, `Review`, `Done`, `Cancelled`, `Duplicate` or `Rejected` |
| `priority` | `Low`, `Medium`, `High` or `Urgent` |
| `creator` | Required. The Human's name or a Teammate's name |
| `assignee` | Optional. A Teammate's exact `name`, or the Human's name |
| `origin` | Optional. A Room ID, which links the Task to that Room |
| `parent` | Optional. One Task ID, which makes this a sub-task |
| `blocked_by` | Optional list of Task IDs: `[BUSI-0001, BUSI-0005]` or a block list |
| `duplicate_of` | Required when status is `Duplicate`, and forbidden otherwise |
| `tags` | Optional list. Each 1–40 characters, no `,` or `;` |
| `start_date`, `due_date` | Optional, `yyyy-MM-dd` |

**Choose the ID prefix the app would choose.** Huddle derives a Team's prefix
from its name: the first four letters or digits, upper-cased, skipping leading
digits. `Business` gives `BUSI`, `Household` gives `HOUS`. When you use the
same prefix, the next Task created in the UI continues after your highest
number. Give Teams names whose first four letters differ; two Teams starting
`MARK` get `MARK` and `MARK2` in whatever order they are first used.

**Write the Change log carefully.** It drives the Created, Updated and Closed
dates:

- Each line is `- yyyy-MM-ddTHH:mm:ssZ | Actor | summary`, oldest first, in UTC,
  with no fractions of a second. Lines that do not match are ignored.
- **Created** is the first entry, and **Updated** is the last.
- **Closed** is the last entry whose summary is exactly `closed`. Give every
  Task in `_closed/` one.
- Use the app's wording so the log looks real: `created`,
  `status: To Do → In Progress`, `priority: Medium → High`,
  `assignee: — → Nova` (`—` means empty), `tags: +finance`,
  `moved: Business → Business/Website`, `closed`, `reopened`,
  `description edited`.

**Set each Task file's modified time to its last Change log entry.** When
Huddle starts, it compares the two. If the file is more than two seconds newer,
it appends `edited outside Huddle` with the current time, and your backdated
Updated date is lost.

```powershell
$task = Get-Item 'C:\seeds\website-late\data\Teams\Business\_tasks\BUSI-0001.md'
$task.LastWriteTimeUtc = [datetime]::Parse('2026-09-02T10:30:00Z').ToUniversalTime()
```

**Derived states** need no field:

| State | How to produce it |
| --- | --- |
| Overdue | `due_date` before today and status not Done, Cancelled, Duplicate or Rejected |
| Blocked | A `blocked_by` Task that exists and is not finished |
| Closed | File in `_tasks/_closed/`. Status can be anything; `Done` is typical |

### Views

Saved Task Views go in `views.json` at the data folder root. `all-tasks` and
`my-tasks` are built in; do not redefine them.

```json
{
  "version": 1,
  "views": [
    {
      "id": "business-urgent",
      "name": "Business urgent",
      "kind": "list",
      "scope": "active",
      "fields": ["status", "priority", "assignee", "due_date", "updated"],
      "filter": {
        "teams": ["Business"],
        "priorities": ["High", "Urgent"],
        "blocked": "unblocked"
      },
      "grouping": ["assignee"],
      "sort": [{ "field": "due_date", "direction": "ascending" }],
      "columns": []
    },
    {
      "id": "business-board",
      "name": "Business board",
      "kind": "board",
      "scope": "active",
      "fields": ["priority", "assignee", "due_date"],
      "filter": { "teams": ["Business"] },
      "grouping": ["project"],
      "sort": [],
      "columns": [
        { "label": "Backlog", "states": ["Backlog"] },
        { "label": "To Do", "states": ["To Do"] },
        { "label": "In Progress", "states": ["In Progress"] },
        { "label": "Review", "states": ["Review"] },
        { "label": "Done", "states": ["Done"] },
        { "label": "Won't do", "states": ["Cancelled", "Duplicate", "Rejected"] }
      ]
    }
  ]
}
```

| Property | Rule |
| --- | --- |
| `id` | Unique, URL-safe; the View opens at `/tasks/<id>` |
| `name` | 1–60 characters, unique |
| `kind` / `scope` | `list` or `board` / `active` or `closed`. A Board must be `active` |
| `fields` | From: `id`, `title`, `status`, `priority`, `assignee`, `creator`, `team`, `project`, `parent`, `blocked_by`, `tags`, `start_date`, `due_date`, `created`, `updated`, `closed`, `origin` |
| `filter` | `teams`, `projects` (`[{ "team": "Business", "project": "Website" }]`, with `null` for no Project), `assignees` (names, `@me`, `@unassigned`), `states`, `priorities`, `blocked` (`all`, `blocked`, `unblocked`) |
| `grouping` | Any of `team`, `project`, `assignee`, `state`. A Board cannot group by `state` |
| `sort` | `[{ "field": ..., "direction": "ascending" or "descending" }]`; not `tags` or `blocked_by` |
| `columns` | Boards only: every one of the eight statuses in exactly one column; labels up to 30 characters |

### Other settings files

All of these sit at the data folder root and are optional.

```json
{ "Nova": { "label": "NV", "background": "#2e7d6b" }, "You": { "label": "ME", "background": "#d46002" } }
```

That is `avatars.json`: initials and a colour per name. Pictures need an upload
through the Teammate card, so leave them out unless the user agrees.

```json
{ "theme": "huddle", "accentColor": "#e2762d", "libraryPaneSide": "right" }
```

That is `appearance.json`.

```json
{ "pinned": [{ "name": "Studio archive", "path": "C:\\seeds\\website-late\\pinned" }], "hidden": [] }
```

That is `library-roots.json`, which pins extra Library folders. It is read only
at startup.

`prompts.json` overrides prompt texts. Leave it out unless the scenario is about
prompts.

## Phase 5: Bring the Teammates online

Start Huddle. Open `/teammates` and check:

- Every Teammate appears, under each of its Teams, marked **Online**.
- There is **no "Files that didn't load"** box. If there is, fix the file it
  names while Huddle runs; it reloads within a second.
- Each Teammate has a one-to-one Room in the **Chats** group. Huddle creates it
  when the Teammate first connects.

A Teammate becomes selectable in chat pickers only after it has connected once.
That is why the mock adapter is needed. With `Team:Acp:Enabled=false`, no
seeded Teammate can join a Room.

## Phase 6: Web UI steps

Do these in the browser at `http://localhost:5199`.

### Collect each Teammate's sender ID

Transcript lines identify a Teammate by an internal sender ID, which lives in
`team.db`. Collect each one from the transcripts instead:

1. Open the Teammate's one-to-one Room from **Chats**.
2. Type `ping` in the message box and press Enter. The mock replies in about a
   second.
3. Read the Room ID from the address bar (`/rooms/<roomId>`), then open
   `data/rooms/<roomId>.jsonl`. The reply line's `senderId` is the Teammate's
   ID. The Chief of Staff's ID is already on its greeting line.
4. Record every ID in the manifest.

You remove these `ping` lines in Phase 7.

### Create the Rooms

| Goal | Steps |
| --- | --- |
| Group Room | Select **New chat**, tick two or more Teammates, select **Start chat**. It is named after its members, for example "Nova, Kai" |
| One-to-one Room | Already exists. Selecting a single Teammate in **New chat** reopens it |
| Add a member | In the Room, select **Add teammate** at the top right and choose one |
| Custom name | Select the pencil beside the Room name, type the name, press Enter |
| Archive | Hover the Room in **Chats**, open its `⋯` **Room actions** menu, select **Archive** |

Record each Room's ID from the address bar.

> [!CAUTION]
> Do not type messages that mention a Teammate (`@nova`) in the web UI. The
> mock replies to every mention, and its reply is the full prompt echoed back.
> In a Room with several Teammates, echoed mentions can set off a chain of
> replies until the Room's budget pauses them. Write conversations in Phase 7
> instead.

### Create empty Teams and Projects

| Goal | Steps |
| --- | --- |
| Team with no members | Select **New team** at the bottom of the **Teams** group, type the name, select **Create** |
| Project with no files | Open `⋯` on the Team row in the sidebar, select **New project**, type the name, select **Create** |

### Set Model and Effort (only if the scenario needs them)

Open **Teammates**, select **Edit** on the card, choose **Model** and
**Effort**, and select **Save**. The list shows the mock adapter's models, which
may not exist on the adapter the tests later use. Skip this step unless the
scenario is about models.

### Add memberships the files did not set

Normally `teams:` in the Teammate files covers membership. To exercise the UI
path instead, open the Team page, **Members** tab, **Add member**.

## Phase 7: Write the chat history

Stop Huddle. Each Room's transcript is `data/rooms/<roomId>.jsonl`: one JSON
object per line, displayed in file order.

```json
{"id":"7eb91183b33f4ea19b57ada1f2bc6f77","timestamp":"2026-09-29T18:53:00.0000000+00:00","senderId":"01a0ee81131673b6b1fc132454e7bc86","senderName":"Nova","text":"@kai I'll finish BUSI-0001 first; it blocks your landing page."}
```

| Field | Rule |
| --- | --- |
| `id` | Unique 32-character hex string, for example a GUID without dashes |
| `timestamp` | ISO 8601 with offset. Keep lines in ascending time order |
| `senderId` | `human` for the Human; otherwise the ID collected in Phase 6 |
| `senderName` | The Human's name (`You` by default), or the Teammate's current `name` |
| `text` | Markdown. Task IDs such as `BUSI-0002` become links; `@alias` shows as a mention |

Then:

1. **Clean up.** Delete the `ping` and mock-reply lines from the one-to-one
   Rooms. Replace the `text` of the Chief of Staff's greeting line with a
   realistic greeting, or delete the line.
2. **Append the conversations** for each Room from the manifest. A Room with no
   messages yet has no file; create `<roomId>.jsonl`.
3. **End every file with a newline.** Huddle appends without adding one, so a
   missing final newline corrupts the next real message.
4. Only use sender IDs of the Room's members.
5. To link a Task to a conversation, set the Task's `origin:` to that Room's ID
   and reset the file's modified time as in Phase 4.

Seeded messages are history only. Loading them never makes a Teammate reply.

## Phase 8: Verify

Start Huddle and walk this checklist. Record the results in the manifest.

| Check | Where | Expect |
| --- | --- | --- |
| Teammates load | `/teammates` | Every Teammate, correct Teams; no "Files that didn't load" |
| Teams and Projects | Sidebar **Teams** | Every Team and Project; empty Teams show **No members** |
| Tasks load | `/tasks/all-tasks` | Every open Task; no "Tasks that didn't load" |
| No outside edits | Each Task's **Change log** | No `edited outside Huddle` line you did not write |
| Board states | A Team page, **Tasks** tab | Correct columns and lanes; overdue icon; blocked Tasks |
| Closed Tasks | A View with scope Closed | Every Task from `_closed/` |
| Views | Sidebar **Tasks** | Every View from `views.json`; each shows its expected Task IDs |
| Library | Team page, **Files** tab | Notes present; wikilinks resolve; **Backlinks** listed; unresolved link offers creation |
| Skills | `/settings/skills` | Each Skill listed; no **Problems** |
| Rooms | Each Room | Messages in order, from the right senders; Task IDs are links |
| Server log | Console or `data/logs/` | No errors or warnings about your files |

Finally, stop Huddle and remove your entry from `.claude/launch.json`. The seed
is the whole `<seed-root>` folder: copy it to reuse the scenario, and point
`Team:DataDir` at a copy for each test run so tests never change the original.

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| Notes appear in `Teammates/_unsorted/` | Files were written before the first start | Move them back to `Teams/`; the marker now exists |
| A Task shows `edited outside Huddle` | The file's modified time is later than its last log entry | Reset the time in the file and remove the line |
| A Task is missing | It is under **Tasks that didn't load** | Read the reason: duplicate ID, bad status, `duplicate_of` rules |
| A Teammate is missing or Offline | Bad frontmatter, or `Acp:Enabled` is off | Check "Files that didn't load" and the launch settings |
| "No ACP adapter is installed" | `Adapters:0:Args:0` is missing | Add `--Team:Acp:Adapters:0:Args:0=--mock` |
| A Teammate is not in **New chat** | It has never connected | Start with the mock adapter and wait for Online |
| `echo` and `alpha` appear | Demo agents are on | Add `--Team:DemoAgent:Enabled=false` |
| Replies are long prompt text | That is the mock adapter | Replace them in Phase 7 |
| A seeded message does not show | Invalid JSON on that line, or no final newline on the line before | Fix the line; bad lines are skipped with a warning in the log |
| First UI-created Task gets `-0001` | Your files use a different prefix from the one the app derives | Use the Team's derived prefix |
