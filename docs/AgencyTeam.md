# Agency.Huddle

Agency.Huddle is a Slack-shaped chat app where the other participants in the conversation
are real Claude agents running as child processes. This page is the hub of its
documentation: read it whole if you are picking the codebase up cold, then follow
only the links your task needs.

Applies to the repo as of 2026-09-12: 309 tests passing in `Team.sln` and 251
in `Team.slnx` (8 more skipped), zero build warnings. **Both solutions have
tests** — see [Build, test, run](#build-test-run).

The product is Agency.Huddle, and since 2026-09-12 so is every namespace. The
projects, assemblies, folders and both solution files are still `Team` — see the
last rule in [Repository and toolchain rules](#repository-and-toolchain-rules).

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
| [Language](agencyteam/language.md) | To name something, or write prose or interface copy | ~1.5k |
| [Code map](agencyteam/code-map.md) | To find which file does a thing | ~1.3k |
| [Runtime architecture](agencyteam/architecture.md) | To know how a Message actually travels | ~1.1k |
| **[Rules](agencyteam/rules.md)** | **Before editing anything in `src/Team.App`** | ~2k |
| **[Traps](agencyteam/traps.md)** | **Before editing `Team.Acp`, `Team.Contracts` or the wire** | ~1k |
| [Testing](agencyteam/testing.md) | To add a test, or to verify what no test can prove | ~1.2k |
| [Known limits](agencyteam/known-limits.md) | Before "fixing" something that looks missing | ~1.4k |
| [Roadmap](agencyteam/roadmap.md) | Before work in `PersonaRunner`, `ReplyGate`, `IAgentHostFactory`, Persona frontmatter or `app.css` | ~8.4k |
| [Decision record](agencyteam/decisions.md) | To revisit a decision, or to read an older doc | ~2.7k |
| [Domain context](agencyteam/CONTEXT.md) | To see the vocabulary used in dialogue, not defined | ~0.6k |
| [ADRs](adr/) | To read one decision in full, with what was rejected | ~0.5k each |

The two bold rows are **binding, not informative**. They are the only two pages
whose contents can cost you a day, and neither is summarised here: a rule copied
into two files is a rule that goes stale in one of them. Follow the link.

### Vocabulary at a glance

The defined terms, so you can tell whether a word you are about to use is one of
them without opening [Language](agencyteam/language.md):

> Teammate · Human · Agent · Name · Adapter · Room · Member · Invitation ·
> Persona · Model · Effort · Turn · App Tool · Reply Gate · Catch-up ·
> Progressive discovery · Work Dir · Message · Mention · Envelope · Transcript ·
> Team Directory

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
three or more is a group (answer only when `@`-mentioned). See `ReplyGate` — six
lines, and the whole of that rule.

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
  spanning every Room that Agent is in.

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

Four projects in `Team.sln` — `Team.Contracts`, `Team.Acp`, `Team.App` and
`tests/Team.Tests`. `Team.Console` is in `Team.slnx` only, and appears below
because the dependency arrows do not read correctly without it. Dependencies
point downward only:

```text
Team.App  ────────────┐          Blazor Server: the chat UI, the pipe server,
   │                  │          the Persona library, the Persona supervisor
   │                  ↓
   │            Team.Acp         reusable ACP client: spawns an agent process,
   │                  ↑          talks JSON-RPC over its stdio, hosts MCP tools
   ↓                  │
Team.Contracts   Team.Console    a terminal REPL against one agent — the
(wire protocol)                  original ACP proof of concept
```

- **`Team.Contracts`** — the named-pipe wire protocol. Records and JSON only, no
  behaviour. Both the app and any external client depend on it.
- **`Team.Acp`** — knows nothing about chat. Given a command line it starts an
  agent process, opens a session, streams events, and can expose your own C#
  methods to the model as MCP tools.
- **`Team.App`** — the application. Depends on both.
- **`Team.Console`** — a separate, standalone REPL. **Not part of the chat app.**

**Project names and namespaces differ, deliberately.** Every project, assembly,
folder and solution file is still `Team.*`; every namespace inside them is
`Agency.Huddle.*`. The project `Team.Acp` builds `Team.Acp.dll` and declares
`Agency.Huddle.Acp`. The two are independent in .NET; `<RootNamespace>` in each
of the six `.csproj` files is what carries the split, and `AcpReferenceTests`
pins the assembly name so the project side cannot drift by accident.

> [!NOTE]
> `src/Team.Console`, `tests/Team.Acp.Tests`, `tools/acp` and `Team.slnx` were
> historically owned by a parallel effort. Expect them to change under you; do
> not edit them without a reason.

### Two bounded contexts

The vocabulary in [Language](agencyteam/language.md) governs **`Team.App`**
and **`Team.Contracts`** only.

**`Team.Acp` uses ACP's own words and is deliberately untouched by the
*vocabulary* rename.** The 2026-09-12 *namespace* rename did reach it — it
declares `Agency.Huddle.Acp` now — but that moved the namespace root only, and
not one type name. ACP is the *Agent Client Protocol*, so `IAgentHost`,
`IAgentSession`, `AgentSessionOptions` and `DotAcpAgentSession` are already
correct domain language inside a library whose whole value is not knowing about
chat. Pushing Team's vocabulary into it would break the layering the dependency
arrows exist to protect.

The boundary holds empirically: there are zero occurrences of chat vocabulary in
`Team.Acp`, `Team.Console` or `tests/Team.Acp.Tests`.

## Configuration

All under the `Team:` section — `TeamOptions.cs` and `Acp/AcpOptions.cs`.

| Key | Default | Note |
| --- | --- | --- |
| `DataDir` | `App_Data` | Everything created at runtime lives here. |
| `PipeName` | `team` | |
| `HumanName` | `You` | |
| `DemoAgent:Enabled` | `true` | The echo agents. |
| `DemoAgent:Names` | `["echo", "alpha"]` | |
| `Acp:Enabled` | `false` | **Spends money when true.** |
| `Acp:Command` | `node` | |
| `Acp:AdapterPath` | `null` | Otherwise located by probing upward. |
| `Acp:Args` | `null` | |
| `Acp:PersonaDir` | `personas` | Relative to `DataDir`. |
| `Acp:WorkDir` | `work` | One subdirectory per Persona. Relative to `DataDir`. |
| `Acp:TraceWire` | `false` | **Dumps the bearer token.** Debugging only. |
| `Acp:CatchUpMessages` | `20` | Per-Room catch-up buffer size. |

`Logging:LogLevel` is the one place that looks like it belongs to this section
and does not. Its keys are log-category prefixes, and a category comes from
`typeof(T).FullName` — so those read `"Agency.Huddle"`, the namespace root, while
everything in the table above stays under `Team:`. See
[Traps](agencyteam/traps.md) for what a stale `"Team"` key there does silently.

## Build, test, run

```powershell
dotnet build Team.sln          # must be 0 warnings
dotnet build Team.slnx         # must be 0 warnings
dotnet test  Team.sln --       # 309 passing, no tokens spent
dotnet test  Team.slnx --      # 251 passing, 8 E2E skipped
dotnet run --project src/Team.App --urls http://localhost:5100
```

> [!IMPORTANT]
> Two ways to skip tests without noticing, both of which look like success.
>
> **`Team.sln` does not contain `tests/Team.Acp.Tests`** — that project is in
> `Team.slnx`, along with `Team.Acp` and `Team.Console`. Testing only `Team.sln`
> leaves the whole ACP client unverified. Run both.
>
> **The trailing `--` is required.** Without it, `dotnet test` exits code 5 with
> "Zero tests ran" — a Microsoft Testing Platform CLI quirk under this SDK. It
> reads as a no-op, not as a failure.
>
> Only the `E2E/` folder of `Team.Acp.Tests` spends money, and it is gated on
> `TEAM_E2E=1`. Leave that unset and the whole suite is free.

Development configuration sets `Acp:Enabled: true`, so `dotnet run` starts one
`node` process per Persona at startup, before you type anything. To open the app
without that:

```powershell
$env:Team__Acp__Enabled = 'false'
dotnet run --project src/Team.App --urls http://localhost:5100
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
| **The product is Agency.Huddle; the code is `Team`. Never rename `mcp__team__` or the `Team:` config root to match.** | The tool prefix is model-facing prompt text in 12 code files and every persona, and a test pins it — a model never sees it as a brand. The config root is a breaking change for any running install. Namespaces were the separate question, and on 2026-09-12 they moved to `Agency.Huddle.*` — the wire derived nothing from them, and both suites passed unchanged. Projects, assemblies and solution files did not move. See the [Decision record](agencyteam/decisions.md). |
