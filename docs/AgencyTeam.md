# Agency.Huddle

Agency.Huddle is a Slack-shaped chat app where the other participants in the conversation
are real Claude agents running as child processes. This page is the hub of its
documentation: read it whole if you are picking the codebase up cold, then follow
only the links your task needs.

Applies to the repo as of 2026-09-22, after roadmap item 15 (a Teammate chooses its
own Avatar): one solution, `Huddle.slnx`, holding all six projects, builds
with zero warnings and its 1421 tests pass, 10 of them skipped unless
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
> record](agencyteam/decisions.md) for the mapping.

## The map

Everything below this section is here because almost every task needs it. Each
page in `agencyteam/` is here because most tasks do not. Open one when its
question is yours; the cost column is roughly what it will spend.

| Page | Open it when you need | Cost |
| --- | --- | --- |
| [Language](agencyteam/language.md) | To name something, or write prose or interface copy | ~3.1k |
| [Code map](agencyteam/code-map.md) | To find which file does a thing | ~2.8k |
| [Runtime architecture](agencyteam/architecture.md) | To know how a Message actually travels | ~1.1k |
| **[Rules](agencyteam/rules.md)** | **Before editing anything in `src/Huddle.App`** | ~3.9k |
| **[Traps](agencyteam/traps.md)** | **Before editing `Huddle.Acp`, `Huddle.Contracts` or the wire** | ~2.6k |
| [Testing](agencyteam/testing.md) | To add a test, or to verify what no test can prove | ~2.3k |
| [Manual test script](agencyteam/manual-tests.md) | To test the running app in a browser, by hand | ~2.6k |
| [Test planning](agencyteam/manual-tests/planning.md) | To choose which manual tests to run, or to see what they cost | ~6.4k |
| [Test tracker](agencyteam/manual-tests/tracker.md) | To see where a manual test stands, or to record a result | ~19k |
| [Known limits](agencyteam/known-limits.md) | Before "fixing" something that looks missing | ~1.9k |
| [Roadmap](agencyteam/roadmap.md) | Before work in `PersonaRunner`, `ReplyGate`, `IAgentHostFactory`, Persona frontmatter, `app.css` or `Themes/` | ~10.7k |
| [Adapters handoff](Huddle.Adapters-Handoff.md) | **Start here for Adapters work.** State of play, what to do first, and the traps. Points at the other three | ~9k |
| [Adapters design](Huddle.Adapters-Specifications.md) | Before work on which ACP agent a Persona runs on — Adapter Profiles, the tool-name prefix, the Model/Effort catalogue probe, or `Huddle.MockAdapter` | ~45k |
| [Adapters live findings](Huddle.Adapters-LiveFindings.md) | What contact with the real `agency-acp` changed. The only Adapters doc about reality rather than intent | ~13k |
| [Skills design](Huddle.Skills-Specifications.md) | Before work on Skills, `read_skill`, `propose_teammates`, Proposals, the built-in Chief of Staff or its Greeting. Three streams, with a test-first task plan. Proposed, not built | ~20k |
| [Skills tracker](Huddle.Skills-Tracker.md) | To see or record where each of the plan's 90 tasks stands | ~4k |
| [Skills project plan](Huddle.Skills-ProjectPlan.md) | **Start here to build Skills.** 90 atomic, test-first tasks in 17 deliverables, each written for an agent with no context | ~13k |
| [Decision record](agencyteam/decisions.md) | To revisit a decision, or to read an older doc | ~6.3k |
| [Domain context](agencyteam/CONTEXT.md) | To see the vocabulary used in dialogue, not defined | ~0.6k |
| [ADRs](adr/) | To read one decision in full, with what was rejected | ~1.4k each |

The two bold rows are **binding, not informative**. They are the only two pages
whose contents can cost you a day, and neither is summarised here: a rule copied
into two files is a rule that goes stale in one of them. Follow the link.

### Vocabulary at a glance

The defined terms, so you can tell whether a word you are about to use is one of
them without opening [Language](agencyteam/language.md):

> Teammate · Human · Agent · Name · Alias · Title · Avatar · Team · Adapter · Room ·
> Member · Invitation · Archived · Persona · Rejected file · Model · Effort · Turn ·
> App Tool · Reply Gate · Budget · Catch-up · Progressive discovery · Skill · Work Dir ·
> Message · Draft · Mention · Envelope · Transcript · Stop · Team Directory ·
> Prompt · Placeholder · Default · Timing · Theme · Token · Appearance

Words that are *wrong here* and have a right replacement: bot, user, channel,
direct room, group room, agent session, sandbox, profile, prompt, database.
[Language](agencyteam/language.md) says what each becomes and why.

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
[Known limits](agencyteam/known-limits.md).

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

The vocabulary in [Language](agencyteam/language.md) governs **`Huddle.App`**
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

All under the `Team:` section — `TeamOptions.cs` and `Acp/AcpOptions.cs`.

| Key | Default | Note |
| --- | --- | --- |
| `DataDir` | `App_Data` | Everything created at runtime lives here. |
| `PipeName` | `team` | |
| `HumanName` | `You` | |
| `AgentMessageBudget` | `40` | Agent-authored Messages one Room takes between Human Messages. Any Human Message resets it; the Room view offers the Human one more Budget at a time. **Zero or less removes the runaway-loop guard entirely.** |
| `DemoAgent:Enabled` | `true` | The echo agents. |
| `DemoAgent:Names` | `["echo", "alpha"]` | |
| `Acp:Enabled` | `false` | **Spends money when true.** |
| `Acp:Command` | `node` | |
| `Acp:AdapterPath` | `null` | Otherwise located by probing upward. |
| `Acp:Args` | `null` | |
| `Acp:TeamsDir` | `Teams` | Relative to `DataDir`. Scanned recursively — sub-folders are organisational only; Team membership comes from each Persona's `teams` frontmatter field, not its location. Setting the old `Acp:PersonaDir` key throws at startup rather than silently scanning nothing. |
| `Acp:WorkDir` | `work` | One subdirectory per Persona. Relative to `DataDir`. |
| `Acp:SkillsDir` | `Skills` | Relative to `DataDir`. Holds Skill folders: an override of a shipped Skill, file by file, or a Skill written by hand. Created at startup. See [ADR-0021](adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md). |
| `Acp:TraceWire` | `false` | **Dumps the bearer token.** Debugging only. |
| `Acp:CatchUpMessages` | `20` | Per-Room catch-up buffer size. |
| `Acp:TokenBudget` | `1000000` | Per-Persona token Budget, summed from the rises in `UsageUpdated.Used` and reset by any Human Message. Catches a loop that mints fresh Rooms, which the per-Room Budget cannot. Zero or less disables it. |
| `Acp:TurnIdleTimeoutSeconds` | `180` | The longest an Adapter may say **nothing** during one Turn. Bounds silence, not duration: any event restarts the clock, so a long tool-using Turn that keeps reporting progress never trips it. Firing sends `session/cancel`, reports Degraded and counts toward the consecutive-failure streak — it is a failure, never a Stop. Zero or less disables it. Exists because an Adapter on an unreachable endpoint may return nothing at all, which reads as *hung* rather than Degraded. |
| `Acp:Adapters` | `null` | The Adapters this installation can launch, in configuration order; the **first is the default**. Absent means exactly one profile synthesised from `Acp:Command` / `Args` / `AdapterPath`, so a stock install is unchanged and the Adapter select does not render. Per entry: `Id`, `DisplayName`, `Description`, `Command`, `Args`, `AdapterPath`, `UsesToolNamePrefix`, `EnvironmentOverrides`. A blank `Command` or a duplicate `Id` throws at **startup**, not at first Turn. See [ADR-0013](adr/0013-an-adapter-is-a-property-of-the-persona.md). |
| `Acp:Adapters:*:EnvironmentOverrides` | `null` | Environment variables set on that Adapter's process, over and above the inherited environment — how an adapter that ships no `appsettings.json` of its own gets its configuration. **Set these from `appsettings.json`, never through the environment-variable provider:** that provider rewrites every `__` into `:`, so `Team__Acp__Adapters__0__EnvironmentOverrides__Agent__DefaultModel` binds as the key `Agent:DefaultModel`, which no process will ever read. |

Four runtime files and two runtime directories sit outside that section, because none of
them is a setting: the Persona library under `{DataDir}/{Acp:TeamsDir}`,
`{DataDir}/prompts.json`, `{DataDir}/appearance.json`, `{DataDir}/avatars.json`, the
uploaded avatar images under `{DataDir}/avatars/`, and Skill folders under
`{DataDir}/Skills/`.

| File | Holds |
| --- | --- |
| `{DataDir}/prompts.json` | **Overrides only**, one key per changed Prompt. Absent is normal and means nothing is overridden; the app does not create it, and it appears on the first save from `/settings`. Hand-editing it is supported and watched — a save in an editor reaches the next Turn without a restart. |
| `{DataDir}/appearance.json` | The selected Theme id. **That one key** — the per-Token override map went with the Tokens on 2026-09-14 ([ADR-0010](adr/0010-a-theme-is-a-mudblazor-theme.md)), and the `dark` light/dark preference went on 2026-09-21 when a Theme became a single palette ([ADR-0017](adr/0017-a-theme-is-a-palette-not-a-pair.md)). A file written before that still carries `dark`; it is an unknown key now, so it is ignored and kept, and there is no migration. Absent is normal and means the default Theme; the app does not create it. Hand-editable and watched, exactly like `prompts.json`. Not a setting under `Team:`: it is state this application writes. |
| `{DataDir}/avatars.json` | One entry per Teammate that has chosen an **Avatar**, keyed by Name — the Human included, since the Human has a Name but no Persona file. **Overrides only**, exactly like `prompts.json`: an absent file is normal, the app does not create it, and a Teammate with no entry renders the initials it always did. Hand-editable and watched. Deliberately not part of the Persona, so changing an avatar never restarts a session ([ADR-0019](adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md)). |
| `{DataDir}/avatars/` | The uploaded avatar images themselves, each named by a generated id rather than by a Teammate's Name. Served at `/teammate-avatars` by a `PhysicalFileProvider` — `MapStaticAssets` is manifest-driven and cannot see a file written at run time. Created at startup, unlike the JSON files, because a `PhysicalFileProvider` throws when its root is missing. |
| `{DataDir}/Skills/` | **Overrides and additions only.** A shipped Skill such as `team-building` lives in code as embedded resources; a folder here with the same name overrides it file by file (Source *Overridden*), and a folder with a new name is a Skill of the Human's own (*Yours*). An override whose `SKILL.md` is invalid falls back to the shipped one with a Warning; an invalid new Skill is left out. Watched: an edited file is what the next `read_skill` returns, while a changed name or description reaches a Teammate's system prompt only at its next session. Created at startup, because its watcher throws when the root is missing. Settings › Skills lists it and restores a default by deleting the override folder. |
| `prompts.default.json` (beside the binary) | Every Prompt's shipped wording, **generated** from `PromptCatalog` and copied to the output folder. The restore source, and readable as a reference. It is not the authority: delete both files and the app still runs on exactly the text it shipped with. |

A Prompt is one piece of text sent to a model. See [Language](agencyteam/language.md)
for the word, [ADR-0007](adr/0007-model-facing-text-is-configuration.md) for why
defaults live in code, and [Rules](agencyteam/rules.md) for the two things an edit
must never do.

A Theme is a MudBlazor `MudTheme` in `ThemeCatalog` carrying **one** palette and the
`ThemeMode` naming it, picked on the Appearance tab of `/settings` from a list grouped
Light / Dark / High contrast. **Picking a Theme picks light or dark with it** — there is
no separate preference, and so no way for the two to disagree
([ADR-0017](adr/0017-a-theme-is-a-palette-not-a-pair.md)). Nineteen ship: `huddle`
(labelled "Huddle Light") and `huddle-dark`, plus seventeen of the colour Themes bundled
with Visual Studio Code, converted once by hand into C# under `Themes/VsCode/` — one file
per Theme, **no importer**
([ADR-0016](adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md)).
See [Language](agencyteam/language.md) for
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
[Rules](agencyteam/rules.md) carries the three things a change here must not undo.

`Logging:LogLevel` is the one place that looks like it belongs to this section
and does not. Its keys are log-category prefixes, and a category comes from
`typeof(T).FullName` — so those read `"Agency.Huddle"`, the namespace root, while
everything in the table above stays under `Team:`. See
[Traps](agencyteam/traps.md) for what a stale `"Team"` key there does silently.

## Build, test, run

```powershell
dotnet build Huddle.slnx                             # must be 0 warnings
dotnet test  Huddle.slnx --                          # 8 E2E tests skipped when Acp:Enabled is off
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
test in `tests/Huddle.Tests` hosts it in-process through
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

Open `http://localhost:5100`. Two Rooms, `echo` and `alpha`, are already there,
because the app starts its own demo agents. Click `echo`, type `hi @echo`, press
Enter, and it answers in bold.

To talk to it as an external process instead, connect to the pipe:

```powershell
pwsh tools/echo-bot.ps1 -Name mybot
```

```text
Connecting to pipe '\\.\pipe\team' as agent 'mybot'...
Sent hello. Listening for messages (Ctrl+C to exit)...
{"type":"welcome","agentId":"01a08c...","name":"mybot","rooms":[...],"version":2}
```

A Room named `mybot` appears in the browser immediately, with no refresh.

## Repository and toolchain rules

These four apply to every change, which is why they are here rather than in
[Rules](agencyteam/rules.md):

| Rule | Why |
| --- | --- |
| **Package versions live only in `Directory.Packages.props`.** | Central package management is on. A `Version` attribute on a `PackageReference` is an error. |
| **Warnings are errors.** | Inherited from the root build props. Watch for CA1859 (return concrete types) and CA1305 (culture). |
| **Tests are xunit v3 under the Microsoft Testing Platform runner.** | Selected in `global.json`, required on .NET 10. |
| **Trust the compiler, not the editor.** | The IDE language server reports large numbers of phantom errors in this repo. If `dotnet build` is clean, the code is fine. |
| **The product is Agency.Huddle. Two identifiers still carry the old code name `Team` and must never be renamed to match: the `mcp__team__` tool prefix and the `Team:` config root.** | The tool prefix is model-facing prompt text in 12 code files and every persona, and a test pins it — a model never sees it as a brand. The config root is a breaking change for any running install. Namespaces were the separate question, and on 2026-09-12 they moved to `Agency.Huddle.*` — the wire derived nothing from them, and both suites passed unchanged. Projects, assemblies, folders and the solution followed afterward and are now `Huddle.*` too; only those two identifiers and the domain vocabulary (Teammate, Team Directory, `team.db`, the `team` pipe name) still say `Team`. See the [Decision record](agencyteam/decisions.md). |