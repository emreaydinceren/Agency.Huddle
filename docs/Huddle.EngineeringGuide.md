# Agency.Huddle

Agency.Huddle is a Slack-shaped chat app where the other participants in the conversation
are real Claude agents running as child processes. This page is the hub of its
documentation: read it whole if you are picking the codebase up cold, then follow
only the links your task needs.

Applies to the repo as of 2026-10-01, after Work Modes: one solution, `Huddle.slnx`, holding all six projects, builds
with zero warnings and 5265 of its 5284 tests pass and the other 19 are skipped unless
`Team:Acp:Enabled` is on — see [Build, test, run](#build-test-run).

The product is Agency.Huddle, and since 2026-09-12 so is every namespace.
Projects, assemblies, folders and the solution followed and are now `Huddle.*`
too — see the last rule in [Repository and toolchain
rules](#repository-and-toolchain-rules).

It is a proof of concept. Simple over complete, deliberately — do not add
abstraction layers, retry policies, or configuration surfaces that no current
feature asks for.

> [!IMPORTANT]
> Every other Markdown file in this repository predates the 2026-09-11
> vocabulary change and uses the old words — Bot, Direct Room, Group Room, Agent
> Session, sandbox. They are kept as dated records, not corrected. Where they
> disagree with this page, this page is right. See [Decision
> record](engineering/decisions.md) for the mapping.

## The map

The map of every page, by task and by feature, with its cost and owner, is
[Index.md](Index.md). Start there when you need to know which file to open; this page
holds only what almost every task needs: the idea, the configuration and the build.

Two pages are **binding, not informative**: [Rules](engineering/rules.md) and [Traps](engineering/traps.md). They are the only two pages
whose contents can cost you a day, and neither is summarised here: a rule copied
into two files is a rule that goes stale in one of them. Follow the link.

### Vocabulary at a glance

The defined terms, so you can tell whether a word you are about to use is one of
them without opening [Language](engineering/language.md):

> Agent · Human · Agent · Name · Alias · Title · Avatar · Team · Adapter · Room ·
> Member · Invitation · Archived · Persona · Rejected file · Model · Effort · Work Mode · Adapter Profile · Adapter command · Turn · Turn detail · Edit preview · Spend · Room Session ·
> App Tool · Reply Gate · Budget · Catch-up · Watched Folder · File Changes · Memory · Team Memory · Progressive discovery · Skill · Proposal ·
> Candidate · Question · Greeting · Work Dir ·
> Library · Library Root · Library Pane · Pinned root · Team folder · Agent folder · Project ·
> Task · Closed · Won't do · Change log · Origin · Wake · View · Board ·
> Message · Draft · Mention · Envelope · Transcript · Stop · Team Directory ·
> Prompt · Placeholder · Default · Timing · Theme · Token · Appearance

Words that are *wrong here* and have a right replacement: bot, user, channel,
direct room, group room, agent session, sandbox, profile, prompt, database.
[Language](engineering/language.md) says what each becomes and why.

## The one big idea

An **Agent** exists in this application for exactly one reason: some external
process connected to a named pipe and said `hello`. That is the entire contract.
The chat surface has no idea what is behind an Agent.

So there are three kinds of thing behind Agents today, and they are
interchangeable:

| Behind the Agent | Where | What it does |
| --- | --- | --- |
| `tools/echo-bot.ps1` | a PowerShell script | echoes text back |
| `DemoAgentHost` | in-process | echoes text back |
| `PersonaRunner` | in-process | drives a **real Claude process** over ACP |

That is why joining the two halves of this repo required almost no change to the
chat surface. `PersonaRunner` is an ordinary pipe client with no privileged
access to the database — *everything it knows arrives in an envelope*. Preserve
that property. It is what keeps the design honest.

The second idea: a Room's behaviour is defined by **how many Members it has**,
not by a type column. Two Members is a private conversation (answer everything);
three or more is a group (answer only when `@`-mentioned). One thing overrides
both — a Room that has spent its **Budget** answers nothing until the Human speaks
or grants more. `ReplyGate` is still the whole of that rule and still a pure
function: the Budget reaches it as two numbers on the Envelope, so the server is
labelling, not deciding.

## What a team is for

The product claim is that a small team of Agents does things one agent cannot.
The reasoning, the five patterns and the catalogue of teams live in the shipped
`team-building` Skill: its
[`team-patterns.md`](../src/Huddle.App/Skills/Defaults/team-building/team-patterns.md)
holds the full table, and is not repeated here. In one line each, a team gives:

- **A context kept for one job.** Each Agent's session holds only its own
  instructions and work, so its attention is not diluted. A specialist can also
  keep its own library of notes and references in its Work Dir, which no other
  Agent has to carry: an SEO Specialist and an Ads Specialist each stay expert
  in one field and trade conclusions by Mention, never their libraries.
- **Independent first answers.** Panellists Mentioned in one Message each answer
  before seeing the others.
- **Secrets.** A Persona's body reaches only its own Agent, so a character can
  hold goals the other characters never see.
- **Different blind spots.** The Model is per Persona, so one panel can span
  vendors.
- **Memory that outlives a session.** An Agent forgets its conversation on
  restart but not its files, so a Keeper remembers by writing notes.

A team is **not** for raw accuracy on a plain question, where one strong agent
usually does as well, or for sequential work where each step depends on the last.
The Skill tells the Chief of Staff to say so and propose a single Agent.

### The test of a team

Agents on the same model, working from the same context, share blind spots and
agree with each other as easily as one agent agrees with itself. More Agents
add cost, not quality. The gain comes from three things together: **different
roles**, **separate context**, and **a real review or test step**. A published
study of why multi-agent systems fail (Cemri et al., arXiv 2503.13657) groups the
failures the same way: poor role design, agents out of step with each other, and
weak verification. Judge any team, a Chief of Staff Proposal included, by those
three.

The working loop is **Generate → Challenge → Verify**. One Agent produces the
work, a second with its own context hunts for flaws, and a third step checks the
result against something real: a test, a source, or the Human. Without the last
step it is two Agents agreeing.

| Part of the loop | In Huddle |
| --- | --- |
| Different roles | Each Persona has its own instructions, Skills and Title |
| Separate context | One Room Session per Agent and Room, and a Persona's body reaches only its own Agent |
| Different blind spots | The Model is chosen per Persona, so a challenger can run on another Model |
| Verify | The Human at the end, or an Agent whose Persona tells it to run the tests. Huddle has no built-in test step, so a Proposal that has only a Generate and a Challenge Agent has no Verify |

What a team does **not** fix:

- **Collaboration is not automatic value.** Conversation between Agents is not
  a review. Mention a challenger to attack a specific piece of work, not to chat.
- **Cascading errors.** If one Agent makes a wrong assumption early and the
  next trusts it, every later step builds on it, and the work looks more finished,
  not less. Verify at each hand-off, not only at the end.
- **Cost.** Every hand-off is more model calls. The Budget and `Acp:TokenBudget`
  bound it; they do not make a team cheaper than one agent.
- **Mental load moves, it does not vanish.** Setting up roles and reviewing a
  team's output is work. The payoff is on repeated or long-running jobs.

Teams are wired one of three ways: **centralized** (one coordinator routes all
work, easy to follow but a bottleneck), **peer-to-peer** (Agents talk directly,
flexible but hard to track and prone to drift) or **hierarchical** (leads
supervise sub-teams, scales but costs more hand-offs). Huddle is peer-to-peer by
Mention, and an Agent that routes for the others gives it the centralized
shape. Start with one Agent; add a team when coordination, not capability, is
what slows you down.

### What the code must keep true

Each benefit rests on a behaviour of the chat surface that no test names as a
feature. A refactor that changes one of these removes the benefit silently:

| Benefit | Rests on | Where |
| --- | --- | --- |
| A context kept for one job | One ACP session per Persona, never shared between Personas. The same fact makes context bleed between one Agent's own Rooms, so focus holds only while it stays in the Rooms for its job | The cardinality diagram in [Relationships](#relationships); [Known limits](engineering/known-limits.md) |
| Independent first answers | A Turn's prompt is fixed when its Mention arrives: the read loop takes the Catch-up and builds the work item there, not when the Turn starts, so a Turn queued behind another still cannot see replies posted meanwhile | `PersonaRunner.cs:283-284` |
| Secrets | `list_agents` shows other Agents a Persona's frontmatter fields, minus `_`-prefixed and excluded keys, and never its body. A secret belongs in the body; a frontmatter field is public to every Agent | `PersonaFrontmatter.ComposeJobDescription`, `ListAgentsTool` |
| Different blind spots | The Model is stored per Persona and applied at session start | `PersonaModelStore` |
| Memory that outlives a session | The Work Dir is created when missing and never deleted by the app. `run.ps1 -Clean` leaves it alone. A rename moves it, and gives up with a logged warning if the old process still holds it | `DotAcpAgentHostFactory.cs:89-90`, `run.ps1:27-31`, `PersonaRenameCascade.cs:225-264` |

## Relationships

- A **Room** has two or more **Members**; every Room includes the **Human** in
  this version.
- A **Room** is defined by its **Members**, never by a type or a name. Nothing is
  persisted about "kind".
- An **Agent** belongs to at most one two-Member Room at a time, and any number
  of larger ones.
- A **Message** belongs to exactly one **Room** and lives in that Room's
  **Transcript**.
- A **Message** may carry zero or more **Mentions**, each resolving to one
  **Member**.
- Reconnecting under the same name re-attaches to the same **Agent** id.
- An **Invitation** adds a Member to a Room and renames it after its Agents.
- A **Persona** brought online registers exactly one **Agent**, with one session
  spanning every Room that Agent is in. Its identity is its frontmatter `name`,
  not its filename and not its folder — a **Team** is a label in its `teams`
  field, so a Persona may belong to several Teams or none, and moving its file
  between Team sub-folders changes nothing at all.

Cardinality down the ACP side is 1:1:1:1 — one Persona file produces one runner,
one session, and one registered identity:

```text
1 app
└── 1 PersonaSupervisor
    └── N PersonaRunner          one per Persona, keyed by Persona name
        ├── 1 pipe connection  → 1 Agent in the Team Directory
        ├── 1 ACP session        spans every Room that Agent is in
        ├── 1 AppToolServer      loopback Kestrel
        └── per-Room catch-up buffers   (state, not instances)
```

Nothing here is per-Room. That is exactly why context bleeds between Rooms — see
[Known limits](engineering/known-limits.md).

## Solution layout

All six projects live in the one solution, `Huddle.slnx`. Dependencies point
downward only:

```text
Huddle.App    ──────────┐        Blazor Server: the chat UI, the pipe server,
   │                    │        the Persona library, the Persona supervisor
   │                    ↓
   │              Huddle.Acp     reusable ACP client: spawns an agent process,
   │                    ↑        talks JSON-RPC over its stdio, hosts MCP tools
   ↓                    │
Huddle.Contracts   Huddle.Console  a terminal REPL against one agent — the
(wire protocol)                    original ACP proof of concept
```

- **`Huddle.Contracts`** — the named-pipe wire protocol. Records and JSON only, no
  behaviour. Both the app and any external client depend on it.
- **`Huddle.Acp`** — knows nothing about chat. Given a command line it starts an
  agent process, opens a session, streams events, and can expose your own C#
  methods to the model as MCP tools.
- **`Huddle.App`** — the application. Depends on both.
- **`Huddle.Console`** — a separate, standalone REPL. **Not part of the chat app.**
- **`Huddle.MockAdapter`** — the `mock-acp` test adapter. It links three fake-agent files out of `tests/Huddle.Acp.Tests/Fakes/` rather than copying them; see the root `CLAUDE.md`.
- **`Huddle.Seeder`** — `huddle-seed`, a console app that rebuilds a seed data folder in one command. See [Data seeding](../agents/DataSeeding.md).

**Project names and namespaces differ, deliberately.** Every project, assembly,
folder and the solution file are `Huddle.*`; every namespace inside them is
`Agency.Huddle.*`. The project `Huddle.Acp` builds `Huddle.Acp.dll` and declares
`Agency.Huddle.Acp`. The two are independent in .NET; `<RootNamespace>` in each
of the six `.csproj` files is what carries the split, and `AcpReferenceTests`
pins the assembly name so the project side cannot drift by accident.

> [!NOTE]
> `src/Huddle.Console`, `tests/Huddle.Acp.Tests` and `tools/acp` are owned by a
> parallel effort. Expect them to change under you; do not edit them without a
> reason. The solution file, `Huddle.slnx`, is shared by both efforts.

### Two bounded contexts

The vocabulary in [Language](engineering/language.md) governs **`Huddle.App`**
and **`Huddle.Contracts`** only.

**`Huddle.Acp` uses ACP's own words and is deliberately untouched by the
*vocabulary* rename.** The 2026-09-12 *namespace* rename did reach it — it
declares `Agency.Huddle.Acp` now — but that moved the namespace root only, and
not one type name. ACP is the *Agent Client Protocol*, so `IAgentHost`,
`IAgentSession`, `AgentSessionOptions` and `DotAcpAgentSession` are already
correct domain language inside a library whose whole value is not knowing about
chat. Pushing Team's vocabulary into it would break the layering the dependency
arrows exist to protect.

The boundary holds empirically: there are zero occurrences of chat vocabulary in
`Huddle.Acp`, `Huddle.Console` or `tests/Huddle.Acp.Tests`.

## Configuration

All under the `Team:` section — `TeamOptions.cs`, `Acp/AcpOptions.cs`, `FileChanges/FileChangesOptions.cs` and `Tasks/TasksOptions.cs`.

| Key | Default | Note |
| --- | --- | --- |
| `DataDir` | `App_Data` | Everything created at runtime lives here. |
| `PipeName` | `team` | |
| `HumanName` | `You` | The Human's Name on a blank system only. Once `{DataDir}/profile.json` exists (Settings > Profile, `HumanProfileStore`), that file wins; read the live Name through `IHumanIdentity`, never from this key. |
| `HelperConcurrency` | `2` | How many Helper runs may happen at once, across all Helpers. A Helper is a background agent that makes one judgement and lets Huddle act on it ([the Helpers design](Huddle.Helpers-Specifications.md) §6.3). It sits outside `Team:Helpers` because the configuration binder cannot mix a dictionary's entries with ordinary properties on one section. |
| `Team:Helpers:<Id>:Enabled`, `Adapter`, `Model`, `Effort`, `DailyCap`, `ContinueGrant`, `WaitSeconds`, `TimeoutSeconds` | the Helper's code default | Per-Helper defaults, such as `Team:Helpers:NeedsHuman:DailyCap`. A value left out keeps the Helper's code default; a choice saved on the Helper's card in Settings › Helpers wins over all of them. `NeedsHuman` defaults to on, Adapter `claude`, Model `haiku`, Effort `low`, 50 calls a day, a 10-second wait and a 60-second timeout. Helpers run only while `Team:Acp:Enabled` is true. |
| `AgentMessageBudget` | `40` | Agent-authored Messages one Room takes between Human Messages. Any Human Message resets it; the Room view offers the Human one more Budget at a time. **Zero or less removes the runaway-loop guard entirely.** |
| `DemoAgent:Enabled` | `false` | The echo agents. Off by default; set `true` to get the `echo` and `alpha` Rooms. |
| `DemoAgent:Names` | `["echo", "alpha"]` | |
| `Acp:Enabled` | `false` | **Spends money when true.** |
| `Acp:Command` | `node` | |
| `Acp:AdapterPath` | `null` | Otherwise located by probing upward. |
| `Acp:Args` | `null` | |
| `Acp:AgentsDir` | `Agents` | Relative to `DataDir`. One definition file per Agent folder, scanned one level; the old `Acp:TeamsDir` key throws at startup rather than silently scanning nothing. |
| `Acp:WorkDir` | `work` | The Work Dir sub-folder inside each Agent folder. **It is an Agent's durable memory**: Keepers and Specialists with a library store their notes there, so the app never deletes it, `-Clean` leaves it alone, and a removed Persona's folder is orphaned rather than deleted. Keep it that way. See [What a team is for](#what-a-team-is-for). |
| `Acp:SkillsDir` | `Skills` | Relative to `DataDir`. Holds Skill folders: an override of a shipped Skill, file by file, or a Skill written by hand. Created at startup. See [ADR-0021](adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md). |
| `Acp:MaxAgents` | `8` | **Retired.** Still bound so an old configuration loads, but no code enforces it. The caps are now per Team (`Team:Acp:MaxAgentsPerTeam`) and per installation (`Team:Acp:MaxLiveProcesses`). |
| `Acp:TraceWire` | `false` | **Dumps the bearer token.** Debugging only. |
| `Acp:CatchUpMessages` | `20` | Per-Room catch-up buffer size, used on every Turn after a Room Session's first. |
| `Acp:TokenBudget` | `1000000` | Per-Persona token Budget, summed from the rises in `UsageUpdated.Used` across every Room Session, and reset by any Human Message. Catches a loop that mints fresh Rooms, which the per-Room Budget cannot. Zero or less disables it. |
| `Acp:SessionIdleMinutes` | `30` | RS §6.2, §6.14. How long a Room Session may sit `Idle` — its queue empty, nothing dequeued — before the pool's sweep closes it. It stays resumable: its stored id survives in `RoomSessionStore` until Restart, a Persona change or removal. Zero or less never closes an idle Room Session. |
| `Acp:MaxLiveSessions` | `3` | RS §6.2, §6.14. The most Room Sessions one Persona may hold open at once. Opening beyond it first closes the least recently used `Idle` one, or waits for one to become idle; a value below `MaxConcurrentTurns` is raised to it, with a startup warning. With `Acp:MaxAgents` at 8, the worst case is 8 Adapter processes and 24 Claude Code CLI children (RS §6.14). |
| `Acp:MaxConcurrentTurns` | `1` | RS §6.2, §6.14. How many of one Persona's Room Sessions may run a Turn at once, gated by `TurnGate`. `1` is today's behaviour: Turns across every Room run serially, in ticket order (finding P-4), exactly as one shared session always has. |
| `Acp:TranscriptCatchUpMessages` | `20` | RS §6.5, §6.14. The most Messages a Room Session's **first** Turn carries from the Transcript, ending strictly before the Message that started the Turn. Distinct from `Acp:CatchUpMessages`, which sizes the in-memory buffer every later Turn uses. |
| `Acp:TranscriptCatchUpTokens` | `100000` | LR-R7. The most estimated tokens (3 characters each) a first Turn rereads; Settings › Sessions may override. |
| `Acp:CatchUpKeepOwnMessages` | `5` | LR-R11. The Agent's newest own Messages a trim keeps. |
| `Acp:TurnIdleTimeoutSeconds` | `180` | The longest an Adapter may say **nothing** during one Turn. Bounds silence, not duration: any event restarts the clock, so a long tool-using Turn that keeps reporting progress never trips it. Firing sends `session/cancel`, reports Degraded and counts toward the consecutive-failure streak — it is a failure, never a Stop. Zero or less disables it. Exists because an Adapter on an unreachable endpoint may return nothing at all, which reads as *hung* rather than Degraded. |
| `Acp:UserInputTimeoutSeconds` | `600` | The longest, in seconds, a form an Agent put to the Human (an `AskUserQuestion`, the retry-after-refusal dialog, an MCP form) may wait for its answer before it is dropped and the Agent is told it was cancelled. While a form is open `Acp:TurnIdleTimeoutSeconds` is paused, so this is the one thing that stops a Persona waiting on an absent Human for ever. Below `30` is raised to `30` and above `86400` is cut to a day, so it can never be switched off. Read live when each form arrives. Hitting it is neither a Turn timeout nor a failure, and it does not release the Turn's slot. |
| `Acp:AdvertiseElicitation` | `true` | Whether `initialize` advertises `clientCapabilities.elicitation.form` (form mode only, never `url`). On, Claude's built-in `AskUserQuestion`, the retry-after-refusal dialog and MCP forms reach the Human as cards in the Room; off restores the old behaviour exactly (nothing advertised, `ask_human` the only way to ask). Read when a Persona's Adapter process starts, so a change applies at its next start. |
| `Acp:Adapters` | `null` | The Adapters this installation can launch, in configuration order; the **first is the default**. Absent means exactly one profile synthesised from `Acp:Command` / `Args` / `AdapterPath`, so a stock install is unchanged and the Adapter select does not render. Per entry: `Id`, `DisplayName`, `Description`, `Command`, `Args`, `AdapterPath`, `UsesToolNamePrefix`, `EnvironmentOverrides`, `ReadsFiles`, `IsolateUserSettings`. A blank `Command` or a duplicate `Id` throws at **startup**, not at first Turn. See [ADR-0013](adr/0013-an-adapter-is-a-property-of-the-persona.md). |
| `Acp:Adapters:*:EnvironmentOverrides` | `null` | Environment variables set on that Adapter's process, over and above the inherited environment — how an adapter that ships no `appsettings.json` of its own gets its configuration. **Set these from `appsettings.json`, never through the environment-variable provider:** that provider rewrites every `__` into `:`, so `Team__Acp__Adapters__0__EnvironmentOverrides__Agent__DefaultModel` binds as the key `Agent:DefaultModel`, which no process will ever read. |
| `Acp:Adapters:*:ReadsFiles` | `true` | Whether this Adapter's agent process can read files at all (FC §6.11). `false` turns off the Watched Folder list, `watch_folder`/`unwatch_folder` and frontmatter `watches` for every Persona on that Adapter; `agency-acp`, which has no file tools, sets it `false`. |
| `Acp:Adapters:*:IsolateUserSettings` | `false` | Whether a session on this Adapter is started with the isolation `_meta` (RS §6.10 "Recommended"): `settingSources: ["project", "local"]` and `settings.autoMemoryEnabled: false`. The synthesised legacy profile sets it `true`, because `agency-acp` ignores the `claudeCode`-shaped entry anyway. **Unverified live** — see [Known limits](engineering/known-limits.md) and [manual-tests/file-changes.md](engineering/manual-tests/file-changes.md) — and known to drop a `CLAUDE_MODEL_CONFIG` model override when on, because `claude-agent-acp`'s `settings` option replaces its computed settings rather than merging. |
| `Acp:Adapters:*:Commands` | `null` | Names of the Adapter commands a Human may run on an Agent on this Adapter, by Mention: `@Nova /compact`. Absent means none. The synthesised legacy profile is `["compact"]`. Compared case-insensitively; a name the Adapter does not advertise is simply not offered. Read at startup, so a change needs a restart. See [ADR-0035](adr/0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md) and [the design](Huddle.Commands-Specifications.md). |
| `Acp:Adapters:*:SessionPerRoom` | `true` | RS §6.12. Whether this Adapter runs one Room Session per (Persona, Room) or the pre-Room-Sessions shape, one session shared across every Room. `false` maps every Room to the one shared session and tells the Agent the shared-session Prompt instead of the per-Room one (RS §6.9). It is a fact about the Adapter, not a global switch — `agency-acp` reports `loadSession: false` and neither its `resume` support nor whether it holds several sessions per process is verified (Appendix B V-5), so an installation running it should set this `false` until V-5 passes. Changing it restarts every Persona on that profile and forgets their stored Room Session ids (RS §9 E-12). |
| `Acp:HiddenModes` | `null` (meaning the default `["bypassPermissions", "auto", "plan"]`) | The Work Mode ids never offered in the picker and never sent to an Adapter; enforced at session open as well as in the picker, so a hand-edited row for one is dropped with a warning. A list replaces the default exactly. To hide nothing, give a list holding **one empty string** (`Team:Acp:HiddenModes:0` set to an empty value): configuration cannot express an empty list. Read at startup. See [Work Modes design](Huddle.WorkModes-Specifications.md). |
| `Desktop:Enabled` | `false` | Desktop host mode, set by the installed launcher (never by tests or `run.ps1`). When `true` the app takes a single-instance file lock `<DataDir>/instance.lock`, writes `<DataDir>/instance.json` (URL, stop token, process ID), serves the loopback-only `POST /_desktop/stop`, and a second launch opens the running instance instead of starting another. See [installer.md](engineering/installer.md). |
| `Desktop:OpenBrowser` | `true` | Whether to open the browser automatically on startup (applies only when `Desktop:Enabled` is `true`). |
| `FileChanges:Enabled` | `true` | Whether File Changes runs at all (FC §6.14). `false` passes a `null` tracker to every runner and offers neither `watch_folder` nor `unwatch_folder`. |
| `FileChanges:Ignore` | `[".git", "node_modules", "bin", "obj"]` | Directory names `FolderScanner` prunes, compared case-insensitively. |
| `FileChanges:MaxFilesPerFolder` | `5000` | Above this a folder scan reports `TooLarge` rather than walking it. |
| `FileChanges:MaxListed` | `50` | The most File Changes lines listed per Turn, across every Watched Folder. |
| `DocHistory:Enabled` | `true` | Whether document History runs at all ([DocHistory spec](Huddle.DocHistory-Specifications.md) §6.9). `false` offers no `log_change`, records no Human saves and shows no byline, History or History panel. The history files stay where they are. |
| `DocHistory:MaxEntries` | `500` | Entries kept per document; the oldest are dropped on the next append. |
| `DocHistory:MaxSummaryLength` | `200` | The longest summary `log_change` accepts. |
| `DocHistory:HumanSaveGroupWindow` | `00:05:00` | How close consecutive Human saves must be to share one row in the panel and the History. |
| `Feed:Enabled` | `true` | Whether the Feed runs at all ([Feed spec](Huddle.Feed-Specifications.md) §6.11). `false` registers a publisher that does nothing, starts no writer and draws neither the Notifications button nor the Feed link. The `feed_events` rows stay where they are. |
| `Feed:DismissedRetentionDays` | `30` | A dismissed Notification is deleted this many days after its dismissal. Zero or less keeps them forever. |
| `Feed:ActivityRetentionDays` | `30` | An Activity item is deleted this many days after it was created. Zero or less keeps them forever. |
| `Feed:CleanupInterval` | `01:00:00` | How often the clean-up of expired rows runs. |
| `Feed:QueueCapacity` | `10000` | Commands waiting for the writer; beyond it new ones are dropped and logged. |
| `Feed:BatchSize` | `200` | The most commands one writer transaction applies. |
| `Feed:PageSize` | `50` | Items per Feed Tab page. |
| `Feed:PanelLimit` | `50` | The most items the Notifications panel loads before "Show more". |
| `FileChanges:MaxMemoryEntries` | `100` | The most Memory lines shown in a new session's system prompt; the rest are counted rather than listed. |
| `Team:Instructions:MaxChars` | `8000` | The most characters one `Instructions.md` contributes to a session's system prompt; a longer file is cut and the cut line names the file. Zero or less means no cap. Instructions and Memory §3.2. |
| `Team:Memory:MaxFileBytes` | `16384` | The most bytes a memory file may hold after a `write`, `append` or `replace` through the `memory` tool; a larger result is refused with "split it into topics". Zero or less means no cap. Instructions and Memory §5.7. |
| `Tasks:Enabled` | `true` | `false` hides the UI, offers no tools and wakes no one. The Task files stay where they are. |
| `Team:Teams:Dir` | `Teams` | Team folders, relative to `DataDir`. Shared with Tasks, and replaces `Tasks:Dir`. Must not overlap `Team:Acp:AgentsDir`; start-up refuses it otherwise (`LayoutGuard`). |
| `Team:Teams:MaxMemoryEntries` | `50` | The cap on Team Memory entries indexed into a member's system prompt at session start, across all of a Persona's Teams; the rest are counted rather than listed. Spec §7.3. |
| `Team:Teams:Tiers:<adapterId>:<tier>` | none | The model id for each model tier (`strongest`, `balanced` or `fast`) on each Adapter, such as `Team:Teams:Tiers:agency-local:strongest`. An Adapter with no entry for a tier uses its default model. With no `Tiers` key at all, every hire card shows **Needs setup**. |
| `Team:Acp:MaxAgentsPerTeam` | `8` | The most Agents one Team may hold. A hire that would take the Team past it is refused. Zero or less disables the limit. (HR-10, decision D1.) |
| `Team:Acp:MaxLiveProcesses` | `16` | The most live Agent processes this installation runs across every Team. A hire that would take the live count past it is refused. Zero or less disables the limit. (HR-10, decision D1.) |
| `Team:AvatarLibrary:Dir` | unset (the bundled library) | An **absolute** path to an avatar library folder of your own, holding `master.json` and the images. **Unset uses the library that ships with the app** (`src/Huddle.App/AvatarLibrary`, copied beside the app), so the **Choose from library** button works with no configuration. A path you set replaces it, and is never backed by it: a folder that does not exist means no library, with no button and no `/avatar-library` mount, which is how the shipped library is switched off. A folder without `master.json` has no button, but its pictures are still served at `/avatar-library`, because the mount follows the folder. Absolute because a library of your own is not under `DataDir`. ([ADR-0044](adr/0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md), [ADR-0048](adr/0048-the-avatar-library-ships-with-the-app-and-is-the-default.md).) |
| `Team:SkillSourcesDir` | empty (`{DataDir}/SkillSources`) | The **Skill Sources folder**: `SKILLS.md`, the Pack and catalog files and each Source's notes. A folder chosen on Settings › Skills › Sources is saved in `{DataDir}/skill-sources.json` and **wins over this key**. Point several Huddles at one shared folder to share their Skills. See [the Skill Packs design](Huddle.SkillPacks-Specifications.md) §8.7. |
| `Team:ComputerName` | empty (`Environment.MachineName`) | The name a per-computer path in `SKILLS.md` (`DESK-01: E:\…<br>LAPTOP: C:\…`) is matched against, ignoring case. Set it when two computers report the same machine name. |
| `Team:SkillsRepositoryDir` | empty | Where this computer's copy of the skills repository is. **Read once, by the move** that turns the old `SkillPacks/` into `SKILLS.md` rows; empty means the move adds no rows for the two Packs Huddle used to ship. Not shown anywhere in Settings. |
| `Team:HuddlePacksPath` | empty (`huddle-packs.exe` beside the app, else `dotnet huddle-packs.dll`) | The `huddle-packs` program `InstallSkillPack.md` tells an AI tool to run for validating. |
| `Tasks:WakeEnabled` | `true` | `false` keeps Tasks but never wakes anyone. |
| `Tasks:WakeCoalesceSeconds` | `5` | How long a Task's changes are coalesced before one wake-up is sent. `0` wakes on every change. |
| `Tasks:AgentWakeBudget` | `10` | The most Agent-made wakes one Task allows before pausing. `0` or less disables the per-Task budget. |
| `Library:Enabled` | `true` | `false` hides the pane, the page, the sidebar link and chat links. |
| `Library:Roots` | `[]` | Pinned roots: `[{ "Name": "Huddle docs", "Path": "E:\\Repos\\Huddle\\docs" }]`. |
| `Library:MaxEditableBytes` | `2097152` | Above this, a text file opens read-only. |
| `Library:MaxIndexedFiles` | `5000` | Above this, a root's wikilink index is not built and backlinks say so. |
| `Team:Library:PropertyTypesFile` | `.obsidian/types.json` | The file inside every Library Root that assigns a type to a front matter key, `/`-separated and relative to the root. It is read even though its folder is hidden; a missing or invalid file falls back to typing by value. Empty turns it off. See [Front matter properties](Huddle.FrontMatterProperties-Specifications.md). |
| `Library:MaxReferencedDocuments` | `10` | The most Library documents listed in one Turn's prompt; the rest are counted. |
| `Team:Library:MaxUploadBytes` | `26214400` | The largest file attached in the chat (25 MiB), enforced while the bytes stream in. Over it, the chip is refused and the partial file removed. See [ADR-0053](adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md). |
| `Team:Library:MaxUploadsPerMessage` | `10` | The most files attached to one Message, matching `MaxReferencedDocuments`. |
| `Team:Library:Uploads:Dir` | `Uploads` | The Uploads Library Root's folder, a relative name under `DataDir`; an absolute value is not supported (nothing validates it, and it would leave `DataDir`). Created on the first upload, not at start-up. |
| `AllowedHosts` (root, not under `Team:`) | `localhost;127.0.0.1;[::1]` | ASP.NET Core host filtering. It was `*`; the Phase 0 commit of [Remote access](Huddle.RemoteAccess-Specifications.md) narrowed it. Requests with another `Host` get 400. While Huddle is bound beyond loopback or runs Tailscale Serve, the gateway adds the bound addresses, `{name}.local`, the MagicDNS name, `CustomBindHost` and `Team:Gateway:ExtraHosts` on top (Spec §6.4). The installer sets `localhost;127.0.0.1`. |
| `Team:Gateway:StrictLocal` | `true` | Only this computer, no sign-in. While on, the gate admits only the loopback ingress (other requests get 403 `strict_local`), the host gets every scope without a session, and Bind, Tailscale and trusted proxies are forced off at startup. A choice saved in Settings › Remote access wins. Turning it on applies at once; turning it off applies after a restart. Tests that exercise sign-in or remote ingress set it to `false`. (Spec §6.5a) |
| `Team:Gateway:Bind` | `loopback` | `loopback`, `tailnet`, `lan`, `auto` (any address inside a container, loopback elsewhere) or `custom`. Anything but `loopback` is plain HTTP, called downgraded. A choice saved in Settings › Remote access wins; a change applies after a restart. |
| `Team:Gateway:CustomBindHost` | none | The IP literal `Bind = custom` listens on. |
| `Team:Gateway:Port` | the app's own port | The port a non-loopback bind uses. An installed copy binds `127.0.0.1:0`, so a non-loopback bind there needs this key; without it Huddle starts loopback-only and says why. |
| `Team:Gateway:Auth:Mode` | `token` | `token`, `password`, `none` (loopback only; a non-loopback bind with `none` is refused) or `trusted-proxy`. |
| `Team:Gateway:Auth:Token`, `Auth:Password` | generated token; no password | Environment `HUDDLE_GATEWAY_TOKEN` and `HUDDLE_GATEWAY_PASSWORD` apply when the key is unset; a value saved in Settings applies last. A blank or placeholder value throws at start. |
| `Team:Gateway:Auth:AllowTailscale` | `true` | Verified Tailscale Serve identity satisfies gateway auth, on routes Huddle created only. |
| `Team:Gateway:Auth:RateLimit:MaxAttempts`, `WindowSeconds`, `LockoutSeconds`, `ExemptLoopback` | `10`, `60`, `300`, `true` | Failed gateway auth: 10 failures in 60 seconds lock the address out for 5 minutes. Loopback is slowed, not locked out. |
| `Team:Gateway:AutoApproveLocal` | `true` | A device on loopback pairs without approval. |
| `Team:Gateway:Tailscale:Mode` | `off` | `off`, `serve` or `funnel`. Funnel is refused unless `Auth:Mode` is `password`. |
| `Team:Gateway:Tailscale:HttpsPort` | the app's port | The port the `ts.net` address uses. Funnel accepts only 443, 8443 and 10000. |
| `Team:Gateway:Tailscale:Command` | `tailscale` | The executable, used when `tailscale` is neither on `PATH` nor at the platform default. |
| `Team:Gateway:TrustedProxies`, `TrustedProxy:UserHeader` | none | Source addresses of an identity-aware proxy and the header carrying its user, for `Auth:Mode = trusted-proxy`. |
| `Team:Gateway:PublicOrigin`, `AllowedOrigins`, `DangerouslyAllowHostHeaderOriginFallback` | none, none, `false` | The Origin check (Spec §8.7). `AllowedOrigins` defaults to `PublicOrigin`; `*` allows any. The Security check flags the fallback. |
| `Team:Gateway:ExtraHosts` | none | Further names the host filter and the plain-HTTP rule accept. |
| `Library:MaxInlineBytes` | `16384` | Per document, the most text inlined for an Adapter without file tools. |
| `Library:MaxImageBytes` | `3145728` | The most bytes of one image sent to an Adapter as a Prompt block (3 MiB, 4 MiB once base64-encoded). Over it, the image is a path line. Zero or less sends none. |
| `Library:MaxImagesPerTurn` | `4` | The most image Prompt blocks in one Turn's prompt; the rest stay path lines. |
| `Library:MaxImageBytesPerTurn` | `8388608` | The most raw image bytes in one Turn's prompt, about a third more on the wire. |
| `Library:MaxImageEdgePixels` | `8000` | The longest side, in pixels, of an image sent as a Prompt block, read from its header. |
| `Search:LibraryParallelism` | `4` | How many Library Roots Global Search walks at once; below `1` is raised to `1`. See [the design](Huddle.GlobalSearch-Specifications.md) §10. |
| `Search:MaxContentFileBytes` | `1048576` | Above this, Global Search matches a Library file by name only and never reads it. |
| `Search:TextExtensions` | `.md`, `.markdown`, `.txt`, `.json`, `.yaml`, `.yml`, `.csv`, `.log`, `.xml`, `.cs`, `.js`, `.ts`, `.css`, `.ps1` | The only extensions whose contents Global Search reads, compared ignoring case. A configured list replaces the default. |
| `Search:MaxVisitedEntries` | `5000` | **Assumed; the owner has not confirmed (OP-2).** How many entries Global Search visits in one Library Root's walk before it stops and says *Results may be incomplete.* A visit budget, not an index: search keeps no index of its own, and nothing is kept after a search. Separate from `Library:MaxIndexedFiles`, which caps the per-root wikilink index (`WikiLinkIndex`) that search does not use. |
| `References:ExpandInPrompt` | `true` | Whether a Turn's prompt gets the Referenced items header. `false` leaves prompts byte-identical to today's. The picker, the links and the snapshot do not depend on it. See [the design](Huddle.AtTagging-Specifications.md) §9 and §10. |
| `References:MaxPromptChars` | `2000` | The most characters of item lines in one header. Below `200` is raised to `200`. See [the design](Huddle.AtTagging-Specifications.md) §9.3. |
| `Acp:Adapters:*:PromptBlocks` | `true` | Whether to send Prompt blocks to an Adapter that advertises it can take them. A kill switch only: `false` for an Adapter that advertises a capability it does not honour; it can never turn blocks on for one that did not advertise. The synthesised legacy profile is `true`. Read at startup. See [ADR-0036](adr/0036-a-prompt-block-is-sent-only-when-the-adapter-advertised-it.md) and [the design](Huddle.PromptBlocks-Specifications.md). |

`{DataDir}/session-settings.json` holds only the Settings › Sessions overrides of `Acp:TranscriptCatchUpMessages` and `Acp:TranscriptCatchUpTokens`. Absent is normal, and Settings › Sessions writes it. It is hand-editable and watched, so an edit applies from the next fresh session without a restart (Spec §5.2, LR-R9, LR-R10).

An installation running `agency-acp`, unverified for resume or for several sessions per process
(RS Appendix B V-5), sets that Adapter's `SessionPerRoom` to `false` until V-5 passes:

```jsonc
"Team": { "Acp": { "Adapters": [
  { "Id": "agency-local", "DisplayName": "Agency (local)", "Command": "agency-acp",
    "UsesToolNamePrefix": false, "SessionPerRoom": false }
] } }
```

Four runtime files and two runtime directories sit outside that section, because none of
them is a setting: the Persona library under `{DataDir}/{Acp:AgentsDir}`,
`{DataDir}/prompts.json`, `{DataDir}/appearance.json`, `{DataDir}/avatars.json`,
`{DataDir}/sprints.md`, the uploaded avatar images under `{DataDir}/avatars/`, and
Skill folders under `{DataDir}/Skills/`.

| File | Holds |
| --- | --- |
| `{DataDir}/prompts.json` | **Overrides only**, one key per changed Prompt. Absent is normal and means nothing is overridden; the app does not create it, and it appears on the first save from `/settings`. Hand-editing it is supported and watched — a save in an editor reaches the next Turn without a restart. |
| `{DataDir}/appearance.json` | The selected Theme id. **That one key** — the per-Token override map went with the Tokens on 2026-09-14 ([ADR-0010](adr/0010-a-theme-is-a-mudblazor-theme.md)), and the `dark` light/dark preference went on 2026-09-21 when a Theme became a single palette ([ADR-0017](adr/0017-a-theme-is-a-palette-not-a-pair.md)). A file written before that still carries `dark`; it is an unknown key now, so it is ignored and kept, and there is no migration. Absent is normal and means the default Theme; the app does not create it. Hand-editable and watched, exactly like `prompts.json`. Not a setting under `Team:`: it is state this application writes. |
| `{DataDir}/avatars.json` | One entry per Agent that has chosen an **Avatar**, keyed by Name — the Human included, since the Human has a Name but no Persona file. **Overrides only**, exactly like `prompts.json`: an absent file is normal, the app does not create it, and an Agent with no entry renders the initials it always did. Hand-editable and watched. Deliberately not part of the Persona, so changing an avatar never restarts a session ([ADR-0019](adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md)). |
| `{DataDir}/sprints.md` | The list of Sprints: one line per Sprint with id and name, created when a Task is assigned to a new Sprint and removed when the last Task leaves it. Hand-editable; a rename is propagated to every Task without rewriting the file. See [Huddle.Sprints-Specifications.md](Huddle.Sprints-Specifications.md) (Spec §6, §8). |
| `{DataDir}/avatars/` | The uploaded avatar images themselves, each named by a generated id rather than by an Agent's Name. Served at `/agent-avatars` by a `PhysicalFileProvider` — `MapStaticAssets` is manifest-driven and cannot see a file written at run time. Created at startup, unlike the JSON files, because a `PhysicalFileProvider` throws when its root is missing. |
| `{DataDir}/Teams/<Team>/_team.md` | The **Team card**: a frontmatter block of single-quoted scalars, then free-form notes. Keys: `teamType` (required, `id@version`), `manager`, `workspace`, `owns`, `onboarded`, and the Team's avatar as three optional keys, `avatarLabel` (up to six text elements, as an Agent's), `avatarImage` (the file name of an image under `{DataDir}/avatars/`, never a path) and `avatarBackground` (a colour such as `#1E90FF`). The avatar keys are written only when set; an unreadable image name or colour is ignored. The avatar is here, and not in `avatars.json`, because that file is keyed by Name and shared with Agents and the Human. Unknown keys and the notes survive a rewrite. A `process` line from an older file is dropped on rewrite: the Process is always `Process.md` in the Team folder, read through `TeamProcessFile` (issue #312). See [ADR-0069](adr/0069-a-teams-avatar-lives-in-team-md-and-is-a-rounded-square.md). |
| `{DataDir}/TeamTypes/` | **Overrides and additions only**, one folder per team type id. A folder here with the id of a built-in type replaces it. Created by the Custom role's "Save for next time". See [ADR-0038](adr/0038-team-types-are-data-in-json-and-markdown-files.md). |
| `{DataDir}/SkillSources/` | The **Skill Sources folder** (or wherever `Team:SkillSourcesDir` or `skill-sources.json` puts it): `SKILLS.md`, `packs/*.skillpack.json`, `catalogs/*.skillcatalog.json`, `sources/<source>/` (`source.json`, `NOTES.md`, `log.jsonl`) and `hidden.json`. **The AI tool owns it**, not Huddle: Huddle reads and watches it, appends `log.jsonl`, writes `hidden.json`, and on Remove deletes a row's lines and files inside the folder. Shareable between Huddles. See [the Skill Packs design](Huddle.SkillPacks-Specifications.md) §7.1. |
| `{DataDir}/InstallSkillPack.md` | The instructions any AI tool reads to add Skills and Packs to this Huddle. **Generated** at start-up and when Roles, installed Skills or `SKILLS.md` change (at most once a second), so a hand edit is overwritten; change the fixed sections through the `installSkillPack.body` Prompt instead. |
| `{DataDir}/install-context.json` | Written with `InstallSkillPack.md`: this computer's name, the Skill Sources folder, the Roles and the installed Skills, for `huddle-packs` to read. Not for hand-editing. |
| `{DataDir}/skill-sources.json` | This Huddle's own Skills settings: the folder override `dir` and the `seen` keys behind the Skills badges. Nothing else. Absent is normal. |
| `{DataDir}/skill-sources-move.json` | The report of the one-time move from the old `SkillPacks/` and catalog into `SKILLS.md`, read once for the snackbar that reports it. The move runs only while `SKILLS.md` does not exist, so deleting this file does not run it again; deleting `SKILLS.md` does. |
| `{DataDir}/SkillPacks/` | **Retired.** The old Skill pack files. Read once by the move and never written; safe to delete after it. |
| `{DataDir}/connectors.json` | The connectors that are connected, such as `{"connected": ["email:read"]}`. Read on every check; a missing or malformed file means none is connected. |
| `{DataDir}/Skills/` | **Overrides and additions only.** A shipped Skill such as `team-building` lives in code as embedded resources; a folder here with the same name overrides it file by file (Source *Overridden*), and a folder with a new name is a Skill of the Human's own (*Yours*). An override whose `SKILL.md` is invalid falls back to the shipped one with a Warning; an invalid new Skill is left out. Watched: an edited file is what the next `read_skill` returns, while a changed name or description reaches an Agent's system prompt only at its next session. Created at startup, because its watcher throws when the root is missing. Settings › Skills lists it and restores a default by deleting the override folder. |
| `prompts.default.json` (beside the binary) | Every Prompt's shipped wording, **generated** from `PromptCatalog` and copied to the output folder. The restore source, and readable as a reference. It is not the authority: delete both files and the app still runs on exactly the text it shipped with. |

A Prompt is one piece of text sent to a model. See [Language](engineering/language.md)
for the word, [ADR-0007](adr/0007-model-facing-text-is-configuration.md) for why
defaults live in code, and [Rules](engineering/rules.md) for the two things an edit
must never do.

`PromptCatalog` holds **102** Prompts (`PromptCatalogTests` pins the count). Seven belong to
Skill Packs and are not sent to a model as such: `installSkillPack.body`, the fixed sections of
`InstallSkillPack.md`, and the six prompts the Human copies into an AI tool —
`skillPrompt.add`, `skillPrompt.findForRole`, `skillPrompt.update`, `skillPrompt.fix`,
`skillPrompt.fetchSkill` and `skillPrompt.needsSetup` (`SkillPromptBuilder`). They are Prompts so
the Human can reword them on Settings › Prompts like any other.

A Theme is an embedded JSON skin loaded into a MudBlazor `MudTheme` in `ThemeCatalog` ([ADR-0058](adr/0058-a-theme-is-a-palette-a-skin-and-traits.md)), carrying **one** palette and the
`ThemeMode` naming it, picked on the Appearance tab of `/settings` from two lists, Light theme and Dark theme.
**Picking a Theme picks light or dark with it** — there is
no separate preference, and so no way for the two to disagree
([ADR-0017](adr/0017-a-theme-is-a-palette-not-a-pair.md)). Eight ship, four light and dark pairs: Lumen (the default), Fluent, Nordic and Cobalt Soft,
mapped from the approved mockup directions. The Huddle pair, the Custom Accent Themes, the seventeen
Visual Studio Code ports and the High contrast group were removed
([ADR-0058](adr/0058-a-theme-is-a-palette-a-skin-and-traits.md); ADR-0016 is superseded). The Human's own
accent is a per-Theme customization in `appearance.json`, and `/settings/theme-lab` (Development only) tunes a
Theme live. A stored `huddle`, `huddle-dark` or Custom Accent id migrates to Lumen.
See [Language](engineering/language.md) for
**Theme**, **Palette property** and **Appearance** — note that *Token*, the word for
the retired 39-value CSS system, is no longer a defined term.
[ADR-0010](adr/0010-a-theme-is-a-mudblazor-theme.md) supersedes
[ADR-0009](adr/0009-a-theme-is-a-stylesheet-layered-over-the-tokens.md), which is kept as
the reasoning for the system this replaced; ADR-0017 then narrows 0010. Of the two things
0009 asserted that 0010 had to retract, one is true again: the feature uses no JavaScript
of its own, because nothing reads the device preference any more, so there is no
first-paint flash. The other stands retracted — changing a Theme still does not force a
page reload. The cost is that the application no longer follows the device's light/dark
setting at all; ADR-0017 records what bringing that back would take.
[Rules](engineering/rules.md) carries the three things a change here must not undo.

`Logging:LogLevel` is the one place that looks like it belongs to this section
and does not. Its keys are log-category prefixes, and a category comes from
`typeof(T).FullName` — so those read `"Agency.Huddle"`, the namespace root, while
everything in the table above stays under `Team:`. See
[Traps](engineering/traps.md) for what a stale `"Team"` key there does silently.

## Remote access command line

The `huddle` verbs run before the web host is built, so they never start the server. An unknown first word (no `-`, no
`=`) prints the usage and exits 2. The launcher sets `Team__DataDir`; the bare exe does not, so user-facing text says
`huddle …`. Design: [Remote access](Huddle.RemoteAccess-Specifications.md) §6.13.

- `huddle auth-token --show` prints the effective gateway token; it refuses when standard output is redirected (also
  true under mintty and Git Bash). `--rotate` replaces the stored token; restart Huddle for it to take effect.
- `huddle dashboard [--json]` opens the running desktop copy already signed in, or prints the link with `--json`. With
  no desktop copy running it prints "No desktop copy is running." and exits 1.
- `huddle devices list`, `approve [REQUEST_ID] [--scope SCOPE …]`, `reject REQUEST_ID`, and
  `revoke|remove|rotate --device DEVICE_ID`. They read `instance.json` in the data directory, or take
  `--url http://127.0.0.1:PORT` for a `dotnet run` copy, and authenticate to `/_gateway/*` with the gateway token.
- `huddle stop` asks the running copy to shut down.

## Build, test, run

```powershell
dotnet build Huddle.slnx                             # must be 0 warnings
dotnet test  Huddle.slnx --                          # 19 tests skipped when Acp:Enabled is off
./test-health.ps1                                    # PASS or FAIL: does the built app boot and answer /health
dotnet run --project src/Huddle.App --urls http://localhost:5100
```

> [!IMPORTANT]
> One thing about this is easy to get wrong, and one is easy to overpay for.
>
> **The trailing `--` is required.** Without it, `dotnet test` exits code 5 with
> "Zero tests ran" — a Microsoft Testing Platform CLI quirk under this SDK. It
> reads as a no-op, not as a failure.
>
> Only the `E2E/` folder of `Huddle.Acp.Tests` spends money, and it is gated on
> `TEAM_E2E=1`. Leave that unset and the whole suite is free.

`./test-health.ps1` is the only check that runs the app as a **process**. Every
test in `tests/Huddle.Tests.Hosted` hosts it in-process through
`TeamWebApplicationFactory`, so a bad options binding or a hosted service that
throws at startup passes the entire suite and fails only here. The script starts
the built output on a loopback port the kernel picks, GETs `/health`, prints PASS
or FAIL, and exits 0 or 1.

It never builds — run `dotnet build` first, or it tells you which command to run.
Add `-Configuration Release` to check what CI checks; CI runs the same script as
the last step of `validate`. It writes nothing outside a temp directory and
starts no `node` process: the child is configured entirely through command-line
arguments, which ASP.NET Core registers last and which therefore outrank
`appsettings*.json` and anything already in your shell.

Development configuration sets `Acp:Enabled: true`, so `dotnet run` starts one
`node` process per Persona at startup, before you type anything. To open the app
without that:

```powershell
$env:Team__Acp__Enabled = 'false'
dotnet run --project src/Huddle.App --urls http://localhost:5100
```

Open `http://localhost:5100`. A fresh install has no Rooms, because the demo
agents are off by default. To get the two demo Rooms, `echo` and `alpha`, start
the app with the demo agents on:

```powershell
$env:Team__DemoAgent__Enabled = 'true'
```

Click `echo`, type `hi @echo`, press Enter, and it answers in bold.

To talk to it as an external process instead, connect to the pipe:

```powershell
pwsh tools/echo-bot.ps1 -Name mybot
```

```text
Connecting to pipe '\\.\pipe\team' as agent 'mybot'...
Sent hello. Listening for messages (Ctrl+C to exit)...
{"type":"welcome","agentId":"01a08c...","name":"mybot","rooms":[{"id":"01a08d...","name":"mybot","members":[...],"isEmpty":true}],"version":3}
```

A Room named `mybot` appears in the browser immediately, with no refresh.

## Repository and toolchain rules

These four apply to every change, which is why they are here rather than in
[Rules](engineering/rules.md):

| Rule | Why |
| --- | --- |
| **Package versions live only in `Directory.Packages.props`.** | Central package management is on. A `Version` attribute on a `PackageReference` is an error. |
| **Warnings are errors.** | Inherited from the root build props. Watch for CA1859 (return concrete types) and CA1305 (culture). |
| **Tests are xunit v3 under the Microsoft Testing Platform runner.** | Selected in `global.json`, required on .NET 10. |
| **Trust the compiler, not the editor.** | The IDE language server reports large numbers of phantom errors in this repo. If `dotnet build` is clean, the code is fine. |
| **The product is Agency.Huddle. Two identifiers still carry the old code name `Team` and must never be renamed to match: the `mcp__team__` tool prefix and the `Team:` config root.** | The tool prefix is model-facing prompt text in 12 code files and every persona, and a test pins it — a model never sees it as a brand. The config root is a breaking change for any running install. Namespaces were the separate question, and on 2026-09-12 they moved to `Agency.Huddle.*` — the wire derived nothing from them, and both suites passed unchanged. Projects, assemblies, folders and the solution followed afterward and are now `Huddle.*` too; only those two identifiers and the domain vocabulary (Agent, Team Directory, `team.db`, the `team` pipe name) still say `Team`. See the [Decision record](engineering/decisions.md). |
