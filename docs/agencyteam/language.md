# Language

The binding glossary for `Huddle.App` and `Huddle.Contracts`. Read it before naming
anything, writing interface copy, or writing prose about this codebase. Match
this vocabulary in code and in prose. Each entry lists words to avoid; those are
not bad words, they are words that are imprecise *here*.

`Huddle.Acp` is out of scope and keeps ACP's own vocabulary; see [Two bounded
contexts](../AgencyTeam.md#two-bounded-contexts). Back to the hub:
[AgencyTeam.md](../AgencyTeam.md).

## People and processes

**Teammate**
: Any named participant in Team. Two kinds exist: the Human, and Agents. This is
  the umbrella term, and the one the `/teammates` page is named for.
: *Avoid*: user, member (that means something narrower), participant.

**Human**
: The single seeded person using the browser. One per installation, a Member of
  every Room.
: *Avoid*: user, operator, admin. In interface copy, write "you".

**Agent**
: A Teammate that is not the Human — a chat identity registered by name over the
  pipe. A Persona brought online is an Agent. The chat surface knows nothing else
  about one.
: *Avoid*: bot, assistant, AI, agent host.

**Name**
: What a Teammate is called — a display name, not a slug. Letters, digits, `_`,
  `-` and single interior spaces, at most 64 characters, opening on a letter or a
  digit. `NameRules.IsValidAgentName` is the whole rule. A Name is a wire identity
  at `hello`, the `name` field of a Persona's frontmatter, the key its **Model**
  and **Effort** rows are stored under, and a token a Mention is resolved against.
  A Persona's *filename* is none of those: it is storage, and nothing reads it.
: The rule stays narrow even though the filename justification retired with
  frontmatter identity — `MentionParser` still matches a Name character by
  character against message text, and that is what single interior spaces and the
  letter-or-digit opening are for.
: *Avoid*: id, slug, handle, username. An **id** is a different thing —
  `NameRules.IsValidId`, no spaces, used for Rooms and Messages.

**Alias**
: A second handle that resolves to the same Teammate, so `@jar` reaches
  `@Jarvis`. Required on every Persona, validated exactly like a **Name**, and
  unique across the library — no two Personas may share one, and one may not
  equal another Persona's Name. A Name always wins a tie. Aliases work wherever
  a Name does: Mentions, `/invite`, `mcp__team__invite_agent` and
  `mcp__team__create_room`.
: *Avoid*: nickname, shortname, handle on its own.

**Title**
: A Teammate's job, as free display text — `Chief of Staff` for a Persona whose
  Name is `Jarvis`. Required, but never validated as a Name, never an identity,
  and never resolved against: a Title is prose. It is the one structural
  frontmatter field `mcp__team__list_agents` still renders.
: *Avoid*: role — `role:` is an ordinary, unstructured frontmatter field and a
  different thing.

**Avatar**
: What a Teammate shows for itself — the small square beside its Name on the
  Teammates page, in the invite and new-chat lists, and against every Message it
  posts. Three ways to fill it, and a background colour that is separate from all
  three:
: **Initials** — the first and last word of the Name, as `Monogram` derives them.
  The default, and *not* a stored kind: it is what renders when neither of the other
  two is set. **Label** — up to three characters, counted as Unicode text elements so
  one emoji is one character however many code units it takes. **Image** — an uploaded
  PNG, JPEG or WebP.
: Held per Teammate in `{DataDir}/avatars.json`, keyed by **Name**, which is why the
  Human has one too and why a Message finds one from its sender. Deliberately **not**
  part of the Persona — an Avatar changes nothing about how a Teammate runs, and
  putting it in frontmatter would restart the session and lose what that Agent
  remembers ([ADR-0019](../adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md)).
: *Avoid*: icon (`Icons.Material` entries are icons), picture, profile photo, headshot.
  **Monogram** is the narrower word and stays: it is the initials *rendering*, not the
  choice.

**Team**
: A label naming a group of Teammates, listed in a Persona's `teams` frontmatter
  field. A Teammate may belong to several, or to none. A Team is a **view** —
  it groups and filters the Teammates page and the invite dialog, and nothing
  more. It is never a permission: every Agent still sees every other Agent
  through `mcp__team__list_agents`, and any Agent can be invited to any Room.
  A Team has a page at `/teams/<Team>` with Members, Files and Tasks tabs.
: A Persona's Team is **not a folder**: its Team membership is its frontmatter
  alone, and where the Persona's Teammate folder sits changes nothing.
  On disk, a Team has a folder under `Teams/` (ADR-0030) for its notes, Projects
  and Tasks; that folder never decides who is in the Team.
: **In Tasks, the opposite holds, and on purpose.** Under `Teams/`, a
  Task's top-level folder *is* its Team, and it names a Team label by convention.
  That rule applies to Tasks only and never to Personas; see
  [ADR-0025](../adr/0025-in-tasks-a-team-is-a-folder-by-convention.md).
: *Avoid*: group, squad, workspace, tenant.

**Adapter**
: An ACP agent one Persona's session runs on — a process this solution launches
  and talks to over stdio. Three exist today: `claude-agent-acp` (a Node package
  under `node_modules`, located by `AdapterLocator`, running cloud Claude),
  `agency-acp` (a .NET executable running a local model), and `mock-acp` (test
  infrastructure this solution builds, §6.10). Which one a Persona uses is part
  of the Persona, written in its `adapter:` frontmatter field. No Adapter that
  runs a real model is part of this solution.
: *Avoid*: agent, bridge, client, provider, backend, host.

**Adapter Profile**
: One configured Adapter — its stable `Id`, the command that launches it, and
  whether its App Tools are advertised to the model with the `mcp__team__`
  prefix. Configured under `Team:Acp:Adapters`; an installation that configures
  none gets exactly one, synthesised from the legacy `Command` / `AdapterPath` /
  `Args` keys, so a stock install behaves exactly as it did before Adapters were
  selectable. A Persona names a profile by `Id`; an unknown `Id` degrades to the
  first profile rather than failing.
: *Avoid*: backend, host, provider, target.

**Adapter command**
: A command an Adapter advertises for a session, sent to it as the start of a prompt. Not a
  **Skill**, which is Huddle's own folder of Markdown and not Claude's `.claude/skills/`, and not an
  **App Tool**. The Human runs one on a Teammate by Mentioning it, `@Nova /compact`, and only a command
  that the Teammate's Adapter Profile names in `Commands` is offered: the list an Adapter advertises is
  much longer, and without isolation includes the Human's own Claude Code skills. Built 2026-09-30;
  see [the Adapter commands spec](../Huddle.Commands-Specifications.md).
: *Avoid*: slash command on its own (`/invite` is Huddle's), skill, tool.

## Conversations

**Room**
: A named conversation with a fixed set of Members whose history is one
  Transcript. There is exactly one kind of Room.
: *Avoid*: channel, chat, conversation, thread. Also avoid *Direct Room* and
  *Group Room* — a Room is a Room, and behaviour follows from member count.

**Archived**
: A Room hidden from the sidebar and listed under **Archived Chats** instead.
  Reversible, and a display filter only — an Archived Room is still live, and
  Agents still post into it.
: *Avoid*: hidden, closed, muted, inactive, soft-deleted. A Room that was
  *deleted* is gone, not Archived — the two are not degrees of the same thing.
**Member**
: A Teammate that belongs to a Room and receives its Messages.
: *Avoid*: participant, subscriber.

**Invitation**
: Adding an Agent to a Room that already exists. Three doors, one behaviour:
  the Human types `/invite @name` in the composer or uses **Add teammate** on the
  Room header, and an Agent calls `mcp__team__invite_agent`. All three end in
  `ChatService.InviteAsync`, so the Room renames itself after its Agents however
  the Invitation was issued.
: *Avoid*: join, add user.

## Agents and Personas

**Persona**
: The Markdown instructions defining one Agent's character, stored as one file
  anywhere under `{DataDir}/{Acp:TeamsDir}` — enumerated recursively, so Team
  sub-folders are free to exist and mean nothing — together with the **Model**
  and the **Effort** it runs on, stored in the `persona_models` and
  `persona_efforts` tables. The file body *becomes* part of a system prompt; it
  is not one.
: Its leading YAML frontmatter block is **partly schema**. `name`, `title` and
  `alias` are required and `teams` is optional; a file missing any required one
  is not a Persona at all — it is a rejected file, listed with its reason on the
  Teammates page rather than silently ignored. Every *other* top-level field
  whose key does not start with `_` becomes one title-cased line of the job
  description `mcp__team__list_agents` shows, in file order, as before;
  `_`-prefixed fields stay reserved for future programmatic use. `name` alone is
  held back from that dump, because the bullet above it already prints the Name.
  See `PersonaFrontmatter` and `PersonaIndex`.
: *Avoid*: profile, character, role, prompt.

**Rejected file**
: A `.md` file under the Teams directory that did not become a Persona — a
  required frontmatter field missing or invalid, or a Name or Alias colliding
  with another file's. Both sides of a collision are rejected, never one
  arbitrary winner. Rejected files are surfaced on the Teammates page with their
  path and reason; being wrong is loud, not invisible. See `RejectedPersonaFile`.
: *Avoid*: invalid persona, broken persona — it is a file, and it is not a
  Persona.

**Model**
: The LLM one Persona's session runs on, chosen from the catalog the Adapter
  advertises at `session/new`. Unset means the Adapter's own default, which is
  the normal case. Fixed for the life of a session, exactly like a system prompt.
: Unchanged by a second Adapter, contrary to roadmap item 12's prediction that
  this definition "cannot survive a second backend". It survived because
  `agency-acp` **is** a process and **does** advertise its catalog at
  `session/new` — the premise that failed was "a NuGet reference", not this
  definition.
: *Avoid*: LLM, engine, backend, variant, and "Claude" — the product name is not
  the setting.

**Effort**
: How hard one Persona's session thinks, chosen from the ladder the Adapter
  advertises. Whether that ladder varies by Model is the Adapter's business:
  `claude-agent-acp` advertises one per Model; `agency-acp` advertises one per
  endpoint surface. Unset means the default, which is the normal case. Fixed for
  the life of a session, exactly like a Model and a system prompt.
: *Avoid*: thinking level, reasoning level, thought level. Not **Budget**
  either — that is a defined term meaning something else entirely, and Effort is
  not one: it buys no allowance and is not spent.

**Turn**
: One prompt-to-completion cycle on a session — since Room Sessions shipped, one
  **Room Session's** session. Load-bearing: `IAgentSession.PromptAsync` throws if
  a turn is already in flight, which is why each Room Session's own work queue is
  mandatory rather than an optimisation; `RoomSessionPool`'s `TurnGate` then admits
  Turns across a Persona's Room Sessions in ticket order, so cross-Room arrival
  order survives even though `MaxConcurrentTurns` defaults to 1 (RS §6.2, finding P-4).
: A Turn ends one of **four** ways, not three - **completed**, **stopped** by the
  Human, **timed out**, or **failed** - and only the last two are faults. The
  distinction is load-bearing in several files, because the mechanism underneath
  both a Stop and a timeout is a `CancellationToken` and everywhere else here that
  means shutdown; see [Rules](rules.md) for the two traps that tell them apart.
: *Avoid*: request, exchange, round.

**Plan**
: An Agent's own checklist for one Turn, replaced whole each time it changes. Each line is a
  **Plan entry** with a status and a priority. Private to the Turn: unshared, unassigned and never
  a file. Nothing sends one today, because the Claude Adapter's session has no todo-list tool.
  Proposed, not built.
: Never a **Task**, which has the same fields and is shared, persisted and assigned.
: *Avoid*: task, to-do, step list.

**Thinking**
: The reasoning text an Adapter streams before its reply, to be shown collapsed in Turn detail.
  Nothing sends it at the default **Effort** today. Proposed, not built.
: *Avoid*: thought, reasoning, chain of thought. "Thought level" is avoided under **Effort**, and
  that names a setting, not this.

**App Tool**
: A tool named by us whose body runs inside `Huddle.App`, offered to a session over
  MCP. This is what lets an Agent genuinely create a Room without reaching into
  the database.
: *Avoid*: MCP tool (that is the transport), function, plugin.

**Reply Gate**
: The client-side rule deciding whether an Agent answers a Message: always in a
  Room of two Members, when Mentioned in a Room of three or more, and whenever it
  is Following that Room — and never once the Room has spent its Budget, which is
  checked first, so neither a Mention nor Following buys a Turn past the cap.
: *Avoid*: policy, filter, trigger.

**Following**
: An Agent's standing request to be woken by every Message in one Room, Mentioned
  or not, made with `follow_room` and withdrawn with `unfollow_room`. A property of
  one Agent in one Room, never of a Persona across all of them, and never of the
  Room itself. It is held in memory, so it does not survive that Agent restarting.
: *Avoid*: subscribe, subscriber, watch, listen, observer.

**Budget**
: How many agent-authored Messages a Room may take between one Human Message and
  the next. Any Human Message in that Room resets it, so a Budget caps one
  unattended run rather than the Room. A spent Room stops accepting agent
  Messages and asks the Human, who may grant one more Budget at a time. The
  per-Persona token Budget is the same word over a different unit — tokens rather
  than Messages, and per Persona, summed over its Room Sessions, rather than per
  Room: it exists to catch a loop that mints fresh Rooms, which a per-Room Budget
  cannot, and a per-Room one would reset with every fresh Room (RS D-10). See
  ADR-0006.
: *Avoid*: quota, limit, cap, rate limit, throttle, allowance.

**Spend**
: What the Adapter has reported spending for one Teammate since the app started, shown in money on
  its card. Display only: it informs and never limits, which is what a **Budget** is for, and it is
  lost on restart. Built 2026-09-30.
: *Avoid*: cost in interface copy, bill, and **Budget** for this.

**Catch-up**
: The Messages an Agent missed in a Room while unmentioned, carried along the
  next time it is Mentioned there. A Room Session's **first** Turn instead carries
  what it has not seen read straight from the Transcript — through the additive
  `ReadTranscript`/`TranscriptTail` Envelope pair — ending strictly before the
  Message that started the Turn, never up to Turn start, so a queued panellist
  never sees another's answer (RS §6.5, D-7). Every Turn after a Room Session's
  first reuses the in-memory buffer, as before.
: *Avoid*: backlog, history.

**Watched Folder**
: A folder whose files an Agent is told about when they change. Every Agent
  watches its own Work Dir. A Persona's `watches` frontmatter adds more, and so does
  the Agent itself with `watch_folder`, which lasts until `unwatch_folder`, even
  across a restart. Each is named by a Teammate's Name, meaning their Work Dir, or
  by a folder inside `DataDir`. **Delivered 2026-09-23** — see
  [ADR-0023](../adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md).
: Not **Following**, which is about Rooms and wakes the Agent. Watching a folder
  never wakes anyone.
: *Avoid*: subscription on its own, monitored folder, share.

**File Changes**
: The files added, changed or deleted in an Agent's Watched Folders since its
  previous Turn **in that Room**, listed by full path at the top of its next Turn's
  prompt, ahead of any Catch-up. Names only: the Agent reads what matters with the
  Adapter's own tools. Worked out by comparing each folder with the baseline that
  Room last saw, saved per Room in `{DataDir}/file-state/<Name>.json`. An edit is
  listed in every Room except the one it was made in, so Nova's addition to its own
  memory in one Room is news in its others. **Delivered 2026-09-23.**
: *Avoid*: notification, alert, event, diff.

**Memory**
: What an Agent deliberately keeps for itself going forward: one Markdown file per
  fact in its Work Dir's `memory/` folder, written with its own tools, the fact on
  the first line. It belongs to the Agent, not to a Room. Every new session, after a
  restart or in another Room, starts with an index of it in the system prompt, and
  File Changes carries later edits to its other Rooms, marked *by you, in Room 'X'*
  when another copy of it wrote them. **Delivered 2026-09-23** — see
  [ADR-0023](../adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md). Depends on
  isolating a Persona's session from the Human's own Claude Code settings, which is
  unverified live — see [Known limits](known-limits.md).
: Capitalised, it means only this. Claude Code's own auto-memory is a different
  thing, and Personas should not use it.
: *Avoid*: notes, knowledge base, scratchpad, context.

**Team Memory**
: What a Team keeps going forward, shared by every member: one Markdown file per
  fact, the fact on the first line, in `Teams/<Team>/memory/` for the whole Team or
  `Teams/<Team>/<Project>/memory/` for one Project. Every member's new session
  starts with an index of it after its own Memory; later edits arrive as File
  Changes. **Delivered 2026-09-29** — see
  [ADR-0032](../adr/0032-a-team-and-each-project-share-a-memory-folder.md).
: Never just *Memory*, which stays the Agent's own.
: *Avoid*: shared memory, project memory, team notes.

**Context only**
: What a delivery amounted to when it named no Teammate the Reply Gate would wake:
  the Message was read as context and no Turn began. A property of the delivery, the
  same way **Mentioned** is — never of the Message, which may be context only for one
  Teammate and a Mention for another. It is what the Room view says, and it instructs
  rather than predicts, because the gate is permission and not obligation.
: *Avoid*: ignored, dropped, unread, silent.

**Progressive discovery**
: The arrangement where the system prompt names one App Tool —
  `mcp__team__get_help` — and that tool names the rest. Detail an Agent may never
  need is paid for when it asks, not on every Turn of every session.
: *Avoid*: lazy loading, tool discovery (that is MCP's own `tools/list`).

**Skill**
: A named folder of Markdown under `{DataDir}/Skills/` that teaches an Agent one
  kind of work. Its name and description sit in the system prompt of every
  Persona assigned it through the `skills` frontmatter field; its body is read
  with `read_skill` only when the conversation calls for it. Progressive
  discovery, applied to know-how rather than tools. A Skill is text and never
  executes; the App Tools it names do. See
  [ADR-0021](../adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md).
: *Avoid*: ability, capability, plugin, playbook, recipe. Not Claude's own
  `.claude/skills/`, which only one Adapter honours.

**Proposal**
: A roster of new Teammates an Agent asks the Human to create, made with
  `propose_teammates` in one Room and shown there with Approve and Decline. An
  Agent never creates a Teammate; the Human's Approve does, and the outcome is
  posted as a Message from the Human, which wakes the proposer. At most one per
  Room, held in memory, so a restart loses it.
: Each proposed Teammate in it is a **Candidate** — never a *draft*, which is
  already the Turn text shown before it becomes a Message.
: *Avoid*: request, application, pending Teammate, draft.

**Question**
: One multiple-choice question an Agent puts to the Human with `ask_human`: a line
  of text, two to four short options, and whether the Human picks one, picks any,
  or ranks them. An Agent may ask up to three at once; they wait together on one
  card in the Room and are answered together, as a Message from the Human that
  quotes each Question and Mentions the asker. Only the Human is ever asked. At
  most one card per Room, held in memory, and dropped by any typed Human Message
  there. Proposed, not built — see
  [ADR-0022](../adr/0022-an-agent-asks-the-human-with-a-question.md).
: Capitalised, it means only this. An ordinary question in a Message stays lower
  case.
: *Avoid*: poll (one person answers), prompt (a defined term for model-facing
  text), form, survey, quick reply.

**Greeting**
: The first Message the built-in Chief of Staff posts to a new Human, unprompted,
  when its Room with the Human has no Messages. The one Turn that no delivered
  Message starts; the instruction for it is a Prompt, never a Message posted as
  the Human. See
  [ADR-0021](../adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md).
: *Avoid*: welcome — that is the pipe handshake Envelope — and intro, onboarding
  message, first-run message.

**Work Dir**
: The per-Persona working directory handed to the agent process as its `cwd`,
  under `{DataDir}/{Acp:TeammatesDir}/<Name>/work/`.
: *Avoid*: sandbox — it is not a jail, and the old name implied one.

**Room Session**
: The session one Agent holds for one Room: that Room's conversation and nothing
  else. It opens lazily on the Room's first Turn (except the Room with the Human,
  which opens at start), closes when idle or evicted LRU past `MaxLiveSessions`,
  and is resumed by a stored id where the Adapter advertises resume — else opened
  fresh with Transcript Catch-up. The Persona text, Work Dir, Memory, App Tools and
  Adapter process are per Persona, and all of a Persona's Room Sessions share them;
  only working context is per Room. **Delivered as code 2026-09-23** — see
  [ADR-0024](../adr/0024-an-agent-holds-one-session-per-room.md), now Accepted, and
  [roadmap item 18](roadmap.md#18-one-session-per-room--delivered-code-2026-09-23).
  Default (`SessionPerRoom: true`); an Adapter Profile can still set
  `SessionPerRoom: false` for one shared session across every Room, with a
  truthful Prompt of its own (RS §6.9, §6.12) — required until Appendix B's V-5
  passes for `agency-acp`. Paid checks proving this live have not yet been run —
  see [Known limits](known-limits.md).
: *Avoid*: conversation, thread, agent session, instance.

## Library

**Library**
: The explorer, viewer and editor as a whole.

**Library Root**
: One top-level folder the Library may reach, with an id: `teams`, `teammates`,
  or a pinned root's slug.

**Library Pane**
: The docked panel: the tree above, the open document below or beside it.

**Team folder**
: `{DataDir}/Teams/<Team>/`, a Team's folder of notes, Projects and Tasks.

**Project**
: A direct sub-folder of a Team folder; the same Project Tasks files into. A folder
  whose name starts with `_` or `.`, or is `memory` (Team Memory, proposed in
  ADR-0032), is never a Project.

**Teammate folder**
: `{DataDir}/Teammates/<Name>/`: the Teammate's definition and its `work/` Work Dir.

**Pinned root**
: A Library Root the Human added in configuration or Settings.

## Tasks

See
[Huddle.Tasks-Specifications.md](../Huddle.Tasks-Specifications.md),
[ADR-0025](../adr/0025-in-tasks-a-team-is-a-folder-by-convention.md) and
[ADR-0026](../adr/0026-a-change-to-a-task-wakes-its-assignee.md).

**Task**
: A unit of work that the Human and the Agents share. It is one Markdown file at
  `{DataDir}/{Team:Teams:Dir}/<Team>/[<Project>/]_tasks/[_closed/]<ID>.md`: a title, a status,
  a priority, at most one assignee, a description, and a Change log. Its identity is the `id`
  in its frontmatter, such as `PLAT-0042`, and it never changes, even when the Task moves. In
  code the record is `TaskItem`, because a type named `Task` would shadow
  `System.Threading.Tasks.Task`.
: Capitalised, it means only this. A task in the ordinary sense stays lower case.
: Not a **Plan entry**: an Agent's own checklist for one Turn has a status and a priority too,
  but it is private, unassigned and never a file.
: A Task is **referenced** in chat by writing its plain id. Rendered Messages turn
  an id that matches a real Task into a link. The copy button and the composer's
  `#` picker both produce exactly the id, with no marker kept.
: *Avoid*: ticket, issue, work item, card (a card is how a Board draws a Task),
  todo; and for the reference, tag or hashtag (the `#` isn't kept).

**Project**
: An optional grouping of Tasks inside one Team: the folder below the Team's
  folder. Its name is unique only within its Team, so write it as
  *Team / Project* wherever the Team isn't clear.
: *Avoid*: epic, milestone, board, workspace.

**Closed**
: A Task that has been put away. Its file sits in a `_closed/` folder, and it is
  hidden until the Human looks at Closed Tasks. Closing is separate from status.
  It doesn't require a terminal status, and a Closed Task can be **Reopened**. In
  the interface the verbs are **Close** and **Reopen**.
: *Avoid*: archived, and archive (**Archived** belongs to Rooms, where it means
  hidden but still live), done (that is a status), resolved, deleted.

**Won't do**
: The three terminal statuses other than Done: **Cancelled**, **Duplicate** and
  **Rejected**. On a Board they share one column. Dropping a card there shows one
  drop target per status, called a **ghost bucket**. A Duplicate names the Task
  it duplicates.
: *Avoid*: invalid, won't fix, discarded, abandoned.

**Change log**
: The history at the end of a Task file, under a `## Change log` heading. Only
  the app writes to it, and only by appending. Each line records when, who, and
  what changed. It is the Task's only history: the Created, Updated and Closed
  dates are derived from it. This is the name in the interface as well as in the
  docs.
: *Avoid*: activity, history, audit log, timeline (the component that draws it).

**Origin**
: The Room a Task was created from, if it came from a conversation. When the
  assignee is woken, the wake-up goes there first.
: *Avoid*: source, parent Room, home Room.

**Wake**
: What a change to a Task does to its AI assignee. A Message listing the change
  is posted into a Room with the assignee Mentioned, which starts the assignee's
  Turn through the ordinary Reply Gate. The actor posts it: the Human for the
  Human's changes, the Agent for an Agent's. Nobody is woken when the assignee is
  the Human, is the actor, or when the Task's **wake budget** is spent. The wake
  budget is a per-Task count of wake-ups caused by Agents; the Human grants more.
  See [ADR-0026](../adr/0026-a-change-to-a-task-wakes-its-assignee.md).
: *Avoid*: notify (except in button text such as *Save & Notify Nova*), ping,
  trigger, alert, nudge.

**Awake / Asleep / Offline**
: An AI Teammate's presence as the Tasks interface shows it. **Awake** means a
  Turn is running. **Asleep** means the Persona is running with no Turn in
  progress, and can be woken. **Offline** means the Persona isn't running; a
  wake-up Message is still posted, but the Teammate doesn't receive it.
: *Avoid*: idle, busy, online (that is `PersonaState`'s word, which these three
  refine), available.

**View**
: A saved way of looking at Tasks, stored in `{DataDir}/views.json`. It has a
  name, a kind (**List** or **Board**), Active or Closed, the fields shown,
  filters, grouping, sort and, for a Board, columns. *All Tasks* and *My Tasks*
  are built in. Views belong to the Human; Agents use `list_tasks`.
: *Avoid*: filter (a filter is one part of a View), query, saved search,
  perspective, dashboard.

**Board**
: A View that draws Tasks as cards in columns of statuses, and changes a status
  when a card is dragged. Each column holds one or more statuses, and every
  status is in exactly one column. Always shows Active Tasks.
: *Avoid*: kanban in interface copy (fine in prose about the design), swimlane
  board, sprint board.

## Messages and storage

**Message**
: One persisted unit of text posted to a Room by a Member, identified by an id
  and a timestamp.
: *Avoid*: post, chat line, event.

**Mention**
: An `@name` token in a Message that resolves to a Member of that Room. Because a
  Name may contain spaces, a Mention has no self-evident end: it is resolved by
  matching the Room's Member Names against the text, longest first.
  **Mentioned** is the per-delivery flag saying a given Agent appears in them —
  a property of the delivery, not of the Message.
: *Avoid*: tag, ping, callout.

**Envelope**
: One JSON line on the pipe carrying exactly one protocol message.
: *Avoid*: packet, frame, payload (that is the inner object).

**Draft**
: The text of a Turn in progress, and its **Turn detail**, shown in the Room before it becomes a
  Message. In memory only - never written to the Transcript, lost on restart, and capped so
  one runaway Turn cannot grow a Singleton without bound. Held by `Drafts`, keyed
  by the Message id the Turn will post under, because two Agents can stream into
  one Room at once.
: *Avoid*: partial, streaming message, preview, buffer, and **delta** - that is the
  Envelope that carries one, not the thing itself.

**Turn detail**
: What a Draft shows beside its text while a Turn runs: the Agent's most recent tool calls, each
  with a status and, for an edit, an **Edit preview**. In memory only, at most six calls per Draft,
  and gone when the Draft is. It is never written to the Transcript, so scrollback shows what an
  Agent said and not what it did ([ADR-0008](../adr/0008-a-turn-is-visible-stoppable-and-says-when-it-fails.md)).
  Built 2026-09-30 — see [the Turn detail spec](../Huddle.TurnDetail-Specifications.md).
: *Avoid*: progress, trace, activity (avoided under **Change log**).

**Edit preview**
: The change one tool call makes to one file — the path, the line when known, and the text removed
  and added — shown in a Draft's Turn detail and opened by the Human in place. Display only: it is
  what the Adapter described, clipped to a fixed length, and Huddle never computes, applies or
  reverts it. Built 2026-09-30.
: *Avoid*: diff (avoided under **File Changes**, which are names only), patch, change list.

**Stop**
: The Human ending a Turn in progress, in one Room. It ends that Room Session's
  live Turn and discards whatever was queued behind it there, so it means *this
  Agent, in this Room, now* rather than *this one Turn*. A normal outcome, not a
  failure: nothing is posted, no badge changes, and the Room stays usable. Per
  Room in both modes — `SessionPerRoom: false`'s one shared session still only
  marks and cancels the stopped Room's own queued work, never another Room's
  (RS §6.8, P-6, D-9). `Chat.razor` already sends one Stop per Agent per Room.
: *Avoid*: cancel - that is ACP's own verb and stays inside `Huddle.Acp` - abort,
  kill, interrupt, pause (pausing a Room is a Budget of zero, which is a different
  thing).

**Transcript**
: The append-only JSON Lines file holding all Messages of one Room, under
  `{DataDir}/rooms/`.
: *Avoid*: log, history file.

**Team Directory**
: The SQLite-backed record of Humans, Agents, Rooms and Members. Named
  `ITeamDirectory` rather than `IDirectory` so it never reads as a filesystem
  directory.
: *Avoid*: database, registry, store.

## Model-facing text

**Prompt**
: One named piece of text this application sends to a model — a system-prompt
  block, a Turn's framing, a `get_help` section, a tool's own description.
  Twenty-two exist. `PromptCatalog` holds every default in code; `prompts.json`
  holds overrides only; `IPromptSource` resolves one over the other per key.
  Named **Hook** until 2026-09-22 — that word read as an executable extension
  point, which is exactly the wrong idea; see
  [ADR-0020](../adr/0020-a-hook-is-a-prompt.md).
: A Prompt is a **template, not an event**: nothing executes, nothing
  subscribes, and the order blocks compose in is fixed in code. See
  [ADR-0007](../adr/0007-model-facing-text-is-configuration.md).
: *Avoid*: hook, template, prompt fragment, snippet, setting. Never "event" or
  "handler" — those promise behaviour a Prompt does not have.

**Placeholder**
: A `{{name}}` token inside a Prompt's text, substituted by code at render time.
  `{{…}}` and not `<…>` because `get_help` sends the model the literal line
  `"[Room: <name> (id: <id>)]"` as documentation, which an angle-bracket syntax
  would silently eat. An unknown token is left verbatim rather than blanked, so a
  typo shows up in the prompt instead of quietly erasing a paragraph.
: *Avoid*: variable, token, parameter, slot.

**Default**
: A Prompt's shipped wording, held in `PromptCatalog` in code. `prompts.default.json`
  beside the binary is *generated* from it, never the source of it — so deleting
  every file still leaves the application running on exactly the text it shipped
  with. Distinct from **stored** (what `prompts.json` currently resolves to) and
  **pending** (typed on the settings page, not yet saved); the three are separate
  on purpose, and Reset is the case that proves it.
: *Avoid*: original, factory setting, baseline.

**Timing**
: Whether an edit to a Prompt reaches a model on the next Turn (`Live`) or only
  for Teammates started afterwards (`NextSession`). Not a preference — a system
  prompt is fixed at `session/new` and there is no later event that re-reads it.
  Editing a Prompt never restarts a session; see [Rules](rules.md).
: *Avoid*: scope, refresh, reload.

## Appearance

**Theme**
: One `MudTheme` object in `ThemeCatalog`, carrying **one** palette and the
  `ThemeDescriptor.Mode` naming which — so a Theme *is* a light one or a dark one rather
  than spanning both, and choosing it chooses the mode
  ([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). It fills only that slot;
  the other keeps MudBlazor's defaults and is never rendered. Nineteen ship built in:
  this application's own `huddle` (labelled "Huddle Light") and `huddle-dark`, and
  seventeen of the colour Themes bundled with Visual Studio Code, converted once at
  authoring time into `MudTheme` objects under `Themes/VsCode/` — see
  [ADR-0016](../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md).
  A Theme also carries a **`ThemeGroup`** — Light, Dark or High contrast — which is the
  heading the picker lists it under and is *not* its Mode: "Dark High Contrast" is a dark
  Theme in the High contrast group.
  A Theme is identified by its **id**, which is what
  `appearance.json` stores; never by its display label, for the reason
  [Rules](rules.md) gives for a Model. Roadmap item 7 is what remains: importing an
  *arbitrary* Theme a Human supplies, mapped from JSON into a `MudTheme` at run time
  rather than generated as CSS. The bundled Themes needed no importer — they were
  converted once, by hand, and are ordinary C#.
: *Avoid*: stylesheet (a Theme stopped being one on 2026-09-14 — see
  [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md)), skin, palette (that is one
  half of a Theme, and a MudBlazor type name), colour scheme (`color-scheme` is a CSS
  property here and no longer carries anything).

**Palette property**
: One named colour a Theme sets — a property on MudBlazor's `Palette`, surfaced to CSS
  as a `--mud-palette-*` custom property. **This replaces the word Token**, which named
  the 35 colours and 4 typography values of the retired `wwwroot/theme.css` and should
  not be reused for this: the old Token list was ours and complete, a palette property
  is MudBlazor's and is not.
: *Avoid*: token (retired 2026-09-14), variable, custom property (that is the CSS
  mechanism), setting.

**Appearance**
: The Appearance tab of `/settings`, and `{DataDir}/appearance.json` behind it: the
  selected Theme id, and nothing else. Per installation, hand-editable and watched,
  exactly like `hooks.json`. There are no per-property overrides — that layer was removed
  with the Tokens — and since 2026-09-21 there is no light/dark preference either: a
  Theme carries its own palette, so selecting one selects the mode
  ([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). A file written before then
  still holds a `dark` key; it is an unknown key now, ignored and kept. Appearance is the
  choice; a **Theme** is what it selects.
: **The tab is not the file.** Since 2026-09-22 the tab has a second section — the
  Human's own **Avatar** — because the Human has no Persona file and so no Teammate
  card to edit one on. That Avatar is stored in `avatars.json`, a different file with a
  different owner; `appearance.json` still holds the selected Theme id and nothing else.
: *Avoid*: dark mode and light/dark preference (there is no such setting any more — say
  *a dark Theme*), display settings.
