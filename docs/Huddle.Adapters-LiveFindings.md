# Huddle.Adapters — live findings against `agency-acp`

**Date:** 2026-09-16 · **Status:** findings, not decisions · **Supersedes nothing**

The written report [Task 12.1](Huddle.Adapters-ProjectPlan.md) asks for in place of a green run:
*"D10 green against `agency-acp`, **or a written report naming each divergence**."* This is that
report.

[Spec §12, **E-18**](Huddle.Adapters-Specifications.md) predicted this outcome and rated it **by
design**: *"Mock and contract diverge | Tier 3 green, Phase 7 red | T-31 is where it surfaces, and
finding it is the point."* It surfaced exactly there. Nothing below is a defect in Huddle's
Adapters work; all of it is contract divergence between `Huddle.MockAdapter` and the real
`agency-acp`, which is what Phase 7 exists to expose.

**What was run.** `E:\Repos\Agency\src\Acp\Agency.Acp\bin\Release\net10.0\Agency.Acp.exe`
(`0.1.193+bceba00e5f`), driven directly over stdio with raw newline-delimited JSON-RPC.
`E:\Repos\Agency` was read and executed, never modified.

---

## D-1 — Persona identity never reaches the agent · **blocking**

**Huddle sends** the composed system prompt on `session/new` at `_meta.systemPrompt` — a raw
string for `SystemPromptMode.Replace`, or `{"append": "…"}` for `Append`
(`src/Huddle.Acp/DotAcp/DotAcpAgentHost.cs`).

**`agency-acp` never reads it.** `MethodDispatcher.HandleSessionNewAsync` reads only
`_meta.model`. A repo-wide search of `E:\Repos\Agency\src\Acp` for `systemPrompt` /
`SystemPrompt` / `IdentityPrompt` returns **zero matches**.

**Confirmed live:** a `session/new` carrying `_meta.systemPrompt: "You are Nova…"` **succeeded**,
with no error and no acknowledgement. The field was silently dropped.

**Consequence.** Every Persona routed through a real `agency-acp` runs on the harness's own
baseline prompt, not its own text. No error is raised anywhere. Two Personas on that Adapter
would be indistinguishable from each other — which is the one thing the whole feature exists to
make possible. **This blocks the joint milestone**; nothing else in this report matters until it
is resolved.

**Note for whoever picks it up.** Agency's own spec §6.9 (D-3) mentions
`QueryContext.IdentityPrompt` as *"re-land; one line in `SystemPromptBuilder`"*, so the
harness-side plumbing may already exist and simply not be invoked from `HandleSessionNewAsync`.
Worth checking before designing anything new. **Which side changes is Agency's call, not
Huddle's** — Huddle is sending the field ACP defines for this, and `claude-agent-acp` reads it.

---

## D-2 — The build ships with no configuration, so `session/new` hard-fails

On a vanilla build, `session/new` returns:

```json
{"jsonrpc":"2.0","id":2,"error":{"code":-32603,"data":null,"message":"Agent:DefaultModel is not configured."}}
```

`Agency.Acp`'s `bin/Release/net10.0/` ships neither `appsettings.json` nor
`shared-appsettings.json`, and its `.csproj` has no content-copy items — unlike
`Agency.Harness.Console`, which ships both.

This is **not** the documented fail-soft behaviour. Agency's §8.1 makes the catalogue fetch and
the MCP connect fail-soft; `DefaultModel` is a hard throw, so "`session/new` is never an error"
does not hold end to end. Huddle's **P4** ("degrade, never reject") assumes the far side starts.

Supplying `Agent__DefaultModel` / `Agent__DefaultClientName` / `Agent__LLmClients__*` as
environment variables makes it succeed. `AgentProcessOptions.EnvironmentOverrides` — the fourth
slot [Spec §6.3](Huddle.Adapters-Specifications.md) notes is *"set by nobody today"* — is the
natural Huddle-side place to carry them if that becomes the answer.

---

## D-3 — `session/prompt` does not fail fast on an unreachable endpoint

Against a deliberately dead endpoint (`http://127.0.0.1:1/v1`), `session/prompt` produced **no
response for 30 seconds** — no error, no partial notification, no exit. The process had to be
force-killed.

Agency's latency budgets cover `initialize` (<50 ms), `session/new` (<500 ms) and a **cold model**
(10–60 s, *"physics, not a defect"*). They say nothing about an **unreachable** endpoint, and the
two are indistinguishable from Huddle's side.

**Consequence for Huddle.** A Persona on a temporarily-unreachable `agency-acp` reads as *hung*
rather than *Degraded*. Use case **U-3** assumes "the first Turn fails; the Teammate goes
Degraded" — that assumption depends on a failure arriving, and here none does. Whether
`PersonaRunner` needs its own client-side turn timeout is a **Huddle-side** question this raises
and does not answer.

---

## D-4 — "Re-run every D10 test unchanged" is not achievable as written · *planning gap, ours*

Task 12.1 says to re-point the conformance suite and **change no test**. That is structurally
impossible: every D10 test except `ProcessModeTests` is wired to `MockAdapterFixture`, which
substitutes `IAgentProcessLauncher` with an in-proc `FullDuplexStream` pair. No configuration
turns that into a real launched process.

This is a **gap in our own plan**, not an Agency defect. [Spec §17, D-12](Huddle.Adapters-Specifications.md)
claims *"conformance tests are written once, run twice"*, and the decomposition into
`MockAdapterFixture` quietly gave that up. Making it true would mean parameterising the fixture
over its launcher — worth doing, and not attempted here.

---

## D-5 — Narrow exception handling in the dispatcher · *flagged, unconfirmed*

`MethodDispatcher.DispatchAsync` catches only `AcpJsonRpcException` and `JsonException` around a
handler call. Any other exception a handler throws — for example `IAgentFactory.CreateAgent`
failing because `DefaultClientName` matches no configured client — would propagate uncaught
rather than becoming the JSON-RPC error Agency's **P6** promises. **Not exercised in this spike**;
flagged from reading, not observed.

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

## Next, in priority order

1. **Resolve D-1.** Nothing else matters until a Persona's identity reaches the agent.
2. **Resolve D-2**, or agree that Huddle supplies configuration through
   `AgentProcessOptions.EnvironmentOverrides`.
3. **Get a reachable endpoint**, then characterise D-3 precisely — is it an HTTP timeout, a retry
   loop, or no timeout at all? — and decide whether Huddle needs its own turn timeout regardless.
4. **Then** close D-4 by parameterising `MockAdapterFixture` over its launcher, so the
   conformance suite really is written once and run twice.
