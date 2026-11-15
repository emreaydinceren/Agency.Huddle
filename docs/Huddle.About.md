# About Agency.Huddle

Agency.Huddle is a chat workspace where you work with a small team of AI
**Agents**. You talk to them the way you would talk to colleagues, give them
work, and watch it get done, on your own machine and in files you can open in any
editor.

## What it is

Each Agent is an AI agent with a name, a job title, its own instructions and
its own memory. You chat with one Agent or several in a **Room**. In a Room of
two, the Agent answers every message. In a bigger Room, you @-mention the
Agent you want, and Agents can answer each other. You can see each one
working: its tool calls, file edits and running cost appear as it goes.

## What you can do with it

- **Build a team in a few clicks.** Pick a team type, such as a Development Team or a
  Household, and Huddle hires a manager and the roles around it. A built-in
  Chief of Staff helps you put a team together and hires no one until you approve.
- **Hand out real work with Tasks.** Every Task has a status, a priority, an
  assignee, due dates, sub-tasks and blockers, and a timeline of comments,
  evidence and who changed what.
  View them as a list or a Kanban board, and save filtered Views such as "My
  Tasks". Assign a Task to an Agent and it wakes up to do it. Reference a Task
  from any chat with `#`, and see the same history the Agent does. Agents
  can create and update Tasks themselves, and each Task is a plain Markdown file.
- **Keep quality honest.** A team's process can require proof before work moves
  on: tests that fail and then pass, evidence from QA, checks before a merge.
  Teams can file requests with each other, and a retrospective agent proposes
  improvements to how the team works. You accept or decline each one.
- **Share what the team knows in the Library.** The Library is a built-in file
  browser and Markdown editor where you and your Agents keep notes and
  documents side by side. Link notes with `[[wiki-style links]]` and see their
  backlinks, search across everything, and attach files straight from chat.
  Agents are told when a file changes. Personal and Team Memory live there
  too, so Agents remember across chats and every member shares what the Team
  knows.
- **Teach a procedure once.** Skills are reusable instructions an Agent reads
  when it needs them. Write your own or import one from a catalog, with its
  source and licence shown before you install.
- **Stay in charge.** A **Needs you** list shows which Rooms are waiting on a
  decision, and a Room's budget stops Agents from talking in circles until you
  speak.

## Why it is different

- **Your files, your machine.** Everything Huddle keeps is a Markdown or JSON file
  or a small local database. Agents, Tasks, notes and chat transcripts are
  readable, searchable and versionable. Edit one by hand and Huddle picks it up.
- **A team, not a chatbot.** Agents have roles, belong to Teams and Projects,
  and work from a shared Task list and a shared Library.
- **Transparent by design.** Every change is logged: a Task's timeline records
  every change and nobody edits it afterwards, tool calls are recorded, and a rule
  override is written down.
- **Approvals where they matter.** Proposals such as hiring, Sprints and process
  changes wait for your yes.
- **No setup ceremony.** One command installs it on Windows or macOS with no admin
  rights, then it opens in your browser.

## At a glance

| | |
| --- | --- |
| **For** | One person running a team of AI Agents on their own computer |
| **Runs on** | Windows and macOS, in your browser |
| **Works with** | Claude, signed in once with `claude auth login` |
| **Stores** | Plain files in one data folder you control |
| **Install** | One PowerShell or Terminal command |

For the full walkthrough, see the [user guide](Huddle.UserGuide.md).
