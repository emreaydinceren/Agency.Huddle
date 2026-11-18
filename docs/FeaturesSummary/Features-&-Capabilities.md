# Features & Capabilities

Huddle is a workspace that runs on your own computer, where you and a team of AI Agents chat, track work
and keep shared notes. Use this page to see what it can do and find tools you may not know are there.

Each area links to a page that opens with a quick overview and ends with the full detail. For step-by-step instructions, see the
[User Guide](../Huddle.UserGuide.md).

## Get started

1. **Install** with one command, without admin rights. See [Install](../Huddle.UserGuide.md#install).
2. **Run** `huddle` to start it and `huddle stop` to stop it. Installing again upgrades and keeps your data.
3. **Set up your profile** on the first page you see.
4. **Message the Chief of Staff** and ask it to help you build a Team.

## The foundations

These are the tools you'll use every day.

| Area | What you can do | More |
| --- | --- | --- |
| **Agents** | Create AI teammates, write their instructions, and choose their model and how much they may do | [Agents](Features/agents.md) |
| **Rooms and chat** | Talk with one Agent or many, reference files and Tasks, and watch the work as it happens. A Message you sent isn't lost when Huddle restarts | [Rooms and chat](Features/rooms-and-chat.md) |
| **Teams and Projects** | Hire a whole Team from a template, organise its work into Projects, and rename or archive either | [Teams and Projects](Features/teams-and-projects.md) |
| **Tasks** | Track work on Lists and Boards, have changes wake the right Agent, and keep comments and Evidence on a Task's timeline | [Tasks](Features/tasks.md) |
| **Library** | Browse your files and keep linked Markdown notes, Obsidian-style | [Library](Features/library.md) |
| **Needs you and the Human gate** | See everything waiting on you, decide before work leaves the Team, and set how much waits for you with the Needs You level | [Needs you and the Human gate](Features/needs-you-and-human-gate.md) |
| **Second Brain and Memory** | Give Agents lasting facts, shared rules and a tidy place for everything, kept in order by a weekly Memory Tidy | [Second Brain and Memory](Features/second-brain-and-memory.md) |

## Secondary features

Reach for these as your setup grows.

| Area | What you can do | More |
| --- | --- | --- |
| **Running a Team** | Add a process, gates, digests, retrospectives and Requests between Teams | [Running a Team](Features/running-a-team.md) |
| **Scheduled Jobs** | Run an instruction on a timetable, for you or an Agent, and let an Agent set a Follow-up to come back to a conversation | [Scheduled Jobs](Features/scheduled-jobs.md) |
| **Skills and Skill Packs** | Teach Agents new abilities with folders of instructions | [Skills and Skill Packs](Features/skills.md) |
| **Finding your way around** | Search everything, and work in tabs and split panes | [Finding your way around](Features/finding-your-way.md) |
| **Feed and notifications** | Catch up on what happened without opening every Room | [Feed and notifications](Features/feed-and-notifications.md) |
| **Settings and appearance** | Pick a Theme, see your spending and change the text sent to models | [Settings and appearance](Features/settings-and-appearance.md) |
| **Remote access** | Use Huddle from your phone or another computer | [Remote access](Features/remote-access.md) |

## Things you might not know are there

- **Search from anywhere.** Press `Ctrl+K` or `/` to find Teams, Agents, files, Tasks and Messages.
  [Finding your way around](Features/finding-your-way.md#search-everything)
- **See what an Agent reads.** Its card lists every part of its next prompt and where to change it.
  [Agents](Features/agents.md#look-after-a-running-agent)
- **Wake everyone.** `@all` wakes every Agent in a group Room.
  [Rooms and chat](Features/rooms-and-chat.md#decide-who-answers)
- **Shrink a long conversation.** `@nova /compact` frees an Agent's memory, using its alias in place of
  `nova`. [Agents](Features/agents.md#look-after-a-running-agent)
- **Give an Agent a clean start.** **Start afresh** opens a new session in one Room and keeps every Message.
  [Rooms and chat](Features/rooms-and-chat.md#keep-long-chats-under-control)
- **Capture a thought quickly.** Tell the Chief of Staff, and it files the thought in the right place.
  [Agents](Features/agents.md#meet-the-chief-of-staff)
- **Run your home.** The **Household** team type handles chores, meals, school and more, and asks before
  any spend. [Teams and Projects](Features/teams-and-projects.md#start-from-a-template)
- **Move files without breaking links.** Moving a note rewrites every `[[link]]` to it.
  [Library](Features/library.md#reorganise-without-breaking-links)
- **Edit by hand.** Agents, Tasks and Jobs are plain files that Huddle picks up when you change them.
  [Tasks](Features/tasks.md#read-and-edit)
- **Change any prompt.** Every piece of text Huddle sends to a model can be edited.
  [Settings and appearance](Features/settings-and-appearance.md#change-what-huddle-says-to-models)
- **Let an Agent come back to you.** When an Agent says it will check something later, it sets a Follow-up. The
  Room shows when, with **Cancel**. [User Guide](../Huddle.UserGuide.md#when-an-agent-says-it-will-come-back-to-you)
- **Give every Agent a browser.** Each Agent has its own, with its own sign-ins. Reading runs freely, and every
  click or typed character asks you first. [User Guide](../Huddle.UserGuide.md#your-agents-browser)
- **Let a Team run its own local work.** Set a Team or Project to **Run inside the Team** and a local merge or a
  Sprint goes ahead without your Accept. A Spend cap hands the decision back to you.
  [User Guide](../Huddle.UserGuide.md#the-needs-you-level)
- **Pick up where you left off.** Messages an Agent hadn't answered when Huddle closed are answered on the next
  start. **Don't answer** calls off the ones still waiting.
  [User Guide](../Huddle.UserGuide.md#after-huddle-restarts)
- **Keep Memory tidy for you.** Weekly, Memory Tidy merges, shortens and removes stale files, copies each file first
  and lists every change in the Feed. [User Guide](../Huddle.UserGuide.md#memory-tidy)
- **Rename a Team or Project.** Use **Rename team...** or **Rename project...**. Its folder, Rooms, Tasks and links
  move with it, and Agents are told once. [Teams and Projects](Features/teams-and-projects.md)

## What Huddle doesn't do yet

These are designed but not built:

- **More Adapters:** Codex, Antigravity and Copilot, with Gemini CLI as a secondary option.
- **Further Work mode options,** beyond the ones on an Agent's card today.
- **Team Playbooks:** a Playbooks tab on every Team page that tells a manager how to run the Team and a member how to
  work in it, and a Project mission the manager plans as Tasks.

Huddle also has some deliberate limits:

- **Spending is reported, not capped.** **Settings > Spending** sets no budgets. The one cap is the optional
  **Spend cap (USD)** on a Team or Project set to **Run inside the Team**, which hands the decision back to you.
- **Teams can't be deleted in the app.** They can be archived and restored from **Archived**.
- **Merge gates aren't a security boundary.** They guard against an Agent's ordinary mistake.
