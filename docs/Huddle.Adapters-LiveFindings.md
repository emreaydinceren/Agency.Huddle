# Huddle.Adapters — live findings against `agency-acp`

**Date:** 2026-09-17 · **Status:** findings, not decisions · **Supersedes:** the 2026-09-16
revision of this file, which was written against `0.1.193+bceba00e5f`

The written report [Task 12.1](Huddle.Adapters-ProjectPlan.md) asks for in place of a green run:
*"D10 green against `agency-acp`, **or a written report naming each divergence**."* This is that
report, re-checked against the published **`AgencyDotNet.Acp 0.1.195-ga1fc165f21`**.

[Spec §12, **E-18**](Huddle.Adapters-Specifications.md) predicted this outcome and rated it **by
design**: *"Mock and contract diverge | Tier 3 green, Phase 7 red | T-31 is where it surfaces, and
finding it is the point."* It surfaced exactly there. Nothing below is a defect in Huddle's
Adapters work; all of it is contract divergence between `Huddle.MockAdapter` and the real
`agency-acp`, which is what Phase 7 exists to expose.

**What was checked, and how.** The first revision drove
`E:\Repos\Agency\src\Acp\Agency.Acp\bin\Release\net10.0\Agency.Acp.exe` (`0.1.193+bceba00e5f`)
over stdio with raw newline-delimited JSON-RPC. This revision is a **source check against the
tree that shipped**, which matters because the local build is older than the package: Agency's
`origin/main` is `a1fc165`, matching the `-ga1fc165f21` suffix on 0.1.195, and
`git diff HEAD origin/main -- src/Acp/` is empty. So the code read below **is** 0.1.195, not a
stale checkout. `E:\Repos\Agency` was read, never modified.

---

## What 0.1.195 settled at no cost to Huddle

Agency's announcement named several contract facts as departures from the design doc. Each was
checked against Huddle's code. **None needed a change**, and that is worth recording so the next
person does not re-litigate it.

| Their statement | Huddle's behaviour | Where |
| --- | --- | --- |
| `session/new` returns `configOptions`, **not** `models[]`/`effortLevels[]`, and there is no `AgentModelOption` in `dotacp.protocol` | Already reads `configOptions`, and handles **both** union branches — a flat option array and grouped options | `ModelConfigOptions.cs:23-33`, `SessionConfigSelects.cs:64-74` |
| `usage.used` is context-window **occupancy**, not a running bill; it falls when history is trimmed — "sum only the rises" | Sums only the rises; a drop contributes zero | `PersonaRunner.cs:564-577`, pinned by `TokenBudget_IgnoresDropsInUsed` |
| `totalCostUsd` is structurally `0` — do not build budget UI on it | No cost identifier exists anywhere in `src/`. The Budget is expressed purely in tokens | `AcpOptions.TokenBudget` |
| Stop reasons are `end_turn`, `max_turn_requests`, `max_tokens`, `cancelled`; a harness error is a JSON-RPC error, never a silent `end_turn` | All four mapped. An unrecognised reason **throws** rather than falling through to `EndTurn`, and surfaces as a failed Turn | `SessionUpdateMapper.cs:128-145` |
| A model's `description` carries residency and is `null` when unknown — "render null as an absent field, not as *not loaded*" | Satisfied, if vacuously: `Description` is carried end to end and rendered by **no** component. No placeholder, no `?? "…"` anywhere | zero `Description` matches across every `.razor` |
| The effort option is **omitted entirely** when the backend supports neither thinking dialect — "handle its absence" | An empty ladder already means *unset / default* | `EffortConfigOptions`, `WithoutAdapterDefault` |
| Tool names pass through unmodified; the adapter mints no `mcp__server__` prefix | `UsesToolNamePrefix: false` on the profile, pinned by a second golden | [ADR-0014](adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md) |
| `session/close` is optional | Huddle sends it anyway — Spec §6.8 | `DotAcpAgentSession.cs:183-230` |

Their four client-side prerequisites — host profiles, the per-backend tool-name prefix, a
per-host model catalogue, and `session/close` on dispose — **all landed** in PR #59.

---

## D-1 — Persona identity never reaches the agent · **blocking** · *stands*

**Huddle sends** the composed system prompt on `session/new` at `_meta.systemPrompt` — a raw
string for `SystemPromptMode.Replace`, or `{"append": "…"}` for `Append`
(`src/Huddle.Acp/DotAcp/DotAcpAgentHost.cs:124-143`).

**`agency-acp` still never reads it.** In the shipped tree, `MethodDispatcher.HandleSessionNewAsync`
reads only `_meta.model` (`Dispatch/MethodDispatcher.cs:158`). A search of `src/Acp` for
`systemPrompt` / `SystemPrompt` / `IdentityPrompt` returns **zero matches**.

**The two sides are exactly crossed.** This is the part the first revision could not yet see:

| Field | Huddle sends | `agency-acp` reads |
| --- | --- | --- |
| `_meta.systemPrompt` | **yes** — the composed Persona prompt | no |
| `_meta.model` | **no** | yes |

Huddle sends no `_meta.model` at all, and should not: `request.Meta` is assigned exactly once,
with a single `systemPrompt` key, and a Model is selected **after** the session opens with
`session/set_config_option` — which is the path Agency's own announcement documents as canonical.
Their speculative `_meta.model` reader therefore has nothing to read, and is dead code.

**Consequence.** Every Persona routed through a real `agency-acp` runs on the harness's baseline
prompt, not its own text. No error is raised anywhere. Two Personas on that Adapter would be
indistinguishable from each other — which is the one thing the whole feature exists to make
possible. **This blocks the joint milestone**; nothing else in this report matters until it is
resolved.

**Note for whoever picks it up.** The harness-side plumbing already exists and works:
`QueryContext.IdentityPrompt` (`src/Harness/Agency.Harness/Contexts/QueryContext.cs:18`) is
consumed by `SystemPromptBuilder.cs:25` and covered by four passing tests in
`SystemPromptIdentityTests`. Nothing in `src/Acp` ever populates it — there is not one
`new QueryContext` in that subtree. **Which side changes is Agency's call, not Huddle's** —
Huddle is sending the field ACP defines for this, and `claude-agent-acp` reads it.

**It also blocks a manual test.** `ADAPTERS-04` asks whether a real local Model calls `get_help`
unprompted. The prompt naming `get_help` is the one that never arrives, so that test would return
INCONCLUSIVE every time while appearing to be evidence about the Model. It is marked blocked
rather than merely unrun.

---

## D-2 — The build ships with no configuration, so `session/new` hard-fails · *stands; answered on Huddle's side*

On a vanilla build, `session/new` returns:

```json
{"jsonrpc":"2.0","id":2,"error":{"code":-32603,"data":null,"message":"Agent:DefaultModel is not configured."}}
```

**Unchanged in 0.1.195.** `src/Acp/Agency.Acp/Agency.Acp.csproj` still declares no content-copy
items and its build output holds no `appsettings.json` — only `deps.json` and `runtimeconfig.json`
— unlike `Agency.Harness.Console`, which ships both.

This is **not** the documented fail-soft behaviour. Agency's §8.1 makes the catalogue fetch and
the MCP connect fail-soft; `DefaultModel` is a hard throw, so "`session/new` is never an error"
does not hold end to end. Huddle's **P4** ("degrade, never reject") assumes the far side starts.

**Huddle-side answer, delivered 2026-09-17.** Supplying `Agent__DefaultModel` /
`Agent__DefaultClientName` / `Agent__LLmClients__*` as environment variables makes it succeed, and
that is a *supported* path rather than a workaround: `Agency.Acp.csproj` references
`Microsoft.Extensions.Configuration.EnvironmentVariables` deliberately. An Adapter Profile now
carries `EnvironmentOverrides`, so a configured `agency` profile can launch without Agency
shipping anything — closing the half of D-2 that was ours. `AgentProcessOptions.EnvironmentOverrides`
— the fourth slot [Spec §6.3](Huddle.Adapters-Specifications.md) called *"set by nobody today"* —
is now set by the catalog.

The **Model-catalogue probe is the first thing this unblocks**, before any Turn is taken: the
probe does exactly `initialize` + `session/new`, which is where the hard throw lives.

> **Trap, recorded because it is silent.** These keys must be set from `appsettings.json`, not
> through the environment-variable configuration provider. An environment variable name contains
> `__`, and that provider rewrites every `__` into `:` — so
> `Team__Acp__Adapters__0__EnvironmentOverrides__Agent__DefaultModel` binds as the key
> `Agent:DefaultModel`, which no process will ever read.

---

## D-3 — `session/prompt` does not fail fast on an unreachable endpoint · *explained; answered on Huddle's side*

Against a deliberately dead endpoint (`http://127.0.0.1:1/v1`), `session/prompt` produced **no
response for 30 seconds** — no error, no partial notification, no exit. The process had to be
force-killed.

**0.1.195 explains why, and it is a consequence of D-2 rather than a separate defect.** The
harness *does* have a per-turn timeout — `AgentOptions.TurnTimeoutSeconds`, applied in
`Agency.Harness/Agent.cs:238-254` and surfaced as a distinct `TimeoutException` so a supervisor
can tell "the model hung" from "the human hit Stop". But it is `int?` with **no default**, and it
is applied only `when timeout is > 0`. Since the adapter ships no configuration at all (D-2), it
is never set. The mechanism was not missing; it was unconfigured.

Two Huddle-side consequences, both now acted on:

1. **`EnvironmentOverrides` can carry `Agent__TurnTimeoutSeconds`** alongside the rest, so the far
   side's own bound can be switched on from Huddle's configuration.
2. **A client-side bound was added anyway** — `Acp:TurnIdleTimeoutSeconds`, an *idle* bound that
   any Adapter event restarts. It is not redundant with (1), because of **D-5**: a handler fault
   produces no response at all, and the far side's turn timeout lives *inside* the handler that
   already threw. That route cannot be closed from configuration, on either side, and it is the
   reason the client-side bound is necessary rather than defensive.

Use case **U-3** assumes "the first Turn fails; the Teammate goes Degraded" — that assumption
depends on a failure arriving. It now does, from our side, within the bound.

**Still open on Agency's side:** whether an unreachable endpoint has an HTTP-level timeout at all,
and what `TurnTimeoutSeconds` ought to default to for an adapter that ships no configuration.

---

## D-4 — "Re-run every D10 test unchanged" is not achievable as written · *planning gap, ours; still open*

Task 12.1 says to re-point the conformance suite and **change no test**. That is structurally
impossible: every D10 test except `ProcessModeTests` is wired to `MockAdapterFixture`, which
substitutes `IAgentProcessLauncher` with an in-proc `FullDuplexStream` pair. No configuration
turns that into a real launched process.

This is a **gap in our own plan**, not an Agency defect. [Spec §17, D-12](Huddle.Adapters-Specifications.md)
claims *"conformance tests are written once, run twice"*, and the decomposition into
`MockAdapterFixture` quietly gave that up. Making it true would mean parameterising the fixture
over its launcher — worth doing, **and still not attempted**. It is now recorded in
[Testing](agencyteam/testing.md) beside the claim it qualifies, so the gap is visible where
someone would otherwise trust the claim.

---

## D-5 — Narrow exception handling in the dispatcher · **confirmed by reading** *(was: flagged, unconfirmed)*

`MethodDispatcher.DispatchAsync` catches only `AcpJsonRpcException` and `JsonException` around a
handler call (`Dispatch/MethodDispatcher.cs:102-114`). The first revision flagged this from
reading and did not exercise it. Reading the **transport** settles what happens next, and it is
worse than "propagates uncaught":

```csharp
catch (Exception)
{
    // A handler fault must not take down the reader loop or the process; the handler is
    // responsible for translating its own failures into JSON-RPC error responses.
}
```

`StdioTransport.InvokeHandlerAsync` swallows everything. But the handler is *not* responsible for
that translation — `DispatchAsync` is, and it covers only two exception types. So an unexpected
handler fault — for example `IAgentFactory.CreateAgent` failing because `DefaultClientName`
matches no configured client — yields **no error response, no crash, and no reply at all**.

From Huddle's side that is indistinguishable from D-3: a request that never returns. It is the
**third** route to a hung Turn, and the only one that **cannot** be closed by configuring
anything, on either side — which is the argument for Huddle's own idle bound.

Still not exercised live; confirmed from source rather than observed.

---

## What did work

`initialize` matched Agency's spec exactly, with zero configuration:

```json
{"agentInfo":{"name":"agency-acp","version":"0.1.193+bceba00e5f"},
 "agentCapabilities":{"loadSession":false},"authMethods":[],"protocolVersion":1}
```

With configuration supplied, `session/new` also succeeded against a **Huddle-shaped** request —
one `mcpServers` entry at a `127.0.0.1` URL with a `Bearer` header — and returned a session id
plus a `configOptions` catalogue carrying both `model` and `effort`. An unreachable MCP server was
recorded as a failure rather than thrown, exactly as designed. So the **transport, the framing,
the capability handshake and the tool-server envelope all agree**. The divergence is in what the
far side *reads*, not in how the two talk.

Agency's own announcement adds, from their side, that an authenticated MCP server with a
per-session bearer token is covered by an E2E test, that no filesystem or shell tools are ever
registered, and that a cancel mid-tool-batch leaves the next Turn on that session working. None of
that contradicts anything observed here.

---

## Reproducing this

`tests/Huddle.Acp.Tests/E2E/RealAgencyAcpTests.cs` carries two tests, **skipped by default** and
gated on `HUDDLE_AGENCY_ACP=1`, copying the `TEAM_E2E` idiom already used by `RealAdapterTests`.
Override the executable with `HUDDLE_AGENCY_ACP_EXE`. They deliberately stop before
`session/prompt` — see **D-3** for why.

No live inference endpoint was reachable from this machine: the configured
`http://inference-host.example:1234` does not resolve (`Non-existent domain`). No
attempt was made to install or start one. The host above is a placeholder — a real
internal `*.local` name in a tracked file fails the `secret-scan` job on
`.gitleaks.toml`'s `internal-mdns-host` rule, which is why it is not written here;
see `agents/CIPipeline.md`.

**The local Agency build is older than the package.** It is `0.1.193+bceba00e5f`, built
2026-09-16. Rebuild from `origin/main` before re-running anything here, or the E2E tests exercise
a version that is no longer what ships.

## Next, in priority order

1. **Resolve D-1.** Nothing else matters until a Persona's identity reaches the agent. It is the
   only item still blocking the joint milestone, and the only one with no Huddle-side mitigation.
2. ~~**Resolve D-2**, or agree that Huddle supplies configuration through
   `AgentProcessOptions.EnvironmentOverrides`.~~ **Answered on Huddle's side 2026-09-17** — an
   Adapter Profile carries `EnvironmentOverrides`. Agency shipping a default `appsettings.json`
   would still be the better fix for anyone not driving it from Huddle.
3. ~~**Get a reachable endpoint**, then characterise D-3 precisely.~~ **Explained** — the far
   side's turn timeout exists but is unconfigured, and Huddle now has its own idle bound. What
   remains is whether an unreachable endpoint has an HTTP-level timeout at all, which still needs
   a reachable endpoint to answer.
4. **Close D-4** by parameterising `MockAdapterFixture` over its launcher, so the conformance
   suite really is written once and run twice. Unchanged, and now the largest piece of our own
   debt in this area.
5. **Raise D-5 with Agency.** Confirmed by reading; a general `catch` in `DispatchAsync` that
   turns any handler fault into a JSON-RPC error would close it, and their **P6** already promises
   that behaviour.
