# Huddle.Adapters — Design Specification (HLD)

**Status:** delivered 2026-09-16 (PR #59) · **Date:** 2026-09-16 · **Supersedes:** the plan recorded in
[roadmap item 12](agencyteam/roadmap.md#12-running-a-persona-on-a-local-model-via-agencynet--delivered-2026-09-16)

Delivers roadmap item 12 — *running a Persona on a local Model* — by a route item 12 did not
foresee. Agency.NET is shipping an **ACP agent** (`Agency.Acp`, specified in
`E:\Repos\Agency\docs\specs\Agency.Acp-Specifications.md`), so this solution does not bridge a
library in-process. It launches a second adapter process and reuses every line of
`src/Huddle.Acp`.

This document is binding on `src/Huddle.App` and `src/Huddle.Acp`. It touches both build
subtrees and shared root docs, so it is announced rather than merged silently.

---

## Table of Contents

1. [Goal](#1-goal)
2. [Example use cases](#2-example-use-cases)
3. [Non-goals](#3-non-goals)
4. [Design principles](#4-design-principles)
5. [Architecture overview](#5-architecture-overview)
6. [System components](#6-system-components)
7. [Data model and storage](#7-data-model-and-storage)
8. [Core algorithms](#8-core-algorithms)
9. [Incremental vs full processing](#9-incremental-vs-full-processing)
10. [Background workers and async components](#10-background-workers-and-async-components)
11. [Performance expectations](#11-performance-expectations)
12. [Edge cases and failure modes](#12-edge-cases-and-failure-modes)
13. [End-to-end flow](#13-end-to-end-flow)
14. [Design notes and rationale](#14-design-notes-and-rationale)
15. [Test-first task plan](#15-test-first-task-plan)
16. [Vocabulary changes](#16-vocabulary-changes)
17. [Decision log](#17-decision-log)

---

## 1. Goal

### 1.1 Primary goal

Make **Adapter** a per-Persona choice. A Teammate may run on Anthropic's `claude-agent-acp`
(cloud, billed per Turn) or on Agency.NET's `agency-acp` (a local model on this machine, free),
selected from a dropdown when the Persona is created or edited, and everything downstream —
the pipe, the Reply Gate, the Budget, the Rooms, every App Tool — must not learn which.

### 1.2 The shape this takes, and why it is small

Item 12 specified *"a second `IAgentHostFactory`"* backed by Agency.NET's harness in-process: a
hand-written pump from `IAsyncEnumerable<AgentEvent>` into a `ChannelReader<AgentEvent>`, a
synthesised `TurnCompleted`, a `using` alias for two colliding `AgentEvent` types, and a second
`IModelCatalog`.

**None of that is now required.** Because Agency.NET speaks ACP over stdio, the second adapter
differs from the first in exactly one respect: **which process gets launched**. Reading
`DotAcpAgentHostFactory.CreateAsync` step by step:

| Step | Adapter-specific? |
| --- | --- |
| Compose the Work Dir | no |
| `AgentProcessOptionsFactory.TryCreate(...)` | **yes — this is the whole difference** |
| Mint the bearer token | no |
| Build the six chat tools + `GetHelpTool` | no |
| Start `AppToolServer` | no |
| `new DotAcpAgentHost(...)` + `StartAsync` | no |
| `StartSessionAsync(new AgentSessionOptions(...))` | no |
| Wrap in `ToolServerOwningAgentHost` | no |

One step in eight. So the design is **one factory that resolves a profile**, not two factories.

### 1.3 Concrete objectives

| # | Objective | Measure |
| --- | --- | --- |
| O-1 | A Persona names its Adapter, and the Teammate card offers the choice | `adapter:` round-trips through Create and Edit; the select renders the configured profiles |
| O-2 | Changing the Adapter restarts the session | `PersonaSupervisor.NeedsRestart` returns true on an Adapter-only change |
| O-3 | Model and Effort catalogs come from the **selected** Adapter | a Persona on `agency` is never offered Claude's models |
| O-4 | `PersonaRunner` is untouched | zero diff in `PersonaRunner.cs` outside the `ThoughtChunk` line |
| O-5 | The tool-name prefix follows the Adapter | the system prompt names `get_help` for `agency`, `mcp__team__get_help` for `claude` |
| O-6 | No new per-Persona store | `PersonaRenameCascade` gains no row |
| O-7 | The session graph is released on the far side | `session/close` is sent on dispose |
| O-8 | **No Huddle work waits on Agency.NET** | every task but Phase 7 runs green against `Huddle.MockAdapter`, in CI, with no Node, no subscription and no GPU |

---

## 2. Example use cases

**U-1 — A cheap summariser beside an expensive coder.**
`Ana` (router) is created with **Adapter: Agency**, Model `google/gemma-4-e2b`. `Kai` (writes
code) stays on **Adapter: Claude**. Both sit in `#product`. A Message Mentioning both wakes two
Turns; one bills the Claude subscription, one costs nothing. Neither Room, Budget nor Reply Gate
distinguishes them.

**U-2 — A human switches a Teammate to local to stop spending.**
Edit `Ana`, change Adapter from Claude to Agency. Model and Effort reset to their defaults with
an inline note. Saving restarts the session; `Ana` loses its conversation memory, exactly as a
Model change already does.

**U-3 — The local endpoint is down.**
`agency-acp` starts, `session/new` cannot reach the inference server, and the catalogue comes
back empty. The session still starts (`models: []` means *unknown*). The first Turn fails; the
Teammate goes `Degraded` with the adapter's message. No other Persona is affected — a different
process.

**U-4 — Only one Adapter is configured.**
`Team:Acp:Adapters` is absent. One implicit `claude` profile is synthesised from the existing
`Command` / `AdapterPath` / `Args` keys, the Adapter select does not render at all, and every
existing installation behaves exactly as it does today.

**U-5 — A stale Adapter id.**
A Persona file says `adapter: agency` but no such profile is configured. The Persona is **not**
rejected — it starts on the default profile and reports `Degraded` with a message naming the
missing id. Same degradation contract as a stale Model.

**U-7 — A developer works on the card with nothing installed.**
No Node, no Claude subscription, no GPU, no `agency-acp`. `Team:Acp:Adapters` names a `mock`
profile pointing at `mock-acp`, and `Acp:Enabled` is `true`. A Persona starts, streams a
chunked reply into a Room, and Stop cancels it. Everything the human sees is real except the
model.

**U-6 — The model catalogue for a local Adapter.**
Opening the Edit card on an `agency` Persona probes `agency-acp`, not `claude-agent-acp`. The
probe costs one process spawn and one `GET /v1/models` — cheaper than the Claude probe, and
cached separately.

---

## 3. Non-goals

| Not doing | Why |
| --- | --- |
| A second `IAgentHostFactory` | §1.2 — one step in eight differs. A second factory would duplicate seven. |
| A second `IModelCatalog` implementation | Both Adapters advertise models at `session/new`. `ModelCatalogProbe` already does exactly that; it needs a profile, not a rewrite. |
| A per-Persona **store** for the Adapter | The roadmap's Ordering warning is binding: *"every per-Persona store added is one more place a rename has to touch, and that cost never goes down."* Frontmatter needs none. |
| Changing Model or Effort **without** restarting | `Agency.Acp` supports it (`SetAgent` preserves history) and `claude-agent-acp` does not. Diverging per Adapter would make one rule two. v2. |
| A Huddle-side `ToolKind` map | **`ToolActivity` carries no kind.** See §6.7 — this was an ask made on a false premise and is withdrawn. |
| Rendering `ThoughtChunk` | Out of scope here; a prerequisite for Loop Kit, which is deferred on the Agency side. |
| A stable per-Persona GUID | Needed only for Agency memory (v2). Requires an ACP carrier that does not exist. |
| Multi-session against one adapter process | `Agency.Acp` supports it; Huddle keeps one process per Persona, which keeps cwd, identity and permission grants correct by construction. |
| Changing what `Acp:Enabled` means | It still means *"start adapter processes"*. A local Adapter spends no money but does spend the machine. |

---

## 4. Design principles

**P1 — The Adapter is a property of the Persona, not of the installation.**
One Huddle can run Teammates on different Adapters at once. Anything that reads a global
"which backend" setting is wrong by construction.

**P2 — Add a field, not a store.**
The Adapter lives in frontmatter, read through the parser that already reads arbitrary keys.
`PersonaRenameCascade` must gain no row.

**P3 — `PersonaRunner` never learns which Adapter answered.**
It holds an `IAgentHost` and an `IAgentSession` and consumes a `ChannelReader<AgentEvent>`.
Every Adapter-specific fact is resolved before the session exists.

**P4 — Degrade, never reject.**
A stale Adapter id, an empty catalogue, an unreachable endpoint: all produce a running Persona
in a reported state, never a Rejected file and never a failed start. Matches the existing
contract for a stale Model.

**P5 — The tool prefix is code, per Adapter.**
`rules.md` forbids a hook template containing `mcp__team__`; the prefix is built in code from
the tool-server name. That rule survives and widens: the prefix is now built in code **from the
Adapter profile**. A human still cannot misspell a tool into nonexistence.

**P6 — One implicit profile means today's behaviour, byte for byte.**
An installation that configures no Adapters must produce the same process, the same prompt and
the same goldens as before this change.

**P7 — Probe cost follows the Adapter.**
The catalogue probe is free of tokens on both Adapters but not free of time. Cache per Adapter;
never probe on a plain page load.

**P8 — A third Adapter this solution owns is test infrastructure, not a stand-in.**
`Huddle.MockAdapter` exists so every Huddle-side path has an automated test that needs no
second repository. It answers the protocol and records what it received; it never simulates a
model's judgement, and it never grows Huddle behaviour of its own.

---

## 5. Architecture overview

### 5.1 Target architecture

```text
┌───────────────────────────────────────────────────────────────────────────────┐
│  Blazor circuit                                                                │
│                                                                                │
│   TeammateCard                                                                 │
│     Adapter ▾ ──┐   Model ▾ ──┐   Effort ▾                                      │
│                 │             │                                                │
│                 │ (cascade: Adapter resets Model+Effort; Model resets Effort)   │
└─────────────────┼─────────────┼────────────────────────────────────────────────┘
                  │             │
     writes       │             │  reads catalogs
  `adapter:` via  │             ▼
  WriteScalarField│      IModelCatalog                (internal, singleton)
                  │             │
                  ▼             ▼
        ┌──────────────┐  ┌──────────────────────────┐
        │ PersonaStore │  │ ModelCatalogProbe        │
        │ file + SQLite│  │  cache ▸ per (adapter,    │
        └──────┬───────┘  │           model)         │
               │          └───────────┬──────────────┘
               │ Persona(Name, Text,  │
               │   Model, Effort,     │
               │   Adapter)           │
               ▼                      │
     ┌───────────────────┐            │
     │ PersonaSupervisor │            │
     │  NeedsRestart =   │            │
     │  record equality  │            │
     └─────────┬─────────┘            │
               │ new PersonaRunner    │
               ▼                      │
     ┌───────────────────┐            │
     │ PersonaRunner     │  ── UNCHANGED ──                                      
     │  work queue       │            │
     │  event reader     │            │
     └─────────┬─────────┘            │
               │ IAgentHostFactory.CreateAsync(persona, agentId)
               ▼                      │
     ┌─────────────────────────────────┴───────────────────────────┐
     │ DotAcpAgentHostFactory                    (one, profile-aware)│
     │   1. AdapterProfileResolver.Resolve(persona.Adapter) ────────┐│
     │   2. AgentProcessOptionsFactory.TryCreate(profile, workDir)  ││
     │   3. mint bearer token                                       ││
     │   4. build 6 chat tools + GetHelpTool(profile.ToolPrefix)    ││
     │   5. AppToolServer(serverName, tools, port 0, token)         ││
     │   6. DotAcpAgentHost(processOptions, launcher, hostOptions)  ││
     │   7. StartSessionAsync(AgentSessionOptions{cwd, perms,       ││
     │        systemPrompt, toolServer, model, effort})             ││
     └──────────────────────────────┬───────────────────────────────┘│
                                    │                                │
                                    ▼                                ▼
                        ┌───────────────────────┐        ┌──────────────────────┐
                        │ AgentProcessLauncher  │        │ AdapterCatalog       │
                        └───────────┬───────────┘        │  from Team:Acp:      │
                                    │                    │    Adapters[]        │
              ┌─────────────────────┴──────────────────┐ └──────────────────────┘
              ▼                                        ▼
   ┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────────┐
   │ claude-agent-acp     │  │ agency-acp           │  │ mock-acp             │
   │ node · cloud Claude  │  │ .NET · local model   │  │ .NET · this solution │
   │ prefix mcp__team__   │  │ no prefix            │  │ no prefix · free     │
   │ ships elsewhere      │  │ ships elsewhere      │  │ §6.10 · Tier 3       │
   └──────────┬───────────┘  └──────────┬───────────┘  └──────────┬───────────┘
              │                         │                         │
                       stdio ACP                 stdio ACP
              └─────────────────────────┼─────────────────────────┘
                                        ▼
                         ┌────────────────────────────┐
                         │ AppToolServer              │
                         │  127.0.0.1:0  POST /mcp    │
                         │  Bearer <32 random bytes>  │
                         │  7 App Tools               │
                         └────────────────────────────┘
```

### 5.2 What is new, what changes, what is untouched

| Layer | Status |
| --- | --- |
| `AdapterProfile`, `AdapterCatalog`, `AdapterProfileResolver` | **new** — `src/Huddle.App/Acp/` |
| `AcpOptions.Adapters` | **new config** — `Team:Acp:Adapters[]` |
| `DotAcpAgentHostFactory`, `AgentProcessOptionsFactory`, `ModelCatalogProbe` | **modified** — profile-aware |
| `Persona`, `PersonaStore`, `PersonaFrontmatter`, `PersonaIndex` | **modified** — one new field |
| `TeammateCard.razor` | **modified** — a third cascading select |
| `DotAcpAgentSession`, `AppToolServer` | **modified** — `session/close`, `WWW-Authenticate` |
| `PersonaRunner`, `ReplyGate`, `ChatService`, every App Tool, `AgentGateway`, the pipe | **untouched** |
| `Huddle.Contracts` | **untouched — no protocol bump** |
| `Huddle.MockAdapter` | **new** — `src/Huddle.MockAdapter/`; links `FakeAcpAgent` from the ACP effort's tests (§6.10) |
| `Huddle.slnx` | **modified** — a shared root file; **announce** |

---

## 6. System components

### 6.1 `AdapterCatalog` and `AdapterProfile`

**Purpose.** Hold the set of Adapters this installation can launch, and answer which one a
Persona means.

**Inputs / outputs.** In: `IOptions<TeamOptions>`. Out: an ordered `IReadOnlyList<AdapterProfile>`
and a resolver.

```csharp
namespace Agency.Huddle.App.Acp;

/// <summary>One ACP agent this installation can launch, and how to launch it.</summary>
public sealed record AdapterProfile(
    string Id,                       // "claude", "agency" - stable, stored in frontmatter
    string DisplayName,              // "Claude", "Agency"  - rendered on the card
    string? Description,             // "cloud, spends money per turn"
    string Command,                  // "node" | absolute path to agency-acp.exe
    IReadOnlyList<string>? Args,
    string? AdapterPath,
    bool UsesToolNamePrefix);        // true -> mcp__{server}__ ; false -> bare
```

**Internal flow.** `AdapterCatalog` is built once at construction from `AcpOptions.Adapters`.
When that list is null or empty it **synthesises exactly one profile** from the legacy keys:

```csharp
new AdapterProfile(
    Id: "claude", DisplayName: "Claude", Description: null,
    Command: acp.Command, Args: acp.Args, AdapterPath: acp.AdapterPath,
    UsesToolNamePrefix: true)
```

This is P6 in one constructor: an installation that configures nothing gets today's behaviour
with today's prompt and today's goldens.

**Implementation notes.**
- `Id` is compared `OrdinalIgnoreCase`, matching `PersonaIndex`'s Name comparison and
  `persona_models`' `COLLATE NOCASE`.
- The **first** profile in the list is the default. Order is configuration order, which is why
  `Args` must stay a `IReadOnlyList<string>?` with no initialiser — `ConfigurationBinder`
  appends to a populated collection (`rules.md`).
- `AdapterCatalog` is a singleton; the list is frozen at startup. Adding an Adapter requires a
  restart, which matches every other `TeamOptions` key.

**Constraints.** A profile whose `Command` is blank is a configuration error and must throw at
startup, not at first Turn — the same fail-fast shape as the `Team:Acp:PersonaDir` rename guard
in `ServiceCollectionExtensions`.

**V1 / V2.** V1: static, from configuration. V2: an installed-adapters discovery pass, so the
card can say *"Agency is configured but its executable is missing"* before a Turn proves it.

---

### 6.2 `AdapterProfileResolver`

**Purpose.** Turn a Persona's `Adapter` string into a profile, never failing.

```csharp
internal sealed class AdapterProfileResolver(AdapterCatalog catalog)
{
    /// <summary>The profile for this id, or the default when the id is absent or unknown.</summary>
    internal (AdapterProfile Profile, string? Warning) Resolve(string? adapterId);
}
```

**Internal flow.**

| `adapterId` | Result | Warning |
| --- | --- | --- |
| `null` / whitespace | default profile | none |
| matches a profile (`OrdinalIgnoreCase`) | that profile | none |
| no match | **default profile** | `"The Adapter 'x' is not configured; running on 'claude'."` |

**Implementation notes.** Returning a tuple rather than throwing is P4, and it mirrors
`PersonaRunner`'s existing treatment of an unadvertised Model: the session starts, the Teammate
reports `Degraded`, and the human sees why. The warning is surfaced through the same
`RaiseStatusChanged(PersonaState.Degraded, …)` path.

**Constraints.** The resolver is pure and synchronous. It must not touch the filesystem — an
adapter that is configured but not installed is discovered by `AgentProcessOptionsFactory`
returning `null`, which already has a message naming `tools/acp/install.ps1`.

---

### 6.3 `AgentProcessOptionsFactory` (modified)

**Purpose.** Unchanged — turn configuration into an `AgentProcessOptions`.

**Signature change:**

```csharp
// before
internal static AgentProcessOptions? TryCreate(AcpOptions options, string workDir, string probeStart)
// after
internal static AgentProcessOptions? TryCreate(AdapterProfile profile, string workDir, string probeStart)
```

**Internal flow** — the same four-step precedence, now reading the profile:

1. `profile.Args is { Count: > 0 }` → `new AgentProcessOptions(profile.Command, profile.Args, workDir)`
2. `profile.AdapterPath` non-blank → `new(profile.Command, [profile.AdapterPath], workDir)`
3. `AdapterLocator.Locate(probeStart)` → `new(profile.Command, [located], workDir)`
4. else `null`

**Implementation notes.** Step 3 — the Node-adapter walk-up — stays, but is now **only correct
for a profile that wants it**. An `agency` profile configured as a bare `Command` with no `Args`
and no `AdapterPath` would fall through to `AdapterLocator` and find `claude-agent-acp`. Two
defences, both cheap:

- `AdapterLocator` is consulted only when `profile.UsesToolNamePrefix` is true. The coupling
  looks odd and is deliberate: a profile that does not take the `mcp__` prefix is not the Node
  adapter, and the locator knows only how to find the Node adapter.
- A profile whose `Command` is an absolute path with no `Args` is used as-is at step 2.

**Constraints.** Must keep returning `null` rather than throwing (a developer who has not run
`install.ps1` must still be able to `dotnet run`).

**V1 / V2.** ~~V2: `EnvironmentOverrides`, the fourth `AgentProcessOptions` slot, is set by nobody
today and is the natural place for per-Adapter environment (an endpoint URL, an API key
placeholder). Out of scope here because `Agency.Acp` takes its configuration from its own
`appsettings.json` and command-line arguments.~~

> **Moved to V1, 2026-09-17.** The premise above was wrong: `Agency.Acp` ships **no**
> `appsettings.json` at all — its `.csproj` declares no content-copy items — so `session/new`
> hard-fails on a vanilla build with *"Agent:DefaultModel is not configured"*
> ([Live findings](Huddle.Adapters-LiveFindings.md), D-2). It does reference
> `Microsoft.Extensions.Configuration.EnvironmentVariables`, so environment variables are a
> first-class configuration source there rather than a workaround. `AdapterProfile` therefore
> carries `EnvironmentOverrides`, projected by `AdapterCatalog` and passed at all three
> `AgentProcessOptions` construction sites, so the Model-catalogue probe launches with the same
> environment a Turn would — which matters, because the probe does exactly `initialize` +
> `session/new` and is the first thing the hard throw stops.

---

### 6.4 `DotAcpAgentHostFactory` (modified)

**Purpose.** Unchanged — build the `(IAgentHost, IAgentSession)` pair for one Agent.

**Internal flow**, with the two changed lines marked:

```csharp
var (profile, warning) = this.resolver.Resolve(persona.Adapter);          // ← new
var workDir = Path.Combine(options.DataDir, options.Acp.WorkDir, persona.Name);
var processOptions = AgentProcessOptionsFactory.TryCreate(profile, workDir, AppContext.BaseDirectory)
    ?? throw new InvalidOperationException(/* names install.ps1 and the profile id */);
var authToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
IReadOnlyList<IAppTool> chatTools = [ /* six, unchanged */ ];
var toolNamePrefix = profile.UsesToolNamePrefix ? $"mcp__{ToolServerName}__" : string.Empty;  // ← new
var getHelpTool = new GetHelpTool(chatTools, hooks, toolNamePrefix);
IReadOnlyList<IAppTool> tools = [getHelpTool, .. chatTools];
var toolServer = new AppToolServer(ToolServerName, tools, this.loggerFactory, 0, authToken);
// … unchanged through StartSessionAsync and ToolServerOwningAgentHost
```

**Implementation notes.**
- `ToolServerName = "team"` stays a single `private const`. **It is the MCP server name and it
  does not change** — only whether the *model-facing* names are prefixed with it. This matters:
  `CLAUDE.md` pins the `mcp__team__` tool prefix as a name that must not be renamed to match the
  brand, and it is not being renamed. It is being made conditional.
- The prefix still flows to exactly two places — `GetHelpTool`'s constructor and
  `SystemPromptComposer.Compose` — and is still never typed into a hook template.
- `warning` is returned alongside the pair so `PersonaRunner` can raise it. Rather than widen
  `IAgentHostFactory`'s return tuple (which `FakeAgentHostFactory` and every supervisor test
  depend on), the factory **logs** it and the resolver's warning is surfaced by
  `PersonaSupervisor` at start time instead. See §8.2.

**Constraints.** `GetHelpTool` is still constructed from the other tools and **last**
(`rules.md`) — it reports the catalog, so it takes the list rather than joining it.

---

### 6.5 `ModelCatalogProbe` (modified)

**Purpose.** Unchanged — read the Model catalog and the Effort ladder without spending a Turn.

**Why it is reused rather than replaced.** Item 12 said *"resist reusing the class: implement
`IModelCatalog` again and keep both simple"*, reasoning that a local endpoint exposes
`GET /v1/models` and needs no process. Under ACP that reasoning does not apply: **both Adapters
advertise their catalog at `session/new`**, because that is what ACP does. The probe's mechanism
is correct for both; only the process differs.

> **Correction, 2026-09-17, against the shipped `AgencyDotNet.Acp` 0.1.195.** This section
> originally said `Agency.Acp` maps its `Model[]` onto an `AgentModelOption` and returns it from
> `session/new` "exactly as the Node adapter does". **There is no `AgentModelOption` in
> `dotacp.protocol`**, and neither adapter returns one. Both return a `configOptions` array of
> `SessionConfigSelect`s, categorised `Model` and `ThoughtLevel`. The conclusion survives intact —
> the catalog does arrive at `session/new` and the probe is right to be reused — and Huddle needed
> no change, because `src/Huddle.Acp/DotAcp/ModelConfigOptions.cs` already reads `configOptions`
> and `SessionConfigSelects.cs` already handles both union branches (a flat option array and
> grouped options). `AgentModelOption` is **Huddle's own** type, mapped from the wire at that
> boundary; the error was attributing it to the protocol.

**Signature change:**

```csharp
internal interface IModelCatalog
{
    ValueTask<IReadOnlyList<AgentModelOption>> GetAsync(string? adapterId, CancellationToken ct);
    ValueTask<IReadOnlyList<AgentEffortOption>> GetEffortLevelsAsync(string? adapterId, string? model, CancellationToken ct);
}
```

**Internal flow.** As today — resolve the profile, `TryCreate`, spawn, `initialize` →
`session/new`, read `session.Models` / `session.EffortLevels`, dispose — with the caches
re-keyed:

| Cache | Before | After |
| --- | --- | --- |
| Models | one `IReadOnlyList<AgentModelOption>? cached` | `ConcurrentDictionary<string, IReadOnlyList<AgentModelOption>>` keyed on adapter id |
| Efforts | `ConcurrentDictionary<string, …>` keyed on model id, `""` = default | keyed on `$"{adapterId}\u001f{model}"` |

**Implementation notes.**
- The `SemaphoreSlim gate` stays **single and shared**. Two Blazor circuits must not spawn two
  processes, and that is true across Adapters as much as within one. A shared gate makes a slow
  Claude probe delay an Agency probe; that is the correct trade against spawning two Node
  processes, and the 20-second timeout bounds it.
- `WithoutAdapterDefault` — which drops the `"default"` sentinel — stays. It is expressed as a
  filter over the wire's answer and is harmless against an Adapter that never emits one.
- The probe cwd stays the Work Dir **root**, never the repo root: the Node adapter auto-loads
  `CLAUDE.md` and `.claude/settings.json` from its cwd, which is a second implicit way a persona
  arrives (`rules.md`).

**Constraints.** `rules.md` row 35 is binding: **no test may reach the real
`ModelCatalogProbe`.** `FakeModelCatalog` gains the `adapterId` parameter and records it, which
is what a UI test asserts against.

**V1 / V2.** V2: once `Agency.Acp` reports residency (`AgentModelOption.Description` = "loaded
now"), the card renders it as secondary text. That is gated on the LM Studio JIT TTL being
enabled and is explicitly **not in force** yet.

---

### 6.6 `TeammateCard` — three cascading selects

**Purpose.** Let a human choose Adapter, then Model, then Effort, each constrained by the one
above.

**Internal flow.**

```text
OnInitializedAsync / BeginEditAsync
  └─ if mode is Create or Edit:
       LoadAdapters()                 ← synchronous; configuration, not a probe
       await LoadModelsAsync()        ← probes the SELECTED adapter
       Task efforts = LoadEffortsAsync();
       StateHasChanged();             ← rules.md: repaint between two awaited probes
       await efforts;

OnAdapterChangedAsync(value)
  ├─ this.adapter = blank ? null : value
  ├─ this.adapterResetModelAndEffort = (model is not null || effort is not null)
  ├─ this.model = null; this.effort = null
  ├─ await LoadModelsAsync()
  ├─ Task efforts = LoadEffortsAsync(); StateHasChanged(); await efforts;

OnModelChangedAsync(value)            ← unchanged
  └─ effortResetByModelChange; effort = null; await LoadEffortsAsync()
```

**Implementation notes.**
- The Adapter select **does not render** when the catalog holds one profile. A dropdown with a
  single option is a control that cannot be used; P6 says a stock installation must look
  unchanged.
- `adapterResetModelAndEffort` renders a `role="status"` note, not `role="alert"` — it is a
  consequence of what the human just did, not an interruption. Exact precedent:
  `effortResetByModelChange` and the rename note.
- **Both awaited probes need the intermediate `StateHasChanged()`.** `ComponentBase` renders a
  handler at its first yield and at its completion and nowhere between, so without it the Model
  select repaints when the *effort* probe answers. This is issue #39 and it now has three
  dropdowns to get wrong instead of two.
- `AdapterChoices` synthesises an entry for a stored-but-unconfigured id, exactly as
  `ModelChoices` and `EffortChoices` do, so the card shows what the file says rather than
  silently resetting it.
- A select whose catalog is still probing is `Disabled`, never `ReadOnly` — MudBlazor 9.10.0
  styles only the disabled state, so a read-only select looks live and swallows the click.

**Helper text.**

| State | Text |
| --- | --- |
| one profile configured | *(select not rendered)* |
| default | "Which agent runs this teammate. Changing it restarts the teammate, which clears what it remembers." |
| stored id not configured | "The adapter 'x' is not configured. This teammate will run on 'claude'." |

**Constraints.** `[Parameter]` may not be of an `internal` type — Razor generates component
classes as `public`. `AdapterProfile` is therefore **public**, like `AgentModelOption`, while
`AdapterCatalog` and the resolver stay internal.

**V1 / V2.** V2: residency annotation on the Model select; an "Agency is configured but not
installed" state on the Adapter select.

---

### 6.7 `PersonaRunner` — and the `ToolKind` ask, withdrawn

**`PersonaRunner` does not change.** It takes an `IAgentHostFactory`, holds one `IAgentHost` and
one `IAgentSession`, and reads five members: `Models`, `Events`, `PromptAsync`, `CancelAsync`,
`DisposeAsync`. Of ten `AgentEvent` subtypes it reads four and ignores six by explicit comment.
Every Adapter-specific fact is resolved before the session exists. This is O-4 and it is the
whole argument for the shape.

**A correction this specification must record.** In the negotiation with Agency.NET, Huddle
asked that tool names pass through unclassified because *"our Room view renders tool activity
with per-kind iconography, so we would lose it entirely."* **That premise is false.** Verified:

```csharp
// src/Huddle.Contracts/Messages.cs:71
public sealed record ToolActivity(
    string RoomId, string MessageId, string ToolCallId, string? Title, ToolActivityStatus Status) : ProtocolMessage;
```

There is no `Kind` on the envelope, no tool **name**, and `ToolKind` appears nowhere in
`src/Huddle.App` source. `PersonaRunner.MapToolCallStatus` maps status and drops kind;
`Drafts` stores `ToolStatus` only. The Room renders **Title and Status**.

Consequences:

- The Huddle-side `ToolKind` map is **not built**. It has nothing to map into.
- `Agency.Acp` emitting `ToolKind.Other` for every call costs nothing, so the agreement stands
  on its result even though the reasoning was wrong.
- Per-kind iconography would require a **protocol bump** on `ToolActivity` — a cost item 12
  explicitly claims to avoid (*"Items 8, 9 and 12 cost no protocol bump"*). That claim survives
  only because this is not being built.

**The one line that does change**, and only for a later item: the comment at
`PersonaRunner.cs:579` listing the ignored events. `ThoughtChunk` moves out of it when Loop Kit
returns. Not now.

**Edge case worth recording.** `ToolCallStarted.Title` is human-readable from the Node adapter
("Read file X") and will be the raw MCP tool name from `agency-acp` (`post_message`), because
`McpProxyTool` passes the server's name through unmodified. The Room will look different per
Adapter. That is acceptable and should not be papered over in `PersonaRunner`.

---

### 6.8 `DotAcpAgentSession` — `session/close`

**Purpose.** Release the far side's session graph when Huddle disposes a session.

**The defect.** `DisposeAsync` completes a local channel and invokes a local callback. It makes
**no wire call**, and `CloseAsync` / `session/close` appear nowhere in `src/`.

```csharp
public ValueTask DisposeAsync()
{
    lock (this.gate) { if (this.disposed) { return ValueTask.CompletedTask; } this.disposed = true; }
    this.onDisposed(this.SessionId);
    this.channel.Writer.TryComplete();
    return ValueTask.CompletedTask;          // ← no session/close
}
```

Against `claude-agent-acp` this is nearly harmless — the process dies with the Persona. Against
any adapter that outlives one session it leaks, and `OnSessionEnd` never fires on the far side.

**Change.** `DisposeAsync` becomes `async ValueTask`, and sends `session/close` on a best-effort
basis before completing the channel:

```csharp
try
{
    await this.connection.CloseAsync(new CloseSessionRequest { SessionId = this.SessionId }, ct);
}
catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
{
    // The process is already gone, or the pipe is closed. Disposal must complete regardless -
    // this is a courtesy to the agent, never a condition of our own teardown.
}
```

**Constraints.**
- Bounded by a short timeout (2 s). Disposal happens on the shutdown path; a hung adapter must
  not hold it.
- Idempotent — the `disposed` flag already guarantees one send.
- `Agency.Acp` §6.3 disposes on **close, transport disconnect, or process shutdown, whichever
  first**, so this is an optimisation on the far side, never the mechanism. We send it anyway
  because it is correct and because it is two lines.

---

### 6.9 `AppToolServer` — `WWW-Authenticate`

**Change.** The existing 401 gains a header:

```csharp
context.Response.StatusCode = StatusCodes.Status401Unauthorized;
context.Response.Headers.WWWAuthenticate = "Bearer";   // bare - no resource_metadata
return;
```

**Why bare.** MCP's authorization spec keys OAuth discovery off a `401` carrying
`WWW-Authenticate` **with `resource_metadata`**. Agency's MCP client leaves
`HttpClientTransportOptions.OAuth` unset. A parameterised challenge risks sending a future SDK
version down a discovery path neither side has designed; a bare one is inert to the client and
makes a raw wire trace self-explanatory.

**Value, stated honestly.** Cosmetic. Agency logs the first-attempt status on `AutoDetect`
fallback, which is what actually fixes the diagnosis — a missing bearer token surfaces to their
client as `404`, not `401`, because `AutoDetect` reads a `401` on POST as "no Streamable HTTP
here" and falls through to an SSE `GET` that our POST-only route rejects. This header does not
change that; it makes the trace readable.

---

### 6.10 `Huddle.MockAdapter` — an Adapter this solution builds

**Purpose.** Remove Agency.NET's delivery date from Huddle's critical path, and give every
Huddle-side path an automated test that runs in CI for free — no Node install, no Claude
subscription, no GPU, no second repository.

**This is test infrastructure, not a stand-in.** It is the reference implementation of the
protocol contract negotiated with Agency.NET, so a conformance suite written against it runs
unchanged against `agency-acp` when that lands.

#### What already exists

`tests/Huddle.Acp.Tests/Fakes/FakeAcpAgent.cs` is a complete ACP **agent** — raw
newline-delimited JSON-RPC 2.0 over a `Stream`, no protocol library — handling exactly the five
methods `Agency.Acp` is being built to implement:

| Handled | Emitted by `PromptContext` |
| --- | --- |
| `initialize` | `agent_message_chunk` (`SendTextChunkAsync`) |
| `session/new` | `agent_thought_chunk` (`SendThoughtChunkAsync`) |
| `session/set_config_option` | `tool_call` (`SendToolCallAsync`) |
| `session/prompt` | `tool_call_update` (`SendToolCallUpdateAsync`) |
| `session/cancel` (notification) | `session/request_permission` (`RequestPermissionAsync`) |

It is already both scriptable and an oracle:

```csharp
internal Func<JsonObject, JsonObject>        OnInitialize      { get; set; } = DefaultInitialize;
internal Func<JsonObject, Task<JsonObject>>  OnNewSession      { get; set; } = DefaultNewSessionAsync;
internal Func<JsonObject, Task<JsonObject>>  OnSetConfigOption { get; set; } = DefaultSetConfigOptionAsync;
internal Func<PromptContext, Task<string>>   OnPrompt          { get; set; } = DefaultPromptAsync;

internal List<JsonObject> Received { get; }                       // every request, in order
internal Task<JsonObject> WaitForAsync(string method, TimeSpan);  // race-safe
```

`Received` is the part that matters for §15: it makes the mock an **assertion oracle for the
Huddle → Adapter direction**. A golden test proves `SystemPromptComposer` composes the right
string; `Received` proves that string actually arrives in `session/new`'s `_meta`.

#### Two modes

| Mode | Transport | Used by | Cost |
| --- | --- | --- | --- |
| **In-proc** | `FullDuplexStream.CreatePair()` (Nerdbank.Streams, already referenced) | most conformance tests | microseconds |
| **Process** | `mock-acp` over stdin/stdout, launched as an ordinary Adapter Profile | the launch-path slice, and `dotnet run` | one process spawn |

In-proc is the default because it is fast and deterministic. Process mode exists to prove the
one thing in-proc cannot: that `AgentProcessOptionsFactory` → `AgentProcessLauncher` →
`DotAcpAgentHost` actually launches and talks to a real child process under a profile.

#### Placement, and why the source is linked rather than moved

`FakeAcpAgent`, `PromptContext` and `FakeRpcError` are `internal` to `Huddle.Acp.Tests`, which
belongs to the ACP effort's subtree. The mock project **links** those files rather than moving
them:

```xml
<Compile Include="..\Huddle.Acp.Tests\Fakes\FakeAcpAgent.cs" Link="Fakes\FakeAcpAgent.cs" />
```

- No file is moved out of the other effort's subtree.
- No `internal` is widened — a linked file compiles into this assembly as its own `internal`.
- **One implementation.** Their fake is the ACP effort's statement of what an ACP agent does; if
  they change it, the mock changes with it, which is the correct direction of dependency.

Adding the project to `Huddle.slnx` touches a shared root file, so it is **announced**, not
merged silently.

#### Behaviour

| Aspect | Default | Overridable |
| --- | --- | --- |
| `initialize` | protocol version 1, `authMethods: []`, `supportsLoadSession: false` | yes |
| `session/new` | a two-model catalog and a two-entry effort ladder | yes |
| `session/prompt` | echoes the prompt back as several `agent_message_chunk`s, then `end_turn` | yes |
| Tool call | one scripted `get_help` call on the first prompt of a session | yes |
| Failure injection | off | a JSON script file names the method and the error |

Chunked echo is the default because **it proves the thing most likely to be wrong**: that text
arrives incrementally, becomes `MessageDelta` on the pipe, and renders live in the Room.

#### Constraints

- **stdout carries protocol bytes only.** Every log sink goes to stderr. A stray
  `Console.WriteLine` corrupts the JSON-RPC stream; `Agency.Acp` §6.1 calls this "the single most
  likely v1 defect" and asserts against it. The same assertion belongs here.
- The mock must **not** become a second implementation of Huddle behaviour. It answers the
  protocol and records what it received. Any temptation to make it *clever* — to validate a
  prompt, to reason about Rooms — is a sign the assertion belongs in a Huddle test instead.
- A mock cannot prove that a real model **calls** the tools. Progressive discovery bets the model
  is strong enough to ask for `get_help`; a mock calls what it is scripted to call. Roadmap item
  12 already names this as an honest limit, and §15 Tier 4 is where it is settled.

#### V1 / V2

V1: in-proc plus a process mode, default behaviours, `Received` as the oracle. V2: a JSON script
format so a manual-test scenario can be replayed without recompiling, and latency injection to
rehearse the cold-model-load case (E-10).

---

## 7. Data model and storage

### 7.1 Nothing new is stored

This is the load-bearing property. The roadmap's Ordering section is binding:

> Each store added is one more place a rename has to touch, and that cost never goes down.

`PersonaRenameCascade` today touches the Team Directory row, `persona_models`,
`persona_efforts`, the Room names and the Work Dir. **It gains nothing here.**

| Datum | Where it lives | Touched by a rename? |
| --- | --- | --- |
| Persona text | the `.md` file | the file is not moved; `name:` is edited in place |
| Model | `persona_models` (SQLite) | yes — existing |
| Effort | `persona_efforts` (SQLite) | yes — existing |
| **Adapter** | **the `.md` file's frontmatter** | **no — it travels with the file** |

### 7.2 The frontmatter key

```yaml
---
name: Ana
title: Router
alias: ana
teams: product, ops
adapter: agency
---
```

**Key name.** `adapter`, matched `OrdinalIgnoreCase` like the other four structural keys.

**Not `_adapter`.** The `_` prefix is reserved for *"future programmatic use"* and its only
implementation is a filter that hides such keys from the job description. But `adapter` is not
hidden knowledge — it is a first-class, human-edited property that now has a dropdown, exactly
like `title`. Item 12 proposed `_host:` when this was to be invisible plumbing; a visible
control changes the argument. The `_` prefix stays reserved for keys that never surface.

**Consequence — `adapter` joins the structural keys.** `PersonaFrontmatter` holds four
`private const` key names; it gains a fifth. Three follow-ons, all mandatory:

1. **`ComposeJobDescription` must exclude it.** Today `JobDescriptionExcludedKeys` contains only
   `Name`. Without adding `Adapter`, every Teammate's job description in
   `mcp__team__list_agents` gains a line reading `Adapter: agency` — model-facing text about
   Huddle's plumbing, which no Agent can act on.
2. **`Compose` must emit it.** The write path emits *only* the identity keys, so a key written
   at Create time is **destroyed**. `PersonaIdentity` gains the field, or `Compose` gains a
   parameter.
3. **`TryReadIdentity` must not require it.** Absent means the default profile. A file without
   `adapter:` is a valid Persona — there are existing files, and P6 demands they keep working.

**The `WriteScalarField` path.** `Update` writes raw text through unchanged, so an Edit-time
change uses `PersonaFrontmatter.WriteScalarField(text, "adapter", value)` — the same mechanism
that gave Name, Title and Alias discrete boxes over the frontmatter. It returns the input
unchanged for a block scalar or block list, which is what drives the card's `*Locked` flags; a
one-line scalar is always rewritable, so `adapter` is never locked.

### 7.3 The `Persona` record

```csharp
public sealed record Persona(
    string Name, string Text, string? Model = null, string? Effort = null, string? Adapter = null);
```

**Why this gets restart-on-change for free.** `PersonaSupervisor.NeedsRestart` is

```csharp
private static bool NeedsRestart(Persona persona, Persona? started) =>
    started is null || persona != started;
```

— pure record value equality, adopted precisely so *"the check cannot go stale the next time
`Persona` grows a field"*. Adding `Adapter` satisfies O-2 with no code change in the supervisor.
A test must pin it anyway, because the property is invisible at the call site.

**Where it is populated.** `PersonaStore.Get` is the only file↔DB join:

```csharp
return new Persona(entry.Name, entry.Text, this.models.Get(entry.Name), this.efforts.Get(entry.Name), entry.Adapter);
```

So `PersonaEntry` gains `Adapter` alongside `Path` and `Text`, filled by `PersonaIndex.Build`
from the parsed identity.

### 7.4 Configuration

```jsonc
"Team": {
  "Acp": {
    "Enabled": false,
    "Adapters": [
      {
        "Id": "claude",
        "DisplayName": "Claude",
        "Description": "cloud, spends money per turn",
        "Command": "node",
        "UsesToolNamePrefix": true
      },
      {
        "Id": "mock",
        "DisplayName": "Mock",
        "Description": "a scripted adapter for development - no model",
        "Command": "C:/repos/Huddle/artifacts/mock-acp/mock-acp.exe",
        "UsesToolNamePrefix": false
      },
      {
        "Id": "agency",
        "DisplayName": "Agency",
        "Description": "a local model on this machine, free",
        "Command": "C:/tools/agency-acp/agency-acp.exe",
        "Args": [ "--Agent:UserId=00000000-0000-0000-0000-000000000000" ],
        "UsesToolNamePrefix": false,
        "EnvironmentOverrides": {
          "Agent__DefaultModel": "google/gemma-4-e2b",
          "Agent__DefaultClientName": "local",
          "Agent__TurnTimeoutSeconds": "180"
        }
      }
    ]
  }
}
```

**Legacy keys survive.** `Command`, `AdapterPath` and `Args` remain on `AcpOptions` and are read
only when `Adapters` is absent. Removing them would break every existing `appsettings` and gains
nothing.

**`Args` must have no initialiser.** `ConfigurationBinder` *appends* to a pre-populated
collection instead of replacing it, silently doubling the value (`rules.md`). This applies to
`AdapterProfile.Args` exactly as it applies to `AcpOptions.Args`.

**`EnvironmentOverrides` must have no initialiser either, for a different reason.** The binder
does not *double* a dictionary the way it doubles a list — it writes each bound key into the
existing instance — but a key a pre-populated default carried and configuration does not mention
**survives**, and the binder has no operation that can remove one. An operator who deletes a stale
`Agent__DefaultModel` from `appsettings.json` would find it still handed to the process, with
nothing to explain why. Same verdict, different mechanism. `AdapterCatalog` copies rather than
aliases the bound dictionary, keyed `StringComparer.Ordinal`, and normalises empty to `null` so a
stock installation's launch is byte-identical.

> **Set these from `appsettings.json`, never through the environment-variable provider.** An
> environment variable name contains `__`, and that provider rewrites every `__` into `:` — so
> `Team__Acp__Adapters__0__EnvironmentOverrides__Agent__DefaultModel` binds as the key
> `Agent:DefaultModel`, which no process will ever read. No code can prevent this; it is why the
> trap is written down in three places.

**Ordinal, and no case-folding.** `ProcessStartInfo.Environment` already applies the platform's
own rule — case-insensitive on Windows, case-sensitive on Unix — so folding case here would make
`Agent__DefaultModel` and `agent__defaultmodel` collide on **Linux**, where they are genuinely two
different variables. Each layer applies its own rule.

---

## 8. Core algorithms

### 8.1 Resolving an Adapter for a Turn

```text
 1  PersonaStore.Get(name)
      ├─ PersonaIndex.ByName → PersonaEntry{ …, Adapter }
      └─ join persona_models, persona_efforts
      → Persona{ Name, Text, Model, Effort, Adapter }
 2  PersonaSupervisor: NeedsRestart(persona, startedByName[name])?
      └─ record inequality on ANY of Text | Model | Effort | Adapter → restart
 3  new PersonaRunner(persona, …)  ── unchanged
 4  runner connects the pipe, says hello, waits for welcome → agentId
 5  factory.CreateAsync(persona, agentId)
      ├─ resolver.Resolve(persona.Adapter) → (profile, warning?)
      ├─ warning? → log; supervisor reports Degraded
      ├─ AgentProcessOptionsFactory.TryCreate(profile, workDir, AppContext.BaseDirectory)
      │    └─ null → InvalidOperationException naming the profile and install.ps1
      ├─ prefix = profile.UsesToolNamePrefix ? "mcp__team__" : ""
      ├─ tools, AppToolServer, DotAcpAgentHost, StartSessionAsync
      └─ (ToolServerOwningAgentHost, session)
 6  runner reads session.Models; a stored Model absent from a NON-EMPTY catalog → Degraded
```

**Step 6 is unchanged and now covers two cases.** An empty `Models` list means *unknown*, never
"no models" — several agents take their model from configuration and advertise nothing. A
Persona switched from `claude` to `agency` carrying a stale `persona_models` row therefore
either degrades with a clear message (Agency advertises a catalog and the id is not in it) or
runs silently on the default (Agency advertises nothing). Both are correct; neither blocks.

### 8.2 Surfacing the resolver warning

`IAgentHostFactory.CreateAsync` returns `(IAgentHost, IAgentSession)`. Widening that tuple would
touch `FakeAgentHostFactory` and ~25 supervisor test call sites for one diagnostic string.

**Chosen:** the resolver is injected into `PersonaSupervisor` as well, which calls it *before*
constructing the runner and reports `Degraded` itself:

```csharp
var (_, warning) = this.resolver.Resolve(persona.Adapter);
if (warning is not null)
{
    this.health.Report(name, PersonaState.Degraded, warning);
}
```

**Why there and not in the factory.** `PersonaHealth` is already the supervisor's dependency,
the supervisor already reports `Offline` with a reason when `Acp:Enabled` is false, and the
resolver is pure — calling it twice costs nothing and keeps `IAgentHostFactory`'s signature
frozen. Two calls, one truth, because the function has no state.

### 8.3 The card's cascade

```text
Adapter changed
  ├─ model := null,  effort := null
  ├─ adapterResetModelAndEffort := (previous model or effort was set)
  ├─ availableModels := probe(adapter)            [await]
  ├─ StateHasChanged()                            ← MANDATORY
  └─ availableEfforts := probe(adapter, null)     [await]

Model changed
  ├─ effort := null
  ├─ effortResetByModelChange := (previous effort was set)
  └─ availableEfforts := probe(adapter, model)    [await]

Effort changed
  └─ effort := value; clear both reset flags
```

**Generation counters.** `LoadEffortsAsync` already guards against a stale probe answering after
a newer one via `effortProbeGeneration`. `LoadModelsAsync` has no such guard because nothing
could previously re-enter it. **It needs one now** — changing the Adapter twice quickly starts
two model probes, and the slower one must not overwrite the faster. This is a new defect class
introduced by this change and is pinned by a test.

### 8.4 Error handling

| Condition | Behaviour |
| --- | --- |
| `adapter:` names an unconfigured id | default profile, `Degraded` with the id named |
| `adapter:` present but blank | treated as absent; default profile, no warning |
| Profile resolves, executable missing | `InvalidOperationException` naming the profile **and** `install.ps1`; supervisor reports `Failed` |
| Catalog probe throws | empty list, logged at Warning, **never cached** — installing the adapter and reopening the card works with no restart |
| Probe exceeds 20 s | empty list; same as above |
| `session/close` throws on dispose | swallowed with a comment; disposal completes |
| A profile has a blank `Command` | startup throws — configuration is wrong, not a Persona |

---

## 9. Incremental vs full processing

| Work | Trigger | Scope |
| --- | --- | --- |
| Adapter catalog build | process start | full, once |
| Persona index rebuild | `FileSystemWatcher`, 500 ms debounce | **full** — every `.md` under the Teams dir, recursively |
| Model catalog probe | a Create/Edit card opening, or an Adapter change | incremental per adapter id; cached on success only |
| Effort ladder probe | a card opening, an Adapter change, or a Model change | incremental per `(adapter, model)` |
| Runner restart | `PersonasChanged` **and** `NeedsRestart` | one Persona |

**The index rebuild stays full and that is correct.** Collision detection — duplicate Name,
duplicate Alias, Alias colliding with another file's Name — is a whole-set property. An
incremental rebuild could not decide whether a removed file *released* a collision, and both
sides of a collision are rejected rather than one arbitrary winner. Adding `adapter` does not
participate in any collision rule, so it does not change this.

**Probe caching is the only place staleness is visible.** A model added to LM Studio after the
first probe will not appear until the process restarts. Accepted: the existing Claude probe has
the same property, and a failed probe is never cached, which covers the case that actually bites
(opening the card before the adapter is installed).

---

## 10. Background workers and async components

| Component | Kind | Lifetime | Notes |
| --- | --- | --- | --- |
| `PersonaSupervisor` | `BackgroundService` | app | returns immediately when `Acp:Enabled` is false, before subscribing to `PersonasChanged` |
| `PersonaRunner` | per-Persona, **not** a hosted service | a Persona's run | `rules.md`: hosted services are fixed at build time; these are created at runtime |
| `PersonaRunner` loops (×3) | `Task` | a Persona's run | read loop, work-item consumer, event reader — all unchanged |
| `PersonaStore` watcher | `FileSystemWatcher` | app | 500 ms debounce, `IncludeSubdirectories`, 64 KB buffer |
| `PersonaRenameCascade` | `IHostedService` | app | **gains nothing** (§7.1) |
| `ModelCatalogProbe` | on-demand | per probe | one shared `SemaphoreSlim(1,1)`; 20 s timeout |
| Adapter process | OS process | a Persona's run | one per Persona per Adapter |
| `mock-acp` | OS process **or** in-proc | a test, or a `dotnet run` | in-proc by default; process mode only for T-0b and T-30 |

**Concurrency constraints.**

- The probe gate stays **one** semaphore across all Adapters. Two Blazor circuits opening two
  cards must not spawn two processes, whichever Adapter each names.
- `PersonaRunner`'s work queue is mandatory, not an optimisation: `IAgentSession.PromptAsync`
  throws if a Turn is already in flight. Unchanged.
- `AdapterCatalog` is immutable after construction and needs no synchronisation.
- Two adapter processes for two Personas is the normal case. Nothing in Huddle bounds concurrent
  inference; the ceiling is enforced outside this process, on the inference port.

---

## 11. Performance expectations

### 11.1 Latency budgets

| Operation | `claude` | `agency` | `mock` | Note |
| --- | --- | --- | --- | --- |
| Adapter catalog build | < 1 ms | < 1 ms | < 1 ms | configuration only |
| Card opens, models probed | 300 ms – 8.5 s | process start + one `GET /v1/models` | **in-proc: µs** · process: one spawn | issue #39 measured the Claude path at 265 ms for two entries and 8.5 s for six |
| Effort ladder probe | same shape | expected trivial — the ladder is static per surface | µs | |
| Adapter change → both selects repopulated | two sequential probes | two sequential probes | µs | ~2× a single probe; the mandatory `StateHasChanged()` is what stops it *looking* like 2× |
| Runner start → first Turn | process start + `initialize` + `session/new` | + a cold model load, 10–60 s | **deterministic** | the cold-load case is new and user-visible |

**The mock's column is why Tier 3 is affordable.** A conformance suite against a peer that
answers in microseconds and never varies can run on every build; the same suite against
`agency-acp` cannot, which is why Phase 7 is opt-in.

**The cold model load is the one new latency class.** A local model that is not resident takes
tens of seconds before the first chunk. `PersonaRunner`'s existing failure surfacing shows a
Turn in flight, so this reads as slow rather than broken — but it is why residency annotation on
the Model select is a v2 item worth having.

### 11.2 Throughput ceiling

One process per Persona per Adapter. Six Personas in a Room is six processes. Memory per process
differs sharply: the Node adapter is small; `agency-acp` is framework-dependent .NET 10 carrying
both provider SDKs and the PowerShell SDK, which is materially heavier.

**Huddle imposes no inference ceiling and should not.** The invariant that matters is *"no more
than N concurrent inferences on the box"*, which is a property of the endpoint and cannot be
enforced by any client — a fact established by a `curl` saturating the same GPU with no adapter
involved. Enforcement lives on the inference port, outside both products.

### 11.3 Scaling notes

- `DotAcpAgentHostFactory`'s own doc comment says to revisit the per-Persona tool server *"past
  roughly four Personas"*. That threshold is unchanged and is now reached with a heavier process
  behind it.
- The probe cache is unbounded but keyed on a configured, small set — at most
  `adapters × (models + 1)` entries.
- The full index rebuild is `O(files)` with one `File.ReadAllText` each, debounced at 500 ms.
  Unchanged.

---

## 12. Edge cases and failure modes

| # | Case | Behaviour | Severity |
| --- | --- | --- | --- |
| E-1 | `adapter:` names an unconfigured id | default profile, `Degraded` naming the id | low — P4 |
| E-2 | Only one profile configured | Adapter select does not render; behaviour identical to today | **must hold** — P6 |
| E-3 | Persona switched to `agency` with a Claude model stored | empty catalog → silent default; non-empty → `Degraded` | low |
| E-4 | Two rapid Adapter changes | stale model probe must not overwrite the newer | **medium — new defect class**, §8.3 |
| E-5 | `adapter:` written at Create time | **destroyed** unless `Compose` emits it | **high** — §7.2 |
| E-6 | `adapter:` leaks into the job description | model-facing text about plumbing | **high** — §7.2, exclusion required |
| E-7 | `agency` profile falls through to `AdapterLocator` | would launch the Node adapter under the wrong name | **high** — §6.3 guard |
| E-8 | Adapter configured, executable missing | `InvalidOperationException` naming profile + `install.ps1`; `Failed` | medium |
| E-9 | Local endpoint down | session starts, first Turn fails, `Degraded` | medium |
| E-10 | Cold model load | 10–60 s to first chunk; reads as a slow Turn | expected |
| E-11 | Tool titles differ per Adapter | Room shows `post_message` vs "Read file X" | low — do not paper over |
| E-12 | `session/close` throws | swallowed; disposal completes | low |
| E-13 | Token Budget on a local Adapter | `UsageUpdated` semantics are occupancy on both; the Budget stays calibrated | low |
| E-14 | A test reaches the real probe | spawns a process | **forbidden** — `rules.md` row 35 |
| E-16 | Mock writes to stdout | a stray `Console.WriteLine` corrupts the JSON-RPC frame | **high** — §6.10, asserted by T-0b |
| E-17 | Mock grows Huddle behaviour | an assertion passes against logic the mock invented | **medium** — P8; that assertion belongs in a Huddle test |
| E-18 | Mock and contract diverge | Tier 3 green, Phase 7 red | **by design** — T-31 is where it surfaces, and finding it is the point |
| E-15 | Golden prompt for the unprefixed profile | `PromptGoldenTests` pins `mcp__team__` only | medium — a second golden is required |

---

## 13. End-to-end flow

```text
 1  Human opens New Teammate
      └─ card: Adapter ▾ (2 profiles)  Model ▾ (disabled)  Effort ▾ (disabled)
 2  Human picks Adapter = Agency
      ├─ model := null, effort := null
      ├─ probe(agency) ─── spawn agency-acp, initialize, session/new, read Models, dispose
      ├─ StateHasChanged()
      └─ probe(agency, null) → effort ladder (two entries, or empty)
 3  Human picks Model = google/gemma-4-e2b, saves
      ├─ PersonaFrontmatter.Compose emits name/title/alias/teams/adapter
      ├─ PersonaStore.Add writes the file; persona_models row written
      └─ FileSystemWatcher (500 ms) → index rebuild → PersonasChanged
 4  PersonaSupervisor
      ├─ Acp:Enabled? no → Offline with a reason; stop
      ├─ resolver.Resolve("agency") → (agency profile, no warning)
      └─ new PersonaRunner(persona, …) → StartAsync
 5  PersonaRunner
      ├─ connect pipe → hello → welcome{agentId}
      └─ factory.CreateAsync(persona, agentId)
           ├─ profile = agency;  prefix = ""
           ├─ processOptions = (C:/tools/agency-acp/agency-acp.exe, [--Agent:UserId=…], workDir)
           ├─ token = 32 random bytes
           ├─ tools = [get_help, list_agents, create_room, invite_agent,
           │           post_message, follow_room, unfollow_room]
           ├─ AppToolServer("team", tools, 127.0.0.1:0, token)
           ├─ DotAcpAgentHost.StartAsync → initialize {protocolVersion 1}
           └─ StartSessionAsync
                cwd          = {DataDir}/work/Ana
                mcpServers   = [{ team, http://127.0.0.1:p/mcp, Authorization: Bearer … }]
                _meta        = { append: SystemPromptComposer.Compose(persona, hooks,
                                          "get_help", ["get_help", "list_agents", …]) }
                model        = "google/gemma-4-e2b"
 6  A Message arrives in #product Mentioning @Ana
      ├─ ReplyGate → Expected
      ├─ PersonaRunner queues a WorkItem, builds "[#product] You: …"
      ├─ session.PromptAsync
      │    ← MessageChunk ×N        → MessageDelta on the pipe → live in the Room
      │    ← ToolCallStarted/Updated → ToolActivity (Title + Status)
      │    ← UsageUpdated            → tokensConsumed += rises
      │    ← TurnCompleted{EndTurn}  → post the reply
      └─ Online
 7  Human clicks Stop
      ├─ StopTurn over the pipe → turn.Cancellation.Cancel(); session.CancelAsync()
      └─ stopped, not failed; no health state, no alert, streak unbroken
 8  Human edits Ana, Adapter → Claude
      ├─ WriteScalarField(text, "adapter", "claude"); model/effort cleared, note shown
      ├─ PersonaStore.Update writes raw text through
      ├─ NeedsRestart: Adapter differs → restart
      └─ session disposed → session/close sent → agency-acp exits → node starts
```

---

## 14. Design notes and rationale

**Why there is no second `IAgentHostFactory`.** Item 12 wrote the plan before an ACP server
existed on the Agency side. With one, seven of eight steps in `CreateAsync` are identical and
the eighth is a process path. A second factory would have duplicated the tool construction, the
token mint, the tool server, the host and the session options — five things that must stay in
step and would silently drift.

**Why `ModelCatalogProbe` is reused despite item 12 saying not to.** The instruction to
implement `IModelCatalog` again assumed the local path would be an HTTP call and the ACP path a
process spawn, so sharing a class shaped around the expensive path would be wrong. Under ACP the
local path *is* a process spawn: `Agency.Acp` answers `session/new` with its catalog exactly as
the Node adapter does. The caching and timeout are correct for both; only the key changes.

**Why the Adapter is frontmatter and not a store.** Two reasons, one binding. The binding one is
the Ordering warning: a store is a rename cost forever. The other is that an Adapter travels
with a file — copy a Persona to another machine and its Adapter comes too, which is right,
whereas a Model id from another machine's catalog is meaningless and correctly does not.

**Why `adapter` and not `_adapter`.** The `_` prefix is reserved for keys that never surface,
and its only implementation is a filter hiding them from the job description. Item 12 proposed
`_host:` when this was invisible plumbing. A dropdown makes it a first-class property, like
`title`, and first-class properties are not hidden. The reservation stands for item 9's `_tools`.

**Why the term is Adapter and not Provider.** `language.md` already defines **Adapter** as the
thing that speaks ACP; item 12 already requires widening it from one package to a category.
Introducing "Provider" would add a fifth word for a concept that has one — and it collides with
Agency.NET's `IModelProvider`, which names the OpenAI-compat vs Anthropic-compat *surface*, a
different axis. One word, two meanings, two repos is how the vocabulary needed retiring the
first time.

**Why changing the Adapter resets Model and Effort.** A Claude model id is meaningless to
Agency. Carrying it over guarantees a stale-model warning on every restart, forever — the exact
failure `rules.md` records for storing a display name instead of an id. The precedent is one
level down and identical: `OnModelChangedAsync` already resets Effort and says so in a note.

**Why the warning is raised by the supervisor, not returned by the factory.** Widening
`IAgentHostFactory`'s tuple for one diagnostic string would touch the fake and ~25 test call
sites. The resolver is pure, so calling it twice is free and the signature stays frozen. Cost:
two call sites must agree, which a test pins.

**Why `PersonaRunner` is untouched, and why that is the argument.** Its XML doc says
`IAgentHostFactory` exists *"purely as a test seam"*. A third implementation is the shape the
file anticipates — and under this design there is no third implementation at all, only a third
configuration. The pipe, the Reply Gate, the catch-up buffers and every App Tool sit on the far
side and never learn what is answering.

**Why the `ToolKind` map is withdrawn rather than built.** The ask was made on a false premise:
`ToolActivity` carries no kind and nothing in `src/Huddle.App` references `ToolKind`. Building a
map with nothing to map into, or bumping the protocol to carry a kind, would both be work
created by a mistaken claim. Recording the correction is cheaper than either, and it preserves
item 12's "no protocol bump" property.

**Why a mock Adapter, and why it is built first.** Two reasons, and the second changed the
plan. The first is scheduling: Agency.NET's delivery date leaves Huddle's critical path, and
every phase but the last runs green without it. The second is that the original plan had
twenty-one tests that each proved one unit, and then a manual milestone — **nothing proved the
subsystems worked together.** A peer that records what it received closes that gap, and the
test it enables (T-23: the prompt a peer *actually receives* names the right tools) is one no
golden file can write.

**Why the mock's source is linked rather than moved.** `FakeAcpAgent` belongs to the ACP
effort's test tree. Linking keeps one implementation, moves no file out of their subtree and
widens no `internal`. The coupling runs the right way: their fake is that effort's statement
of what an ACP agent does, and the mock should follow it rather than drift from it.

**Why the mock never validates.** It would be easy to have it assert that a prompt names the
tools, or that a Room id is well formed. Every such assertion is one Huddle cannot see
failing — it turns a test oracle into a second implementation, and when the two disagree
neither is authoritative. The mock records; Huddle's tests assert.

---

## 15. Test-first task plan

Every implementation task is preceded by its test task. Tests are written first and must fail
**for the right reason** before implementation begins. Vertical slices: one test → one
implementation → next. Do not write the whole test column first.

**Conventions.** `tests/Huddle.Tests` for `Huddle.App`, `tests/Huddle.Acp.Tests` for
`Huddle.Acp`. Every test method carries a `///` summary, one `[Fact]`/`[Theory]`, at least one
`Assert`, and `TestContext.Current.CancellationToken` on every awaited call that takes one.
`dotnet test Huddle.slnx --` — the trailing `--` is required or the run reports "Zero tests ran".

### 15.1 Four tiers, and which are blocked

| Tier | What it proves | Needs | Runs in CI | Blocked by Agency? |
| --- | --- | --- | --- | --- |
| **1 — Unit** | one type behaves | nothing | always | **no** |
| **2 — Component** | a Huddle subsystem behaves, against fakes | `FakeModelCatalog`, `FakeAgentHostFactory` | always | **no** |
| **3 — Conformance** | Huddle drives a real ACP peer correctly | `Huddle.MockAdapter` (§6.10) | **always** | **no** |
| **4 — Live** | a real model behaves | `agency-acp` + a local endpoint | opt-in | yes — and only this |

**Tier 3 is the answer to "do not be blocked".** It is written once against a contract and runs
twice: against the mock now, in CI, unconditionally; and against `agency-acp` when it lands, by
changing one Adapter Profile. Nothing in Tiers 1–3 waits for anything.

For contrast, the existing process-spawning tests — `tests/Huddle.Acp.Tests/E2E/` — are gated
behind `TEAM_E2E=1` and require a Node install, so **they never run in CI**. A mock this solution
builds is always present, which is why Tier 3 can be unconditional where E2E never could be.

### 15.2 Phase 0 — the mock (do this first)

| # | Test task (first) | Implementation task |
| --- | --- | --- |
| T-0a / I-0a | `MockAdapterTests`: driven in-proc over a `FullDuplexStream` pair, it answers `initialize`, `session/new` and a prompt, and `Received` records all three in order | project + linked `FakeAcpAgent`; a duplex wrapper over two streams |
| T-0b / I-0b | `MockAdapterTests`: launched **as a process**, stdout carries protocol bytes only — a log write during a turn does not corrupt a frame | `Main`; every sink to stderr |
| T-0c / I-0c | `MockAdapterTests`: the default prompt handler emits **more than one** `agent_message_chunk` before `end_turn` | chunked echo |

### 15.3 Phase 1 — configuration and resolution · Tier 1

| # | Test task (first) | Implementation task |
| --- | --- | --- |
| T-1 / I-1 | `AdapterCatalogTests`: an absent `Adapters` list synthesises exactly one `claude` profile from `Command`/`AdapterPath`/`Args`, `UsesToolNamePrefix` true | `AdapterProfile`, `AdapterCatalog`, `AcpOptions.Adapters` |
| T-2 / I-2 | a blank `Command` on any profile throws at construction, naming the id | fail-fast validation |
| T-3 / I-3 | `AdapterProfileResolverTests`: null → default, no warning; unknown → default **with** a warning naming the id; `OrdinalIgnoreCase` | `AdapterProfileResolver` |
| T-4 / I-4 | `AgentProcessOptionsFactoryTests`: the Args → AdapterPath → Locate → null precedence survives, **and an unprefixed profile never reaches `AdapterLocator`** (E-7) | retarget to `AdapterProfile` |

### 15.4 Phase 2 — the Persona field · Tier 1

| # | Test task (first) | Implementation task |
| --- | --- | --- |
| T-5 / I-5 | `PersonaFrontmatterTests`: `adapter:` parses; absent is valid; blank is absent | fifth structural key |
| T-6 / I-6 | `adapter` **never** appears in `ComposeJobDescription` (E-6) | `JobDescriptionExcludedKeys` |
| T-7 / I-7 | `Compose(identity, body)` round-trips `adapter` — a Create-time value survives (E-5) | `PersonaIdentity.Adapter` |
| T-8 / I-8 | `PersonaStoreTests`: `Get` returns `Persona.Adapter`; `Update` preserves a hand-written value | `PersonaEntry.Adapter`, `PersonaIndex.Build` |
| T-9 / I-9 | `PersonaSupervisorTests`: two Personas differing **only** in `Adapter` are unequal → `NeedsRestart` (O-2) | `Persona.Adapter` |

### 15.5 Phase 3 — factory and catalogue · Tier 1–2

| # | Test task (first) | Implementation task |
| --- | --- | --- |
| T-10 / I-10 | `PromptGoldenTests`: a **second golden** for the unprefixed profile names `get_help`; the prefixed golden stays byte-identical (E-15, P6) | prefix from the profile |
| T-11 / I-11 | `GetHelpToolTests`: an empty prefix yields bare names and no `mcp__` | none — parameter exists |
| T-12 / I-12 | model cache keyed per adapter; effort cache per `(adapter, model)`; a **failed** probe is never cached | re-key both caches |
| T-13 / I-13 | `PersonaSupervisorTests`: an unconfigured `adapter:` reports `Degraded` naming the id, and the runner still starts (E-1) | resolver in the supervisor |

### 15.6 Phase 4 — the card · Tier 2

| # | Test task (first) | Implementation task |
| --- | --- | --- |
| T-14 / I-14 | one profile configured → the Adapter select **does not render** (E-2, P6) | conditional render |
| T-15 / I-15 | two profiles → it renders, and selecting one probes **that** adapter | `OnAdapterChangedAsync` |
| T-16 / I-16 | changing the Adapter clears Model **and** Effort with a `role="status"` note | `adapterResetModelAndEffort` |
| T-17 / I-17 | two rapid Adapter changes — the **older** model probe's answer is discarded (E-4) | generation counter |
| T-18 / I-18 | a plain GET still probes **zero** times (`rules.md` row 35) | regression guard |

### 15.7 Phase 5 — `Huddle.Acp` · Tier 1

| # | Test task (first) | Implementation task |
| --- | --- | --- |
| T-19 / I-19 | disposal sends `session/close` once; a throwing close still completes disposal; a second dispose sends nothing | `DisposeAsync` → `async ValueTask` |
| T-20 / I-20 | a 401 carries `WWW-Authenticate: Bearer` with **no** `resource_metadata` | one header line |

### 15.8 Phase 6 — conformance · Tier 3 · **the part that was previously missing**

Each of these drives a **real Persona through a real ACP peer**. They are the only tests that
prove the subsystems work together, and none of them needs Agency.NET.

**Two kinds of assertion live here, and only one of them travels.** Corrected 2026-09-18, after
Phase 7 made the difference concrete — see the amendment under D-12 in §17.

- **Portable** — asserts on **observable Huddle behaviour**: a Message in the Transcript, deltas on
  the pipe, a health state, a rendered catalog. Any conformant peer satisfies these, so they run
  against the mock *and* against a real Adapter.
- **Mock-only** — asserts on **what the peer received**, through `FakeAcpAgent.Received`. A real
  Adapter has no equivalent and never will: nothing in ACP lets a client ask an agent what it was
  sent. These are not lesser tests — T-23 is still the strongest in the suite — they simply pin the
  Huddle → Adapter direction, which is a different job from proving the round trip.

| # | Test task | Kind | Asserts |
| --- | --- | --- | --- |
| T-21 | A Persona on a mock profile starts, takes a Turn, and posts a reply into a Room | **portable** | a `Message` lands in the Transcript |
| T-22 | **The composed prompt actually arrives.** The `session/new` the mock received carries the Persona text in `_meta` | mock-only | proves §6.4 end to end, not just the golden |
| T-23 | **The prefix plumbing is real.** Under an unprefixed profile the received prompt names `get_help`; under a prefixed one, `mcp__team__get_help` (O-5) | mock-only | the strongest test in the suite — it is the defect that makes a Persona look broken rather than misconfigured |
| T-24 | The received `session/new` carries `mcpServers[0]` with the loopback URL **and** an `Authorization: Bearer` header | mock-only | proves the token path against a non-Claude peer |
| T-25 | Chunks stream: several `MessageDelta` envelopes reach the pipe before `TurnCompleted` | **portable** | proves O-1's Huddle half |
| T-26 | `session/cancel` reaches the mock on Stop, and the Turn ends **stopped, not failed** | **split** | the *reached the peer* half is mock-only; the *stopped, not failed* half is portable and is the one `rules.md` cares about |
| T-27 | Disposal sends `session/close`, observed by the mock (§6.8) | mock-only | Tier 3 version of T-19 |
| T-28 | Changing the Adapter restarts, and the **new** peer receives the new prompt | **split** | the restart is portable; *"receives the new prompt"* is mock-only |
| T-29 | The Model catalog the card shows is the one the mock advertised at `session/new` (O-3) | **portable** | proves §6.5's dispatch |
| T-30 | Launched **as a process** under a profile, a Persona completes one Turn | **portable** | the only test that exercises `AgentProcessLauncher`; one spawn, kept to a single test |

Five portable (counting the portable halves of T-26 and T-28), five mock-only. **Write the
portability into the test, not into a comment**: a portable assertion that quietly reaches for
`Received` stops being portable and nothing will warn you.

### 15.9 Phase 7 — live · Tier 4 · **the only Agency-dependent work**

| # | Task | Note |
| --- | --- | --- |
| T-31 | ~~Re-point the mock profile at `agency-acp` and re-run **all of Phase 6 unchanged**~~ **Superseded — see T-31a and T-31b.** | ~~If Tier 3 is honest, this is a configuration change.~~ It was not honest, and this is where that surfaced. |
| T-31a | Make the portable half of Phase 6 runnable against a real Adapter: parameterise `MockAdapterFixture` over its launcher **and** move the portable assertions off `FakeAcpAgent.Received`. | The real work. Not a configuration change — see the amendment below. |
| T-31b | Re-point at `agency-acp` and run the **portable** half. The mock-only half stays on the mock, permanently and correctly. | Any failure here is a genuine mock-vs-contract divergence (E-18), which is still exactly what it is for. |
| T-32 | **Manual checklist**: two Personas in one Room, one per Adapter, live chunks in the browser, Stop leaves both resumable | Needs a browser, a GPU and a human. Agency's own Task 12.1 proves the ACP half against a scripted client; **neither plan contains the whole milestone**. |
| T-33 | **Manual**: does a real local model actually call `get_help`? | No test can settle it. Roadmap item 12 names it as an honest limit; a mock calls what it is scripted to call. |

> **Amendment, 2026-09-18 — why T-31 split.** T-31 assumed re-pointing was a configuration change.
> Two things make it not one, and the second was invisible when this was written.
>
> The shallow one: every Phase 6 test but T-30 is wired to `MockAdapterFixture`, which substitutes
> `IAgentProcessLauncher` with an in-proc stream pair. No configuration turns that into a launched
> process. That is fixable by parameterising the fixture, and is what
> [Live findings](Huddle.Adapters-LiveFindings.md) D-4 records.
>
> The deep one: **five of the six conformance test files assert through `FakeAcpAgent.Received`.**
> Parameterising the launcher would let them *run* against `agency-acp` and leave them with nothing
> to assert against, because ACP gives a client no way to ask an agent what it was sent. §6.10 is
> right that `Received` is what makes this tier worth its cost — and that is exactly the property
> that cannot travel. The suite's best idea and its portability claim were in tension from the
> start; nobody noticed because nothing exercised the second one until Phase 7.
>
> **The lesson, which is the reusable part:** "write once, run twice" is a claim about the
> *assertions*, not about the fixture. Deciding which tier an assertion belongs to is a design
> decision to make when writing it, not a property to discover later.

### 15.10 Sequencing

```text
Phase 0 (mock) ─┐
Phase 1 ────────┼─► Phase 3 ─┐
Phase 2 ────────┘            ├─► Phase 6 (conformance) ─► Phase 7 (live)
Phase 4 ─────────────────────┘                              ▲
Phase 5 ────────────────────────────────────────────────────┘
```

Phases 0, 1, 2, 4 and 5 may start **now and in parallel**. Phase 6 needs Phase 0 plus whichever
subsystem it exercises. **Phase 7 is the only thing gated on Agency.NET**, and it is one
configuration change plus two manual checks.

## 16. Vocabulary changes

`docs/agencyteam/language.md` is binding. Item 12 predicted two definitions would stop being
true. Checked against the design: **one widens, one narrows, and one survives** — item 12 was
wrong about which.

### Adapter — widens

> **Before:** The `claude-agent-acp` Node package under `node_modules` that actually speaks ACP.
> Not part of this solution; located by `AdapterLocator`.

> **After:** An ACP agent one Persona's session runs on — a process this solution launches and
> talks to over stdio. Three exist today: `claude-agent-acp` (a Node package under `node_modules`,
> located by `AdapterLocator`, running cloud Claude) and `agency-acp` (a .NET executable running
> a local model). Which one a Persona uses is part of the Persona. No Adapter is part of this
> solution.
> *Avoid*: agent, bridge, client, provider, backend, host.

### Effort — narrows

> **Before:** …chosen from the ladder the Adapter advertises *for that Model*. … Model-dependent
> — some Models offer none.

> **After:** …chosen from the ladder the Adapter advertises. Whether that ladder varies by Model
> is the Adapter's business: `claude-agent-acp` advertises one per Model; `agency-acp`
> advertises one per endpoint surface. Unset means the default, which is the normal case. Fixed
> for the life of a session.

### Model — survives, contrary to item 12

Item 12 asserted that *"chosen from the catalog the Adapter advertises at `session/new`"*
*"cannot survive a second backend that is a NuGet reference rather than a process and has a real
model list."* Under ACP it survives untouched: `agency-acp` **is** a process, and it **does**
advertise its catalog at `session/new`. No change.

### New term — Adapter Profile

> **Adapter Profile**
> : One configured Adapter — its stable `Id`, the command that launches it, and whether its App
> Tools are advertised to the model with the `mcp__team__` prefix. Configured under
> `Team:Acp:Adapters`; an installation that configures none gets exactly one, synthesised from
> the legacy keys. A Persona names a profile by `Id`; an unknown `Id` degrades to the first
> profile rather than failing.
> : *Avoid*: backend, host, provider, target.

### `rules.md` — one row widens

> **Before:** App Tool names must be spelled `mcp__team__*` in the system prompt.

> **After:** App Tool names must be spelled with **their Adapter's prefix** in the system prompt.
> The prefix is built in code from the Adapter Profile and the tool-server name, never typed
> into a hook template. For `claude-agent-acp` it is `mcp__team__`; for `agency-acp` it is empty,
> because `McpClientPool` surfaces a remote tool under the server's own name unmodified. A
> golden test pins both.

---

## 17. Decision log

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-1 | **One profile-aware factory** | a second `IAgentHostFactory` (item 12's plan) | Seven of eight steps are identical; duplicating them guarantees drift |
| D-2 | **Reuse `ModelCatalogProbe`** | a second `IModelCatalog` (item 12's plan) | Both Adapters advertise at `session/new`; only the cache key differs |
| D-3 | **Frontmatter `adapter:`** | a `persona_adapters` table | The Ordering warning is binding: a store is a rename cost forever |
| D-4 | **`adapter`, not `_adapter`** | the reserved `_` prefix (item 12's `_host:`) | `_` is for keys that never surface; this one has a dropdown |
| D-5 | **The term is Adapter** | Provider; Backend; Host | The concept already has a name; "Provider" collides with Agency's `IModelProvider` |
| D-6 | **Reset Model and Effort on Adapter change** | keep them; keep Model per-Adapter | A cross-Adapter model id is meaningless; per-Adapter storage is a schema change and a rename cost |
| D-7 | **Supervisor raises the resolver warning** | widen `IAgentHostFactory`'s tuple | One diagnostic string is not worth touching the fake and ~25 call sites; the resolver is pure |
| D-8 | **Withdraw the `ToolKind` map** | build it; bump `ToolActivity` | `ToolActivity` carries no kind; the ask rested on a false premise |
| D-9 | **Send `session/close` anyway** | rely on process death | Correct, two lines, and required by any adapter outliving one session |
| D-10 | **Build `Huddle.MockAdapter` first** | wait for `agency-acp`; test only against in-proc fakes | Agency's date leaves the critical path; and it adds a tier that never existed — Huddle driving a *real* ACP peer, unconditionally in CI |
| D-11 | **Link `FakeAcpAgent`'s source; do not move it** | move it to a src project; write a second mock | No file leaves the ACP effort's subtree, no `internal` widens, and there stays one implementation of what an ACP agent does |
| D-12 | ~~**Conformance tests are written once, run twice**~~ **Half wrong — amended 2026-09-18** | separate mock tests and live tests | The rejected option was closer to right than this decision was. See below. |

### Amendment to D-12, 2026-09-18

**D-12 was half wrong, and the half it got wrong is the half it was named after.**

The decision claimed every conformance test would be written once and run twice — against
`Huddle.MockAdapter` in CI, and against `agency-acp` by changing one Adapter Profile. Phase 7
showed that **five of the six conformance test files assert through `FakeAcpAgent.Received`**,
which no real Adapter can provide: ACP has no way for a client to ask an agent what it was sent.
Those tests cannot be re-pointed at any price, and no amount of fixture parameterisation changes
that — they would run and have nothing to assert.

**What stands.** Splitting mock tests from live tests *would* have been worse. The rejected option
assumed two suites testing two things; the truth is one suite testing two **directions**, and
mixing them in one file with one fixture is still right. The mock-only tests are also the strongest
ones — T-23 is the defect that makes a Persona look broken rather than misconfigured — so their
non-portability is a property of what they prove, not a weakness.

**What changes.** The claim narrows from *every test* to *the portable half*, and the split becomes
an explicit property of each assertion rather than an emergent accident. §15.8 now marks every test
`portable`, `mock-only` or `split`. T-31 becomes T-31a (make the portable half portable) and T-31b
(run it).

**Why it survived this long unchallenged.** Nothing exercised it. D-12 is a claim about Phase 7,
Phase 7 was the last phase, and every phase before it passed against the mock exactly as designed.
The claim was load-bearing for the plan's schedule and was never once tested — which is the same
shape as the two defects this project found on the other side of the wire, where a test asserted
the layer above the one that breaks. Three instances now, across two codebases. Worth its own ADR
if it happens a fourth time.

### Candidate ADRs

Three meet all three bars — hard to reverse, surprising without context, a real trade-off:

- **ADR: An Adapter is a property of the Persona.** Establishes that one installation runs
  Teammates on different Adapters simultaneously, and that the choice lives in frontmatter
  rather than a store. Supersedes roadmap item 12's plan.
- **ADR: The tool-name prefix belongs to the Adapter.** `mcp__team__` stops being a constant and
  becomes a profile property. Reversing it would require re-pinning every golden.
- **ADR: Model and Effort reset when the Adapter changes.** A deliberate data loss on an edit,
  justified by cross-Adapter ids being meaningless.

D-1, D-2, D-7 and D-9 do not meet the bar: each is cheaply reversible and unsurprising in
context.
