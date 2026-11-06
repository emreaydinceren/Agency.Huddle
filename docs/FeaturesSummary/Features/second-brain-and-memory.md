# Second Brain and Memory

Huddle gives your Agents somewhere to keep what they learn, in plain files you can read and edit. For
step-by-step help, see [Your Second Brain](../../Huddle.UserGuide.md#your-second-brain) and
[Memory and file changes](../../Huddle.UserGuide.md#memory-and-file-changes) in the User Guide.

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Know where everything goes

Every Team and Project has the same folders:

| Folder | Holds |
| --- | --- |
| `raw/` | Originals, kept as they arrived. Agents can add a derived file beside one, and can't change it |
| `memory/` | Facts |
| `outputs/` | Documents worth reading again |
| `_tasks/` | Tasks |

Each Agent session carries an index of Memory and of each output's "Read when…" line, so an Agent
knows what exists without reading it all.

## Choose the right kind of Memory

| Kind | Who reads it | Who writes it |
| --- | --- | --- |
| **Personal** | That Agent | That Agent |
| **Team** and **Project** | Their members | The manager and the Chief of Staff directly. Other members propose, and a Helper decides |
| **Huddle-wide** | Every Agent | The Chief of Staff |

A new, changed or deleted Huddle-wide file reaches each running Agent at its next Turn in each Room.

## Write the rules once

`AGENTS.md` holds rules in plain Markdown at three levels: all of Huddle, a Team and a Project. An
Agent reads every level that applies, and the most specific wins a conflict. Only you or an accepted
retro change writes these files.

When you edit one, running Agents hear about it at their next Turn, with no restart. A small edit that
changes nothing meaningful reaches no Agent and is listed in the Feed.

## Keep Agents up to date

**File Changes** tells an Agent at the start of each Turn which files changed in the folders it
watches: its own `work/` folder, its Teams' folders and anything you list under `watches`.

## Keep it tidy

- **`ingest`** proposes facts, Tasks and documents from new originals.
- **`wrap-up`** proposes what a session produced.
- **`brain-check`** finds drift and files it as Tasks.
- **Upkeep Jobs.** The Chief of Staff offers them, and creates each only if you say yes.
- **Overflow notices.** A Memory folder over its limit raises a daily Feed notice, and the manager
  moves old facts into `memory/archive/`.

## Archive what's finished

Archive a Team from its **Details** tab or a Project from its row. The folder moves unchanged into the
read-only **Archive**, its Rooms are archived and its Tasks leave the Boards. Huddle first says how
many open Tasks it would hide.

Old links still open the file, with an **Archived** chip, and **Unarchive** restores members, manager
and Rooms.

## Good to know

- Huddle doesn't restrict what Agents write in a Team's workspace, such as a code repository.
- Deleting a Memory file sends it to the Recycle Bin, and an Agent does this only when you ask.

## In more detail

### The full layout

```text
Teams/
  AGENTS.md     rules for every Agent
  memory/       facts every Agent keeps
  Home/                         a Team
    AGENTS.md   how this Team works
    raw/        what came in
    memory/     what lasts
    outputs/    what was made
    _tasks/     what is due (Huddle keeps these)
    Repairs/                    a Project in the Team
      AGENTS.md
      raw/  memory/  outputs/  _tasks/
```

| Folder | Who writes it | Agents read it |
| --- | --- | --- |
| `raw/` | You, by attaching | When a Message names a file, or when asked |
| `memory/` | Agents and you | Every session, as a short index |
| `outputs/` | Agents and you | Every session, as an index of "Read when…" lines |
| `_tasks/` | Huddle, through the Tasks screens | Through Task tools |
| `AGENTS.md` | You | Every session, in full |

A document built from originals ends with a **Based on** list linking each one, so you can check a summary
against its source.

### Using it day to day

| You want to | Do this | Where it ends up |
| --- | --- | --- |
| Bring in a document | Attach it in the Team's Room or to a Task | `raw/`, unchanged |
| Capture something quickly | Tell the Chief of Staff in a few words | Routed as a Task, fact or document, with a question if it can't place it |
| Have something remembered | Say "remember this" to an Agent | Its Memory, or its Team's if the whole Team should know |
| Keep a document | Ask the Agent to save it as an output | `outputs/`, with a "Read when…" line and **Based on** list |
| Set a rule | Edit the right `AGENTS.md` | Read by every Agent at that level |
| Track something with a date | Ask for a Task, or create one | `_tasks/`, on the Board |
| Make something happen regularly | Ask for a Job | The owner's `schedule.md` |
| Find something | Ask | Agents check indexes, then Memory, outputs, Tasks and originals, and say which file they used |

### Rule or fact?

Agents sort what they learn with two questions:

1. **Does it say how to behave,** with words like "always", "never" or "before X, do Y"? It's a rule, and
   belongs in `AGENTS.md`. Agents propose rules and don't write them.
2. **Is it a fact that could change?** It's Memory.

When neither settles it, the Agent asks you.

### `AGENTS.md` in depth

| Level | File | Who reads it |
| --- | --- | --- |
| Huddle-wide | `Teams/AGENTS.md` | Every Agent |
| Team | `Teams/<Team>/AGENTS.md` | Every member |
| Project | `Teams/<Team>/<Project>/AGENTS.md` | Every member |

- **Every level adds.** A Project's rules are read with its Team's, never instead of them.
- **Use the broadest level that fits.** Use a specific level to make a deliberate exception.
- **Rules about one Agent** go in its Agent file instead, and editing that restarts the Agent.
- **Team settings** such as gates and schedules live in `_team.md`, so `AGENTS.md` holds only your words.
- **Timing.** The **AGENTS.md Changes** Helper writes one summary per edit. If you save and ask straight away, a
  reply may start up to 30 seconds late. If the Helper is off, paused or slow, the Agent gets the changed lines.
- **Small edits.** An edit it judges not meaningful, at 200 changed characters or fewer, reaches no Agent. If you
  disagree, say the rule to an Agent or restart it.

### Memory files

Each file covers one topic and starts with a description of when to read it:

```markdown
---
description: When invoices go out and which template to use. Read before sending or chasing an invoice.
---

Invoices go out on the last working day of each month.
```

| Rule | Detail |
| --- | --- |
| Description | Required when an Agent writes. In an older file, the first line stands in |
| Before overwriting | An Agent must have read the current version. If you edited the file, its write is refused and it sees your version to merge into |
| Size | 16 KB per file, set by `Team:Memory:MaxFileBytes` |
| `MEMORY.md` | Reserved. Huddle builds the index itself, and a file with this name is never listed |

### What an Agent carries

Each session's prompt lists each file's name and description, cut at 200 characters.

| Index | At most | Setting |
| --- | --- | --- |
| Personal | 100 entries | `Team:FileChanges:MaxMemoryEntries` |
| Teams | 50 in total | `Team:Teams:MaxMemoryEntries` |

The `memory` tool's `list` shows every file. An Agent on an Adapter that can't read files still gets the index
and the tool.

### Team Memory proposals

A member proposes a fact, the **Team Memory** Helper decides whether to file it and where, and Huddle writes it.
The file's **History** shows who proposed it and why. A proposal it doesn't file appears in the Feed's **Helpers**
filter with the reason. The Helper reads only what a member proposed, never your Rooms or Tasks.

To add a fact yourself, open the Team's **Files** tab, select `memory`, then **New note**. To stop proposals, turn
the Helper off, and members will keep the fact in their own Memory or tell the manager.

### File Changes

An Agent hears which files were added, changed or deleted since its last Turn in that Room. The Team folders
exclude `_tasks`. This needs `Team:FileChanges:Enabled`, which is on by default, and an Adapter that can read files.

### Upkeep Jobs

| Job | What it does |
| --- | --- |
| Daily review | What's due and overdue, what's waiting on you and what was filed, with three things to do today |
| Weekly check, per Team | Finds Memory over its limit, outputs with no "Read when…" line, broken links and changed originals. It files them as Tasks and changes nothing |
| Monthly check | Lists quiet Teams and Projects, and suggests keeping or archiving each. It runs on the first Monday |

Facts moved to `memory/archive/` are kept, and no longer carried in every session.

### Teams with a workspace

A Team's workspace is a folder or repository outside Huddle. The real work follows the workspace's own conventions,
such as its own rules files and specs. The Team's `outputs/` then holds only what isn't part of that work, such as
status reports and meeting prep.

### Coming from another system

If you use Jeff Su's system or BrainOS, see the
[mapping table](../../Huddle.UserGuide.md#if-you-use-jeff-sus-system-or-brainos) in the User Guide.
