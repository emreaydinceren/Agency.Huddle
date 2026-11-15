# Tasks

A **Task** is a piece of work you and your Agents can track together. Each one is a Markdown file
you own. For step-by-step help, see the
[User Guide](../../Huddle.UserGuide.md#tracking-work-with-tasks).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Track work your way

Every Task has a status, priority, assignee, tags, start and due dates, a description and a Change
log. It can also have a parent, blockers and a duplicate-of link.

The statuses are **Backlog**, **To Do**, **In Progress**, **Review**, **Needs you**, **Done**,
**Cancelled**, **Duplicate** and **Rejected**.

## See it as a List or a Board

Open **All Tasks**, **My Tasks** or any View you've saved, then shape it:

| Control | What it does |
| --- | --- |
| **Filter** | Narrows by Team, Project, Assignee, Sprint, Status, Priority or Blocked |
| **Group by** | Splits the view into groups |
| **Fields…** | Reorders columns |
| **Dense** | Fits more on the screen |

Changes save to the View as you make them, so there is no Save button. Dates read naturally, such as
"Today" or "Fri", and overdue dates stand out with a warning icon.

## Read and edit

Click a row or card to read the Task in the Right sidebar. **Edit** opens it in its own tab, with a
Markdown toolbar for the description. **New task** and **Make a copy** use the same page.

You can also edit the file by hand. Huddle notices and logs the change, and two people editing the
same Task get a field-by-field merge.

## Let Tasks wake Agents

- **A change wakes the Agent it concerns.** It gets a Message in the Task's Room, and changes within
  5 seconds are combined.
- **A blocked Task wakes its assignee** once its last blocker finishes (Done, Cancelled, Duplicate or Rejected).
- **A blocked Task waits.** In a Team with the `blockers-done-before-start` gate, a Task cannot move to In Progress,
  Review, Needs You or Done while a blocker is open. A blocker in Needs You still blocks.
- **Runaway hand-offs stop.** Each Task allows 10 Agent-made wake-ups, then you choose
  **Allow 10 more**.

## Group work into Sprints

A Sprint is a name. Type a new one on a Task to create it. It disappears with its last Task. Views
can filter, group and sort by Sprint, using `@current` and `@none`.

## Attach files

Link a Library file to a Task, or upload one to the Task's own folder.

## Good to know

- There is no delete. **Closing** a Task moves its file to `_closed/`.
- Task IDs, such as `BUSI-0001`, are never reused.
- Some Tasks need your acceptance before they can be Done. See
  [Needs you and the Human gate](needs-you-and-human-gate.md).

## In more detail

### What's in a Task file

A Task lives at `Teams/<Team>/[<Project>/]_tasks/<ID>.md`:

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

| Field | Rules |
| --- | --- |
| `id` | A prefix from the Team's name and a number that is never reused |
| `title` | One line, 1 to 200 characters |
| `priority` | Low, Medium, High or Urgent |
| `assignee` | An Agent or you, with one at most |
| `origin` | The Room the Task was created from, if any |
| `start_date`, `due_date` | `yyyy-MM-dd` |
| `## Evidence` | Optional, and lines are only ever added |
| `## Change log` | Huddle adds lines and never edits old ones |

The Team and Project aren't fields. They come from the folder the file is in.

### Statuses and closing

| Group | Statuses |
| --- | --- |
| Open | Backlog, To Do, In Progress, Review, Needs you |
| Finished | Done |
| Won't do | Cancelled, Duplicate, Rejected |

You can move a Task from any status to any other, except that a Task under the Human gate reaches Done only
through your **Accept**. **Duplicate** requires naming the original. **Cancelled** and **Rejected** accept an
optional reason.

Closing hides a Task from active Views and doesn't change its status. Huddle never closes Tasks by itself.

### What each action does

| Action | Result |
| --- | --- |
| **New task** | A new file with a `created` line, defaulting to Backlog and Medium |
| Edit any field | One Change log line, such as `status: To Do → In Progress` |
| Drag a card | Saves immediately |
| Change Team or Project | Moves the file and logs `moved: A → B`. A new Project name creates the folder |
| **Close task** or **Reopen** | Moves the file in or out of `_closed/` |
| **Make a copy** | Opens an unsaved `Copy of …` of the Task as last saved |

If someone else changed the same Task while you were editing, Huddle merges different fields silently and
asks **Keep mine** or **Take theirs** for a field you both changed. If the Task is assigned to an Agent, the
button reads **Save & notify {Name}**.

### Who gets woken, and where

An Agent is woken with a Message in the first of these that fits: the Task's origin Room, a Room with exactly
you, the creator and the assignee, or a new Room.

Nobody is woken for their own changes, for Tasks assigned to you, or for assignees that aren't known Agents.
The Task panel warns you first, such as "Nova is offline, so this change won't reach them."

Past the 10 wake-ups, changes still save, and nobody wakes until you select **Allow 10 more**. Any change you
make resets the count, and so does a restart. A waiting hand-off stays in the Feed, with **Wake {Agent}** on
its card after a restart.

When a Task enters Review, Huddle can also wake the first member whose role covers `challenge`, if the
assignee can't review and the manager wasn't woken.

### Views in depth

| Setting | Options |
| --- | --- |
| Kind | **List** or **Board** |
| Filters | Team, Project, Assignee, Sprint, Status, Priority, and Blocked, Unblocked or All |
| **Group by** | None, Team, Project, Assignee, Sprint, or Status in a List |
| **Fields…** | Drag to reorder, switch a field off, or remove it and add it back |
| Column widths | Drag the right edge of a List column header |
| **Dense** | Compact or roomier List rows, on by default |

- **New view** creates the View at once. Rename it from its More (⋮) menu, using 1 to 60 characters.
- **Reset filters** clears the filter and search. The search text isn't saved to the View.
- **Delete view** leaves Tasks untouched.
- **Saved in `views.json`.** With a syntax error there, Huddle shows the line and column and makes Views
  read-only until you fix it.

### Editing in depth

- **Shortcuts.** The description editor has `Ctrl+B`, `Ctrl+I` and `Ctrl+E`.
- **Layout.** The edit page has two columns at about 1024 pixels wide, and one when sidebars take its room.
- **The panel.** The read-only panel can still **Accept**, **Request changes** or **Decline** a gated Task, ask
  for Proof, copy the id or link, make a copy, and close or reopen.
- **Actions.** A Task's More (⋮) menu has **Copy id** and **Copy link**.

### Editing files by hand

| When | What happens |
| --- | --- |
| While Huddle runs | It notices within half a second, logs `edited outside Huddle: <what changed>` and wakes the assignee |
| While Huddle is stopped | It notices at the next start, logs the same line and wakes no one |
| You delete the file | The Task disappears with no log entry |
| A gate refuses a status change | Huddle puts the status back, keeps the rest and logs `outside edit refused by gate` |

A file that fails to load appears under **Tasks that didn't load**, with the reason.

### Sprints in depth

`@current` means the newest Sprint, and `@none` means Tasks with no Sprint. Only you can create a Sprint by
typing a name. Agents pick from existing ones, or propose one for your approval.

### Attachments in depth

An uploaded file goes under `raw/Tasks/`. A Task that links to a file also appears in that file's
**Backlinks**.

### If something goes wrong

| Symptom | Cause and fix |
| --- | --- |
| A Task didn't wake its assignee | The assignee is you, made the change, is offline or no longer exists, or the wake budget is spent. Use **Allow 10 more**, or **Wake** on its Notification after a restart |
| A Task is missing from every View | It's under **Tasks that didn't load**, or in `_closed/` while the View shows Active |
| A Task wouldn't move, naming a gate | Add the Evidence it asks for, or use **Move anyway** in the Task panel |
| A hand-edited status went back | A gate refused it, and the Change log says so |
