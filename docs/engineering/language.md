# Language

The binding glossary for `Huddle.App` and `Huddle.Contracts`. Read it before naming
anything, writing interface copy, or writing prose about this codebase. Match
this vocabulary in code and in prose. Each entry lists words to avoid; those are
not bad words, they are words that are imprecise *here*.

`Huddle.Acp` is out of scope and keeps ACP's own vocabulary; see [Two bounded
contexts](../Huddle.EngineeringGuide.md#two-bounded-contexts). Back to the hub:
[Huddle.EngineeringGuide.md](../Huddle.EngineeringGuide.md).

## People and processes

**Human**
: The single seeded person using the browser. One per installation, a Member of
  every Room.
: *Avoid*: user, operator, admin. In interface copy, write "you".

**Agent**
: A named chat identity that is not the Human, registered by name over the
  pipe. A Persona brought online is an Agent. The chat surface knows nothing else
  about one, and the `/agents` page is named for it. Where text means the Human
  and Agents together, say "participants" or "the Human or an Agent".
: *Avoid*: teammate, team member, bot, assistant, AI, agent host.

**History** (of a document)
: The entries a Markdown document's editors logged, oldest first on disk and newest first on screen. An Agent adds to
  it by calling `log_change`; Huddle adds the Human's saves. It is what the editors reported, not a record Huddle
  verified ([ADR-0060](../adr/0060-a-documents-history-is-what-its-editors-report.md)).
: *Avoid*: revision history, version history, audit log, changelog: none of those is a copy of the document.

**Byline**
: The one line under a document's toolbar that summarises its History: who wrote it, who edited, reviewed and
  objected to it, and when it last changed.
: *Avoid*: author line, credits.

**Name**
: What an Agent is called — a display name, not a slug. Letters, digits, `_`,
  `-` and single interior spaces, at most 64 characters, opening on a letter or a
  digit. `NameRules.IsValidAgentName` is the whole rule. A Name is a wire identity
  at `hello`, the `name` field of a Persona's frontmatter, the key its **Model**,
  **Effort** and **Work Mode** rows are stored under, and a token a Mention is resolved against.
  A Persona's *filename* is none of those: it is storage, and nothing reads it.
: The rule stays narrow even though the filename justification retired with
  frontmatter identity — `MentionParser` still matches a Name character by
  character against message text, and that is what single interior spaces and the
  letter-or-digit opening are for.
: *Avoid*: id, slug, handle, username. An **id** is a different thing —
  `NameRules.IsValidId`, no spaces, used for Rooms and Messages.

**Alias**
: A second handle that resolves to the same Agent, so `@jar` reaches
  `@Jarvis`. Required on every Persona, validated exactly like a **Name**, and
  unique across the library — no two Personas may share one, and one may not
  equal another Persona's Name. A Name always wins a tie. Aliases work wherever
  a Name does: Mentions, `/invite`, `mcp__team__invite_agent` and
  `mcp__team__create_room`.
: *Avoid*: nickname, shortname, handle on its own.

**Title**
: An Agent's job, as free display text — `Chief of Staff` for a Persona whose
  Name is `Jarvis`. Required, but never validated as a Name, never an identity,
  and never resolved against: a Title is prose. It is the one structural
  frontmatter field `mcp__team__list_agents` still renders.
: *Avoid*: role — `role:` is an ordinary, unstructured frontmatter field and a
  different thing.

**Avatar**
: What an Agent shows for itself — the small picture beside its Name on the
  Agents page, in the invite and new-chat lists, and against every Message it
  posts. Three ways to fill it, and a background colour that is separate from all
  three:
: **Initials** — the first and last word of the Name, as `Monogram` derives them.
  The default, and *not* a stored kind: it is what renders when neither of the other
  two is set. **Label** — up to six characters, counted as Unicode text elements so
  one emoji is one character however many code units it takes (the 24px row avatar draws
  only the first three). **Image** — an uploaded
  PNG, JPEG or WebP.
: Held per Agent in `{DataDir}/avatars.json`, keyed by **Name**, which is why the
  Human has one too and why a Message finds one from its sender. Deliberately **not**
  part of the Persona — an Avatar changes nothing about how an Agent runs, and
  putting it in frontmatter would restart the session and lose what that Agent
  remembers ([ADR-0019](../adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md)).
: *Avoid*: icon (`Icons.Material` entries are icons), picture, profile photo, headshot.
  **Monogram** is the narrower word and stays: it is the initials *rendering*, not the
  choice.

**Avatar Library**
: A folder of ready-made Avatar images the Human maintains, configured by `Team:AvatarLibrary:Dir` and read through its `master.json` index. The Human chooses an image from it with the picker. Read-only from Huddle's side: a pick **copies** the image into the Avatar store, so an Agent keeps its face if the library moves or is deleted ([ADR-0044](../adr/0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md)).
: *Avoid*: gallery, stock images, catalogue (as the name of the folder; the parsed index in memory is the catalogue).

**Team**
: A label naming a group of Agents, listed in a Persona's `teams` frontmatter
  field. An Agent may belong to several, or to none. A Team is a **view** —
  it groups and filters the Agents page and the invite dialog, and nothing
  more. It is never a permission: every Agent still sees every other Agent
  through `mcp__team__list_agents`, and any Agent can be invited to any Room.
  A Team has a page at `/teams/<Team>` with Members, Files and Tasks tabs, and a
  Process tab once it has a **Process**. A Team set up from a **Team type** also
  has a **Team card** (`_team.md`) that names its type, manager and workspace.
: A Persona's Team is **not a folder**: its Team membership is its frontmatter
  alone, and where the Persona's Agent folder sits changes nothing.
  On disk, a Team has a folder under `Teams/` (ADR-0030) for its notes, Projects
  and Tasks; that folder never decides who is in the Team.
: **In Tasks, the opposite holds, and on purpose.** Under `Teams/`, a
  Task's top-level folder *is* its Team, and it names a Team label by convention.
  That rule applies to Tasks only and never to Personas; see
  [ADR-0025](../adr/0025-in-tasks-a-team-is-a-folder-by-convention.md).
: *Avoid*: group, squad, tenant.

**Team type**
: A kind of Team, defined as data: a folder with a `team-type.json` manifest, its
  **Roles** (each naming a **Job** of the **Hiring pool**), **Packs** and **Processes**.
  Twenty-six types are built in, from *Development Team* and *Household* to *Home Lab*; a
  user folder with the same id replaces a built-in one. Code knows no role name (ADR-0038, ADR-0049).

**Hiring pool**
: The one global catalog of **Jobs** (`hiringpool.json` and a persona file per job) that
  team types draw their roles from. A job has a title, description, tags, suggested
  skills, the team types it suits and a persona; the pool browses by two tag clouds.
  See the [Hiring Pool specification](../Huddle.HiringPool-Specifications.md).
: *Avoid*: template, kind, blueprint.

**Role**
: A job slot a **Team type** defines, such as Developer or Reviewer. An Agent has
  one Role, named by its `_role` frontmatter key; a Team type's **coverage** says which steps its
  hired Roles cover.
: *Avoid*: position, seat, job.

**Candidate**
: An Agent template for one Role, read from a Team type's `candidates/*.md`. The
  hiring pool shows one card per Candidate; hiring a card writes an Agent.
: *Avoid*: template, profile, persona file.

**Pack**
: A named set of Skills that an Agent's card adds or removes as one chip: a `.skillpack.json` file
  that a row of the Packs table in `SKILLS.md` names. An AI tool writes it; Huddle reads it. A pack
  can also say when to consult the Agent, such as build failures. Huddle ships none. See
  [the Skill Packs design](../Huddle.SkillPacks-Specifications.md).
: *Avoid*: bundle, plugin, Skill pack in the old sense of a JSON file in `SkillPacks/`.

**Skill Sources folder**
: The folder that holds `SKILLS.md`, the Pack and catalog files and one folder per **Source**:
  `{DataDir}/SkillSources` unless `Team:SkillSourcesDir` or Settings › Skills › Sources moves it.
  The AI tool owns what is in it, and several Huddles may share one. Never confuse it with
  `{DataDir}/Skills/`, which holds the copies Agents read.
: *Avoid*: registry folder, skills repo, library.

**Source**
: Where a set of Skills came from: a GitHub repository, another git remote, or a local folder,
  named by an id such as `github.com/anthropics/skills` or `local/my-packs`. Its folder under
  `sources/` holds `source.json` (URL, commit, when and by what it was fetched, and a status:
  Active, Deprecated, Withdrawn or Moved), `NOTES.md` and Huddle's `log.jsonl`.
: *Avoid*: origin, provider, publisher, repo (a Source need not be a repository).

**Catalog**
: In the Skill sense, a `.skillcatalog.json` file that a row of the Catalogs table names: a list of
  Skills a Source offers, each with a category and, once the AI tool has fetched it, a **Skill
  pointer**. A catalog Skill with no pointer on this computer is **Not downloaded**. Not the
  `PromptCatalog`, `ThemeCatalog` or `TeamTypeCatalog`, which are code.
: *Avoid*: index, marketplace, store.

**Skill pointer**
: The `path` a catalog or Pack entry gives for one Skill folder on this computer — usually inside
  a clone the AI tool made, anywhere on disk. Huddle never runs or edits what a pointer names; it
  checks the folder and **copies** it into `{DataDir}/Skills/<name>/` with a `.source.json` when an
  Agent needs it. A pointer folder whose hash no longer matches the copy is one reason for **Update available**; a
  Pack whose list of Skills changed is the other.
: *Avoid*: link, reference (a reference is the chat feature), install path.

**Process**
: How a Team's work is done: its `Process.md`, copied from a Team type's process
  template into the Team folder and never overwritten. It holds the **Gates**, the
  cadence of **Retros** and digests, and the check command.
: *Avoid*: workflow, pipeline, methodology.

**Gate**
: A rule in a Team's **Process** that refuses one change until a condition is met,
  such as a Task moving to Done. Only the Human can override one, and the override is
  recorded on the Task (ADR-0039), except on a **Human-gated** Task, where nothing
  overrides (ADR-0061).
: *Avoid*: check, approval, guard.

**Evidence**
: A comment tagged `evidence` on a Task's **Timeline** that records what was checked, and by whom (ADR-0086). Gates read
  Evidence: a Task's move to Done needs it, and a merge needs one whose check command
  passed. One line of Evidence can meet one item of **Proof**.
: *Avoid*: proof (that is the checklist), log, result.

**Definition of Done**
: The list in a Team's **Process** of what must be shown before a **Human-gated** Task
  reaches the Human: one item per bullet under `## Definition of Done`, each met by
  Evidence, a passing check, a finished sub-Task or an attached document reviewed after
  its last edit (ADR-0062).
: *Avoid*: acceptance criteria, checklist (the checklist on a Task is its **Proof**).

**Proof**
: A Task's **Definition of Done**, evaluated: each item met or open, counted as
  "Proof 3/4". Locks the move to **Needs You** and the Human's Accept until it is
  complete. Shown on every Task, enforced only on a Human-gated one.
: *Avoid*: Evidence (one line), sign-off, verification.

**Human gate**
: The rule that a Task reaches Done only when the Human accepts it. A Task is
  **Human-gated** when it names an **Action** or the Human turned on *Requires your
  acceptance*. The Human decides with **Accept**, **Request changes** or **Decline**;
  only an acceptance opens Done, and it is void if the text changes (ADR-0061).
: *Avoid*: approval, sign-off, override, review (Review is a status).

**Action**
: What a Human-gated Task will do outside the Team once accepted, in one line: "Send
  reply to Acme", "Merge PR #418". Setting one locks the **Human gate** on. The
  assignee carries it out after the Human accepts, then moves the Task to Done.
: *Avoid*: side effect, outcome, operation.

**Request**
: A Task in a receiving Team's backlog, asking for a bug, feature, feedback or
  question from another Team. It is an ordinary Task file with extra keys (ADR-0040),
  and it moves through **Triage** and then the receiving Team's work.
: *Avoid*: ticket, issue, ask.

**Retro**
: A retrospective: the Team's retro role reads its records and proposes up to three
  exact-text changes, each to `Process.md`, the Team card or one member's file. The
  Human accepts or rejects each.
  Results go in `Retro register.md` and `_retros/`.
: *Avoid*: postmortem, review, lessons.

**Team card**
: The `_team.md` summary of one Team: its name, type, manager and what it owns. Other
  managers read it, and `list_teams` returns it.
: *Avoid*: profile, page, tile.

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
  **App Tool**. The Human runs one on an Agent by Mentioning it, `@Nova /compact`, and only a command
  that the Agent's Adapter Profile names in `Commands` is offered: the list an Adapter advertises is
  much longer, and without isolation includes the Human's own Claude Code skills. Built 2026-09-30;
  see [the Adapter commands spec](../Huddle.Commands-Specifications.md).
: *Avoid*: slash command on its own (`/invite` is Huddle's), skill, tool.

**Prompt block**
: A part of one Turn's prompt that is not its text: an image the Human's own Message names, or, for an
  Adapter with no file tools, a document's text as an embedded resource. Sent beside the text, never
  instead of it, and only when the Adapter said at `initialize` that it takes one and its Adapter
  Profile's `PromptBlocks` allows it. Only the Message that started the Turn can contribute one, never
  Catch-up. A document that is not sent as a block keeps its path line, which says whether the file is
  included with the message. Built 2026-10-01; see [the Prompt blocks design](../Huddle.PromptBlocks-Specifications.md)
  and [ADR-0036](../adr/0036-a-prompt-block-is-sent-only-when-the-adapter-advertised-it.md).
: *Avoid*: attachment (that is the Human attaching a file in the composer, roadmap item 23 B, not
  built), upload, content block outside `Huddle.Acp`.

## Conversations

**Room**
: A named conversation with a fixed set of Members whose history is one
  Transcript. There is exactly one kind of Room. A Room in a Team may also
  belong to one Project of that Team, set at creation and never changed
  ([ADR-0082](../adr/0082-a-room-may-belong-to-one-project-of-its-team.md)).
: *Avoid*: channel, chat, conversation, thread. Also avoid *Direct Room* and
  *Group Room* — a Room is a Room, and behaviour follows from member count.
: *chat* survives only as a verb or an activity: you chat **in** a Room ("Chat
  with Nova", "don't ask for approval in chat"), and Huddle is a chat
  application. Every label that names the thing says Room: the **Rooms** view,
  **New room**, **Open room**, **Tasks in this Room**, the Archived page's
  **Rooms** tab.

**Scope**
: Where a Room belongs: the root, or exactly one Team. Set when the Room is
  created and never changed. It decides where the Room is listed and where its
  uploaded files go, never who may join or who replies (ADR-0052).
: *Avoid*: team chat as a kind of Room, channel.

**Team Room**
: The Room scoped to a Team and named after it, holding the Human and every
  member. Created when the Team first has a member; an Agent who joins the
  Team later is added to it.
: *Avoid*: team channel, general.

**Upload**
: A file the Human attaches to a Message in the composer. It is saved at once into
  `raw/` in the Room's scope in the Library: `Raw/<roomId>/` for a root Room, and
  `Teams/<Team>/raw/<roomId>/` for a Team Room. Its path is added to the Message when
  it is sent (ADR-0053). Until then it is a pending upload, shown as a chip.
: *Avoid*: attachment (there is no attachment field on a Message), file share.

**Archived**
: Hidden from the sidebar and listed on the **Archived** page instead, where
  **Restore** brings it back. For a Room it is the page's **Rooms** tab: reversible, and a display filter only — an Archived Room is
  still live, and Agents still post into it. For a Team or a Project it means the
  folder has been moved to `{DataDir}/Archive`, where it is read-only.
: *Avoid*: hidden, closed, muted, inactive, soft-deleted. A Room that was
  *deleted* is gone, not Archived — the two are not degrees of the same thing.

**Member**
: An Agent that belongs to a Room and receives its Messages.
: *Avoid*: participant, subscriber.

**Invitation**
: Adding an Agent to a Room that already exists. Three doors, one behaviour:
  the Human types `/invite @name` in the composer or uses **Add agent** on the
  Room header, and an Agent calls `mcp__team__invite_agent`. All three end in
  `ChatService.InviteAsync`, so the Room renames itself after its Agents however
  the Invitation was issued.
: *Avoid*: join, add user.

## Agents and Personas

**Persona**
: The Markdown instructions defining one Agent's character, stored as one file
  in its own folder, `{DataDir}/{Acp:AgentsDir}/<Name>/<Name>.md`, found by a
  one-level scan (ADR-0031) — together with the **Model**,
  the **Effort** and the **Work Mode** it runs on, stored in the `persona_models`,
  `persona_efforts` and `persona_work_modes` tables. The file body *becomes* part of a system prompt; it
  is not one.
: Its leading YAML frontmatter block is **partly schema**. `name`, `title` and
  `alias` are required and `teams` is optional; a file missing any required one
  is not a Persona at all — it is a rejected file, listed with its reason on the
  Agents page rather than silently ignored. Every *other* top-level field
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
  arbitrary winner. Rejected files are surfaced on the Agents page with their
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

**Work Mode**
: The mode, from the list its Adapter advertises (the ACP `mode` config option), that one
  Persona's sessions start in: how much the Agent may do before it must ask. `claude-agent-acp`
  advertises `default` (shown as Manual), `acceptEdits`, `plan` and `auto`, and
  `bypassPermissions` where the process is not root. Unset means the Adapter's own mode and
  nothing is sent, which is the normal case. Fixed for the life of a session, exactly like a
  Model, and chosen only by the Human. See [ADR-0033](../adr/0033-a-persona-has-a-work-mode.md).
: *Avoid*: "mode" alone — it already means light/dark, an Agent card's Create/Edit/View state
  and the system-prompt mode; "permission mode", "ACP mode". Not a **Plan**: the Adapter's `plan`
  mode is one Work Mode, and a Plan is an Agent's own checklist.

**Turn Hook**
: A compiled class that adds to or reads from a Turn at one of three points: when a
  session opens, before a Turn is sent, and after it ends. Huddle runs a fixed, ordered
  list of them around every Turn. A prompt addition is one class and one list entry.
  File Changes and the `AGENTS.md` notice are Turn Hooks. A hook that throws is logged
  by name and skipped, and the Turn goes on. See
  [ADR-0076](../adr/0076-a-running-agent-hears-of-an-agents-md-change-through-a-turn-hook.md).
: Not a Claude Code hook, and not a plugin: Huddle's developers add them in code, and
  nothing loads at runtime.

**Turn Enricher**
: A compiled class that adds to a Message Turn's work item after the Transcript catch-up,
  which a Turn Hook runs too early to read. Library documents and Referenced items are Turn
  Enrichers. One that throws is logged by name and skipped, and the Turn goes on.
: Not a Turn Hook: it keeps no state and has no point after the Turn.
: *Avoid*: plugin, middleware, callback.

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

**Turn Queue**
: The Turns an Agent owes, kept in `team.db` (`turn_queue`): one row per Agent and Message, written by the
  server when the Message is posted, from the Reply Gate's answer at that moment. A row is done when a reply
  that names it reaches the Transcript, or the Agent's Turn ends with nothing to post. It is what lets a Message
  waiting at a restart, a crash or an Agent still connecting be answered, once
  ([ADR-0091](../adr/0091-a-rooms-waiting-turns-are-kept-in-team-db.md)).
: Not the Room Session's in-memory work queue, which drives Turns and is fed from it at a Welcome.
: *Avoid*: inbox, backlog, mailbox.

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
: What the Adapter has reported spending for one Agent since the app started, shown in money on
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
  across a restart. Each is named by an Agent's Name, meaning their Work Dir, or
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

**Instructions** (retired 2026-11-03)
: Replaced by **`AGENTS.md`**. Nothing reads an `Instructions.md` any more: the
  start-up retired-file scan warns about each one under `Teams/` or an Agent's Work
  Dir, and its rules are not moved for you. The file was built 2026-10-30 — see
  [ADR-0070](../adr/0070-an-agent-reads-the-instructions-the-human-writes.md).
: *Avoid*: house rules, system prompt, rules file.

**`AGENTS.md`**
: The Human's prose rules for one level of the Second Brain, in a file named
  `AGENTS.md`. Every Team and every Project holds one, and the root of the Teams tree
  holds one for everyone. Huddle splices them into the system prompt right after the
  Persona's text, from general to specific: the Teams root, then each Team and each of
  its Projects, so the more specific level wins. The Human edits them in Obsidian or the
  Library. A running session hears of an edit at its next Turn in each Room, with what
  changed and a note that it applies from now on; the file's text wins. See
  [ADR-0076](../adr/0076-a-running-agent-hears-of-an-agents-md-change-through-a-turn-hook.md).
: Not an Agent's own rules, which live in its Persona, and not the Persona's text.
: *Avoid*: Instructions, rules file, house rules.

**Memory**
: What an Agent deliberately keeps for itself going forward: Markdown topic files in
  its Work Dir's `memory/` folder, one topic per file, each with a one-line
  `description` in its frontmatter that says when to read it. Written through the
  `memory` App Tool, which refuses a write over a file the Agent has not read. It
  belongs to the Agent, not to a Room. Every new session, after a restart or in
  another Room, starts with an index of it (`file - description`) in the system prompt,
  and File Changes carries later edits to its other Rooms, marked *by you, in Room 'X'*
  when another copy of it wrote them. **Delivered 2026-09-23; topic files and the
  `memory` tool built 2026-10-30** — see
  [ADR-0023](../adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md) and
  [ADR-0071](../adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md).
  Depends on isolating a Persona's session from the Human's own Claude Code settings,
  which is unverified live — see [Known limits](known-limits.md).
: Capitalised, it means only this. Claude Code's own auto-memory is a different
  thing, and Personas should not use it.
: *Avoid*: notes, knowledge base, scratchpad, context.

**Team Memory**
: What a Team keeps going forward, shared by every member: topic files in
  `Teams/<Team>/memory/` for the whole Team or `Teams/<Team>/<Project>/memory/` for one
  Project, in the same format as an Agent's Memory. Members propose facts with
  `propose_team_memory` and the **Team Memory Helper** files them; the Team's manager and
  the Chief of Staff may also write it directly with `memory`. Every member's new session
  starts with an index of it after its own Memory; later edits arrive as File Changes.
  **Delivered 2026-09-29; manager-only writes built 2026-10-30; proposals built
  2026-10-30** — see
  [ADR-0032](../adr/0032-a-team-and-each-project-share-a-memory-folder.md),
  [ADR-0071](../adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md) and
  [ADR-0073](../adr/0073-members-propose-team-memory-and-a-helper-files-it.md).
: Never just *Memory*, which stays the Agent's own.
: *Avoid*: shared memory, project memory, team notes.

**Huddle-wide Memory**
: What the Human and the Chief of Staff keep going forward for every Agent: topic files
  in `Teams/memory/`, in the same format as an Agent's Memory. Every Agent's new session
  starts with an index of it, within its cap (`Team:Teams:MaxHuddleMemoryEntries`, 30),
  ahead of its Team Memory.
: Never just *Memory*, which stays the Agent's own, nor *Team Memory*, which is one
  Team's or Project's.
: *Avoid*: global memory, company memory, shared memory.

**Context only**
: What a delivery amounted to when it named no Agent the Reply Gate would wake:
  the Message was read as context and no Turn began. A property of the delivery, the
  same way **Mentioned** is — never of the Message, which may be context only for one
  Agent and a Mention for another. It is what the Room view says, and it instructs
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
: A roster of new Agents an Agent asks the Human to create, made with
  `propose_agents` in one Room and shown there with Approve and Decline. An
  Agent never creates an Agent; the Human's Approve does, and the outcome is
  posted as a Message from the Human, which wakes the proposer. At most one per
  Room, held in memory, so a restart loses it.
: Each proposed Agent in it is a **Candidate** — never a *draft*, which is
  already the Turn text shown before it becomes a Message.
: *Avoid*: request, application, pending Agent, draft.

**Question**
: One multiple-choice question an Agent puts to the Human with `ask_human`: a line
  of text, two to four short options, and whether the Human picks one, picks any,
  or ranks them. An Agent may ask up to three at once; they wait together on one
  card in the Room and are answered together, as a Message from the Human that
  quotes each Question and Mentions the asker. Only the Human is ever asked. At
  most one card per Room, held in memory, and dropped by any typed Human Message
  there. See
  [ADR-0022](../adr/0022-an-agent-asks-the-human-with-a-question.md).
: Capitalised, it means only this. An ordinary question in a Message stays lower
  case.
: *Avoid*: poll (one person answers), prompt (a defined term for model-facing
  text), form (a **Form** is the other thing, defined next), survey, quick reply.

**Form**
: A set of typed fields an Agent's tool asks the Human to fill in, over ACP
  elicitation: Claude's built-in `AskUserQuestion`, its retry-after-refusal
  dialog, or a form an MCP server sends. It waits on an `ElicitationCard` in the
  Room, any number at once, and the Agent's Turn stays open until it ends. The
  answer is a Message from the Human that mentions nobody and is withheld from the
  asker, which receives it as the tool's own result; Skip tells the Agent the
  Human skipped it. See
  [the Questions spec](../Huddle.Questions-Specifications.md#68a-the-elicitation-bridge).
: Capitalised, it means only this. It is not a **Question**: a Question is
  `ask_human`'s tappable options, and its asker has already ended its Turn.
: *Avoid*: Question (the other thing), survey, dialog, popup.

**Needs You**
: The sidebar card listing every Room that waits on the Human, and the in-memory
  tracker behind it. A Room is listed when an Agent Mentions the Human, a
  **Question** or **Form** waits there, or its **Budget** is spent; it stays
  listed until the Human posts in it or dismisses it. Opening the Room does not
  clear it. In interface copy the card reads "Needs you". See
  [ADR-0050](../adr/0050-needs-you-is-derived-from-room-events-and-held-in-memory.md).
: Also the Task status between Review and Done that only a **Human-gated** Task can
  enter, once its **Proof** is complete. A Task in it, not yet accepted, is listed on
  the card and the `/needs-you` page; it leaves only by a decision or a status change
  (ADR-0061). The status and the card share the name on purpose.
: *Avoid*: inbox, notifications (a **Notification** is a stored **Feed item**,
  ADR-0063), unread, alerts, pending. **Unread** in particular is a different
  idea: Needs You is about who is blocked on the Human, not what the Human has read.

**Feed**
: The stored history of what Agents and Huddle did: one row per event, posted by
  the component that knows it, never worked out from other events. Shown as the
  Notifications panel and the Feed Tab (`/feed`). Beside **Needs You**, not instead
  of it. See
  [ADR-0063](../adr/0063-the-feed-is-a-stored-history-that-each-domain-component-posts-to.md).
: *Avoid*: timeline, activity log, inbox, stream.

**Feed item**
: One row of the Feed: who, when, what, and the stable key of its target (a Task id,
  a Room id, a Library path). Never holds Message text.
: *Avoid*: post (a Message's word), event, entry (a History or Change log line).

**Notification**
: A Feed item its sender marked for the Human (`notify`). It is shown in the
  Notifications panel, is counted until seen, and can be dismissed. It is resolved,
  dimmed and uncounted when its sender says the condition cleared.
: *Avoid*: alert, unread (the count is **unseen**), Needs You (the live list).

**Activity**
: A Feed item that is not a Notification: shown only in the Feed Tab, and deleted
  after `Team:Feed:ActivityRetentionDays`.
: *Avoid*: Change log (a Task's own history), History (a document's).

**Feed publisher**
: `IFeedPublisher`, the one interface producers call: `Post` and `Resolve`, both
  enqueue-only and never throwing.
: *Avoid*: notifier (`IRequestNotifier` wakes Personas), event bus.

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
  under `{DataDir}/{Acp:AgentsDir}/<Name>/work/`.
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

**Helper**
: A background agent that makes one stateless judgement about an event and answers
  in JSON, which Huddle parses into a **Verdict** and applies through the ordinary
  domain service. It has no tools, no Room, no Persona and no Room Budget, and posts
  no Messages. In code, `HelperAgent` (`IHelperAgent<TInput, TVerdict>`). If it
  talks, keeps memory or works in a Room, it is an Agent instead. Each Helper has
  a daily call cap; reaching it pauses that Helper until the Human presses Continue.
  See [ADR-0064](../adr/0064-a-helper-judges-an-event-and-the-app-applies-its-verdict.md).
: Capitalised, it means only this. The lowercase "helper" (a code utility class,
  MudBlazor helper text) is unrelated.
: *Avoid*: special agent, bot, assistant, background Agent.

**Verdict**
: A Helper's parsed answer, a typed record such as `NeedsHumanVerdict(NeedsHuman,
  Reason)`. Huddle applies it; the Helper never acts.

**Needs Human Helper**
: The first Helper. It reads each Task an Agent creates or rewrites and turns its
  Human gate on when the work leaves the Team or can't be undone, writing as
  `Needs Human (Helper)` with its reason in the Change log. It never turns the gate
  off, and never judges a Task again once the Human has turned that Task's gate off.

**Team Memory Helper**
: The second Helper. It judges each fact a member proposes with `propose_team_memory`
  against the Team's Memory index: file nothing, create a topic file, or update one it
  was shown in full. Huddle writes the file through the `memory` tool's rules and records
  `Team Memory (Helper)` with the proposer's Name in the file's History. See
  [ADR-0073](../adr/0073-members-propose-team-memory-and-a-helper-files-it.md).

**Reported file**
: `reported.md` in an Agent's own Memory: one line, `- yyyy-MM-dd · <Room> · <what>`, for each
  thing the Agent told the Human. The `memory` tool writes the date, starts the file and drops
  lines older than `Team:Memory:Reported:KeepDays`; the Agent reads it before it reports, and the
  Chief of Staff reads a Team manager's through `agent:<Name>`. Special only in an Agent's own
  Memory: elsewhere `reported.md` is an ordinary topic file. See
  [ADR-0094](../adr/0094-an-agent-keeps-a-record-of-what-it-told-the-human.md).
: *Avoid*: report log, status file, sent file.

**Memory Tidy**
: The weekly pass over Memory. For each Agent's own Memory folder that changed and holds enough
  files, the **Memory Tidy Helper** may merge two files, shorten one, fix a missing description or
  drop a file that is out of date. It never creates a file. Huddle checks each step, copies the
  file to the **Memory backup** first, and writes a list of every change. It starts on a timetable
  or by **Tidy now** on the Helper's card. See
  [ADR-0095](../adr/0095-a-helper-tidies-memory-weekly-and-huddle-keeps-a-copy.md).
: *Avoid*: dreaming, consolidation, cleanup job.

**Memory Tidy Helper**
: The fourth **Helper**. It reads one Memory folder and answers with a **Verdict** of steps:
  merge, rewrite, describe or drop. Huddle refuses a step that adds a number, a date, a link or an
  address, that grows a file, or that loses one of those in a merge, and applies the rest. It
  defaults to `sonnet` at low Effort. In code, `MemoryTidyHelper`.

**Memory backup**
: The copy Huddle makes of a Memory file before Memory Tidy changes or removes it. Copies sit in the
  read-only **Memory backups** Library root, one dated folder per pass, beside that pass's
  `changes.md` list. The Human restores a file by copying its text back by hand. Pass folders older
  than `Team:Memory:Tidy:KeepBackupDays` (90) are deleted when a pass starts.
: *Avoid*: archive (that is `memory/archive/`, old topics kept on purpose), trash.

**`AGENTS.md` Changes Helper**
: The third Helper. It reads one edit to an `AGENTS.md` file, as the old text, the new
  text and a comparison, and lists the rules added, changed or removed, each with a
  quote. It runs once per edit, not once per Agent. Huddle checks every quote against
  the file, and one bad quote refuses the whole answer. A small edit it calls not
  meaningful is skipped and listed in the Feed. See
  [ADR-0076](../adr/0076-a-running-agent-hears-of-an-agents-md-change-through-a-turn-hook.md).
: Not the File Changes report, which names files and no rules.

**Huddle (as sender)**
: The sender of a Message Huddle itself writes, such as a Helper's wake-up to the
  assignee: `KnownIds.Huddle`, neither the Human nor an Agent, with no `User` row
  and no Membership. Its Messages answer no Mention, clear nothing from Needs You
  and reset no Budget. See
  [ADR-0065](../adr/0065-huddle-posts-as-itself.md).
: *Avoid*: system, bot, the app.

## Library

**Library**
: The explorer, viewer and editor as a whole.

**Library Root**
: One top-level folder the Library may reach, with an id: `teams`, `agents`,
  or a pinned root's slug.

**Library Pane**
: The docked panel: the tree above, the open document below or beside it.

**Team folder**
: `{DataDir}/Teams/<Team>/`, a Team's folder of notes, Projects and Tasks.

**Project**
: A direct sub-folder of a Team folder; the same Project Tasks files into. A folder
  whose name starts with `_` or `.`, or is `memory` (Team Memory,
  ADR-0032), is never a Project.

**Agent folder**
: `{DataDir}/Agents/<Name>/`: the Agent's definition and its `work/` Work Dir.

**Pinned root**
: A Library Root the Human added in configuration or Settings.

**Raw**
: The folder for what came in: the Raw root (`{DataDir}/Raw`, set by `Team:Library:Raw:Dir`)
  and every `raw/` folder under a Team, a Project or the Archive. Chat uploads land here.
  An Agent may not delete, move or change an existing file under it, though it may create
  a new file beside one. The Human's files, not Memory: what an Agent keeps for itself goes
  in `memory/`.
: *Avoid*: inbox, input, Uploads (the retired folder).

**Outputs**
: The folder for what was made: `outputs/` in a Team or a Project
  (`Teams/<Team>/[<Project>/]outputs/`). Its Markdown files are indexed for the Team's
  members at each session start.
: *Avoid*: deliverables, artifacts.

**Workspace**
: A folder outside the Second Brain that a Team works in, named by the `workspace` key on
  its Team card (an absolute path, often a repository). Its path reaches an Agent's prompt
  as the Team's workspace part.
: Not the Team's folder under `Teams/`, and not a Project. A workspace may be a
  repository, but the word is the folder's.
: *Avoid*: repo, repository (for the folder itself), project folder.

**Library tag**
: A label on a Library note, written `#tag` in its body or in its frontmatter `tags`
  list, matched without case and nested on `/` (`work/active` is under `work`), as in
  Obsidian. Huddle reads it and never writes it. Not a Task's `tags` field, and never a
  Mention or a reference. *Avoid*: hashtag.

## Tasks

See
[Huddle.Tasks-Specifications.md](../Huddle.Tasks-Specifications.md),
[ADR-0025](../adr/0025-in-tasks-a-team-is-a-folder-by-convention.md) and
[ADR-0026](../adr/0026-a-change-to-a-task-wakes-its-assignee.md); for Sprints,
[Huddle.Sprints-Specifications.md](../Huddle.Sprints-Specifications.md) and
[ADR-0037](../adr/0037-a-sprint-is-a-named-group-of-tasks-in-one-markdown-file.md).

**Task**
: A unit of work that the Human and the Agents share. It is one Markdown file at
  `{DataDir}/{Team:Teams:Dir}/<Team>/[<Project>/]_tasks/[_closed/]<ID>.md`: a title, a status,
  a priority, at most one assignee, at most one Sprint, a description, and a **Timeline**. Its identity is the `id`
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
  folder. A Room of that Team may also belong to it (ADR-0082). Its name is unique only within its Team, so write it as
  *Team / Project* wherever the Team isn't clear.
: *Avoid*: epic, milestone, board.

**Sprint**
: A named group of Tasks in time. It is an id and a name, listed one per
  line in `{DataDir}/sprints.md`, such as `- S2 | Auth hardening`. A Task is in at most one
  Sprint, by writing the id in its `sprint:` key, and a Sprint spans Teams and Projects. It
  has no dates, goal or state. Its order is its id's number, and the **current** Sprint is the
  one with the highest number. The Human creates one by typing a new name in place in the
  Sprint field on the Task panel; an Agent can only use one that exists. A Sprint lasts while a
  Task, Active or Closed, holds it: when a change moves the last Task out, the app removes its
  line.
: Not a **Project**: a Project is a folder, a Task has exactly one, and it says what the Task
  is part of. A Sprint is a field, a Task has at most one, and it says when.
: *Avoid*: iteration, cycle, release, milestone (a milestone is a date, and a Sprint has
  none), and sprint board (a Board filtered to one Sprint is still a Board).

**Closed**
: A Task that has been put away. Its file sits in a `_closed/` folder, and it is
  hidden until the Human looks at Closed Tasks. Closing is separate from status.
  It doesn't require a terminal status, and a Closed Task can be **Reopened**. In
  the interface the verbs are **Close** and **Reopen**.
: *Avoid*: archived, and archive (**Archived** belongs to Rooms, Teams and Projects, where
  it means hidden, and for a Team or a Project read-only), done (that is a status),
  resolved, deleted.

**Won't do**
: The three terminal statuses other than Done: **Cancelled**, **Duplicate** and
  **Rejected**. On a Board they share one column. Dropping a card there shows one
  drop target per status, called a **ghost bucket**. A Duplicate names the Task
  it duplicates.
: *Avoid*: invalid, won't fix, discarded, abandoned.

**Change log**
: The events on a Task's **Timeline**: when, who, and what changed. Only `TaskService` writes them, and only by
  appending. The Created and Closed dates are derived from them. A Task file written before the Timeline keeps them
  under a `## Change log` heading until its first write.
: *Avoid*: activity, history, audit log.

**Timeline**
: A Task's comments, **Evidence** and **Change log** events in one list, oldest first, under the `## Timeline`
  heading at the end of its file (ADR-0086). Nobody edits or deletes an entry; the description is the only text that
  is edited. In the interface it is the Task view's Timeline section, with the comment box under it.
: *Avoid*: activity, history, thread.

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
: *Avoid*: notify (except in button text such as *Save & notify Nova*), ping,
  trigger, alert, nudge.

**Awake / Asleep / Offline**
: An AI Agent's presence as the Tasks interface shows it. **Awake** means a
  Turn is running. **Asleep** means the Persona is running with no Turn in
  progress, and can be woken. **Offline** means the Persona isn't running; a
  wake-up Message is still posted, but the Agent doesn't receive it.
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
  board, sprint board (filter a Board to a **Sprint** instead).

## Jobs

See [Huddle.Jobs-Specifications.md](../Huddle.Jobs-Specifications.md) and
[ADR-0074](../adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md).

**Job**
: An instruction that runs on a timetable for the Human or one Agent, its
  owner. It is a `## ` section of the owner's `schedule.md`. Any Agent may
  schedule its own; only the Chief of Staff schedules for someone else.
: *Avoid*: schedule (that is the owner's whole file), task (a **Task** is
  shared work with a status), reminder, cron job, routine.

**Job Room**
: The Room a Job runs in, named `⏰` and the Job's title, made on its first
  run. Huddle posts the instruction there; an Agent's holds the Human and that
  Agent, the Human's holds the Human alone. Never an Agent's one-to-one.
: *Avoid*: schedule room, reminder room.

**Scheduled**
: The Sidebar view and page that list every Job and add one.
: *Avoid*: schedules, cron, timers.

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

## Remote access

The words for using Huddle from another device ([Huddle.RemoteAccess-Specifications.md](../Huddle.RemoteAccess-Specifications.md)).
OpenClaw's own word is kept wherever Huddle has no clash. In interface copy, a person is "you", and a device is a
"device", never a "client".

**Gateway**
: Huddle's HTTP surface as a whole: every page, asset, endpoint and the `/_blazor` connection. Named as OpenClaw names
  its own.
: *Avoid*: server, API.

**Gateway auth**
: The shared secret every connection presents: the gateway token (48 hex characters, the default) or the gateway
  password (`Auth:Mode`), or Tailscale identity, or trusted-proxy identity.
: *Avoid*: login.

**Device identity**
: An Ed25519 key pair the browser generates and keeps in its local storage. The **device id** is the SHA-256 hex of
  the public key.
: *Avoid*: fingerprint, client id.

**Connect handshake**
: What `/auth` does: the browser asks for a challenge, signs it with its device key together with its credential, and
  receives a **Session**.
: *Avoid*: login, sign-in.

**Session**
: The proof of one successful handshake: an HttpOnly cookie that Huddle's pages and `/_blazor` carry. It lasts 12
  hours. Huddle's equivalent of an authenticated OpenClaw WebSocket.
: *Avoid*: login session.

**Pairing**
: A device becoming known. A pairing request waits up to 5 minutes for an admin's **Approve** or **Reject**, unless
  it is auto-approved.
: *Avoid*: registration.

**Device token**
: The per-device credential issued on approval, scoped to a role and scopes, kept by the browser in local storage and
  presented on later handshakes instead of the gateway token.
: *Avoid*: API key.

**Scope**
: What a session may do: `operator.read`, `operator.write`, `operator.pairing`, `operator.admin`.
: *Avoid*: permission, role.

**Limited access**
: A device without `operator.admin`. It may **Request admin**, which needs approval. Limited is not read-only: there is
  no read-only device ([known-limits.md](known-limits.md)).
: *Avoid*: read-only.

**Bootstrap token**
: A single-use credential, valid 10 minutes and bound to the first device that redeems it, that opens one browser
  already authenticated. Minted for the host's own browser by the desktop launcher or the `dashboard` verb, or for
  another device as a **setup link**. Carried in the URL fragment.
: *Avoid*: magic link.

**Ingress**
: How a request arrives: loopback, managed (through Huddle's own Tailscale Serve or Funnel), direct (a tailnet or LAN
  bind), or trusted proxy. Decided only by the ingress classifier.
: *Avoid*: transport.

**Managed listener**
: The extra loopback listener on a random port that only Huddle's own `tailscale serve` points at.
: *Avoid*: proxy port.

**Downgraded**
: OpenClaw's word for plain HTTP off loopback: it works, but nothing is encrypted and the page can be altered in
  transit.
: *Avoid*: insecure mode.

**Instance id**
: A random id stored once per `{DataDir}`, part of every cookie name, because cookies ignore ports and copies run side
  by side.
: *Avoid*: port.

## Model-facing text

**Prompt**
: One named piece of text this application sends to a model — a system-prompt
  block, a Turn's framing, a `get_help` section, a tool's own description.
  They are listed, with their defaults, on the Prompts tab of Settings (`/settings`).
  `PromptCatalog` holds every default in code; `prompts.json`
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
  for Agents started afterwards (`NextSession`). Not a preference — a system
  prompt is fixed at `session/new` and there is no later event that re-reads it.
  Editing a Prompt never restarts a session; see [Rules](rules.md).
: *Avoid*: scope, refresh, reload.

## Search

**Global Search**
: The search box in the Top Bar and its dropdown, which finds Teams, Agents, Library
  entries, Tasks and Rooms by exact phrase, ignoring case. It keeps no index of its own:
  every search reads again.
: *Avoid*: quick open, find, lookup, omnibox.

**Top Bar**
: The fixed bar across the top of the window, above the left menu and every pane. It holds
  the breadcrumb trail of the focused pane's active Tab and the Global Search box. The window
  owns it, never a Tab or a page.
: *Avoid*: header, app bar, title bar, toolbar.

**Filter**
: In Global Search, one of All, Teams, Agents, Library, Tasks and Rooms, drawn as a chip.
  Typing its prefix (`team:`, `agent:`, `lib:`, `task:`, `room:`) and picking its chip
  are the same act.
: *Avoid*: scope (that is the Active or Archived pass), category, tab.

**Card** (search)
: One result row in the Global Search dropdown, drawn by the component for its kind of hit.
  *Card* already names two other things: a Board's drawing of a Task, and the Agent card
  that opens from the Agents page. Say *search card* where the three could be confused.
: *Avoid*: result item, tile, row.

## References

**Reference**
: Text in a Message that names one Task, Room, Team or Library file by its stable key: a Task id,
  a `/rooms/<id>` or `/teams/<Team>` link, or a Library path. Written by the Reference picker or
  typed; either way it is ordinary text. An Agent is never referenced: it is **Mentioned**. When
  the Message is posted, what each reference named is kept beside the text as its snapshot.
: *Avoid*: tag, hashtag, link card, pill.

**Reference picker**
: The list the Composer opens on `@`, and on `#` for Tasks, over the Global Search providers. Picking
  a row writes a Mention or a reference at the caret.
: *Avoid*: autocomplete, mention picker, tag picker.

**Referenced items**
: The short header a Turn's prompt gains for the references in the Message that started it, looked
  up when the Turn starts: a few facts per item, fenced as data, ending in how to learn more.
: *Avoid*: context, attachments, expansion (in interface copy).

**Snapshot**
: What a reference named when its Message was posted, kept beside the Message's text. It is what
  the Transcript shows when the named item later changes or goes away.
: *Avoid*: cache, copy of the item.

## Appearance

**Theme**
: One skin file per Theme and mode, loaded into a `MudTheme` in `ThemeCatalog`, carrying **one** palette and the
  `ThemeDescriptor.Mode` naming which — so a Theme *is* a light one or a dark one rather
  than spanning both, and choosing it chooses the mode
  ([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). It fills only that slot;
  the other keeps MudBlazor's defaults and is never rendered. Eight ship built in, the
  Lumen (the default), Fluent, Nordic and Cobalt Soft pairs (lumen-light, lumen-dark and so on,
  mapped from the approved mockup directions). The Huddle pair, the Custom Accent Themes and
  seventeen Visual Studio Code ports shipped until 2026-10-17 — see
  [ADR-0058](../adr/0058-a-theme-is-a-palette-a-skin-and-traits.md) and
  [ADR-0016](../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md)
  (superseded). A Theme is a **skin**: a palette, `--hu-*` tokens, ten traits, faces, radius,
  typography and shadows. A Human's own accent is a per-Theme **customization**, not a Theme.
  A Theme also carries a **`ThemeGroup`** — Light or Dark — which is the picker list it
  appears in. Today it always equals its Mode, but it is a separate field.
  A Theme is identified by its **id**, which is what
  `appearance.json` stores; never by its display label, for the reason
  [Rules](rules.md) gives for a Model. Roadmap item 7 is what remains: importing an
  *arbitrary* Theme a Human supplies, mapped from JSON into a `MudTheme` at run time
  rather than generated as CSS. The bundled Themes needed no importer — they are
  embedded JSON skins.
: *Avoid*: stylesheet (a Theme stopped being one on 2026-09-14 — see
  [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md)), palette (that is one
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
  selected Theme ids (`theme`, `lightTheme`, `darkTheme`), and `customizations` per Theme id (a palette and token override validated like a skin; the Appearance tab writes only `primary`), and nothing else (a retired `libraryPaneSide` key is ignored). Per installation, hand-editable and watched,
  exactly like `hooks.json`. The old CSS-Token override layer was removed
  with the Tokens, and since 2026-09-21 there is no light/dark preference either: a
  Theme carries its own palette, so selecting one selects the mode
  ([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). A file written before then
  still holds a `dark` key; it is an unknown key now, ignored and kept. Appearance is the
  choice; a **Theme** is what it selects.
: **The tab is not the file.** Since 2026-09-22 the tab has a second section — the
  Human's own **Avatar** — because the Human has no Persona file and so no Agent
  card to edit one on. That Avatar is stored in `avatars.json`, a different file with a
  different owner; `appearance.json` still holds the selected Theme ids and the Theme customizations, and nothing else.
: *Avoid*: dark mode and light/dark preference (there is no such setting any more — say
  *a dark Theme*), display settings.
