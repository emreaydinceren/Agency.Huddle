# Adapters: choosing one per Persona, and running two at once

Prove, in a real browser, that the **Adapter** a Persona runs on is a per-Persona choice rather than an installation setting: that the Adapter select appears only when more than one Adapter Profile is configured, that two Personas on *different* Adapter Profiles can hold one Room between them, that a scripted Adapter's reply lands whole in the Room and the Transcript, and that changing a Persona's Adapter resets its choices and restarts it. One test here can be settled by no automated test at all and is the reason this area exists: whether a real local Model actually calls `get_help` unprompted.

**4 tests** · 3 free, 1 paid 💰 · about 1.5 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

Background, if you need it: [ADR-0013](../../adr/0013-an-adapter-is-a-property-of-the-persona.md)
(an Adapter is a property of the Persona), [ADR-0014](../../adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md)
(the tool-name prefix follows the Adapter) and the
[Adapters design](../../Huddle.Adapters-Specifications.md).

## Setup

Run [`P-BUILD`](common.md#p-build) then the **RESTART LANE** described in
[Model and Effort](model-effort.md) — agent runners must actually start for anything here to
mean anything. You can skip that lane's Haiku step for the Chief of Staff: here the default
Adapter is `mock`, so the Chief of Staff's Greeting Turn is answered by the free mock
(`PersonaRunner.cs:481-495`). This area adds:

1. **At least two Adapter Profiles must be configured**, or the Adapter select does not render at
   all and three of these four tests are unrunnable (`AgentProfileCard.razor:310`). That is correct
   behaviour, not a defect: a stock installation configures no `Team:Acp:Adapters` and gets exactly
   one synthesised profile, and a dropdown offering one option is a control nobody can use. Add to
   `appsettings.Development.json`, writing `<repo>` as the absolute clone path with forward slashes:

   ```jsonc
   "Team": { "Acp": { "Enabled": true, "Adapters": [
     { "Id": "mock",   "DisplayName": "Mock",   "Description": "a scripted adapter - no model",
       "Command": "<repo>/src/Huddle.MockAdapter/bin/Debug/net11.0/mock-acp.exe",
       "Args": ["--mock"], "UsesToolNamePrefix": false },
     { "Id": "mock2",  "DisplayName": "Mock 2", "Description": "a second scripted adapter - no model",
       "Command": "<repo>/src/Huddle.MockAdapter/bin/Debug/net11.0/mock-acp.exe",
       "Args": ["--mock", "--second"], "UsesToolNamePrefix": false },
     { "Id": "claude", "DisplayName": "Claude", "Description": "cloud, spends money per turn",
       "Command": "node", "UsesToolNamePrefix": true, "IsolateUserSettings": true }
   ] } }
   ```

   The **first** profile in the list is the default, so a Persona with no `adapter:` field, and the
   Chief of Staff, run on `mock` and spend nothing (`AdapterCatalog.Default` is `Profiles[0]`).
   `mock-acp` is built by this solution's own `dotnet build Huddle.slnx` — it needs no Node, no
   subscription, no GPU and no money.

   **The `Args` on the mock profiles are required, not decoration.** A profile with a `Command`
   and neither `Args` nor `AdapterPath` and `UsesToolNamePrefix: false` yields no launch options
   (`AgentProcessOptionsFactory.cs:26-47`), and the Persona goes Offline with `No ACP adapter is
   installed for Persona '<Name>' on Adapter 'mock'` (`DotAcpAgentHostFactory.cs:138-141`). The
   mock ignores the value (`Program.cs:25` reads only `--script`), which is also why `mock2` can
   differ from `mock` by an extra argument: that argument is what tells their processes apart in
   `O-MOCK-ADAPTERS`. `IsolateUserSettings` is `true` on `claude` because a configured profile
   defaults it to `false` while the stock synthesised profile is `true`
   (`AdapterProfileOptions.cs:65`); without it, adding an `Adapters` list silently changes how the
   stock Claude Persona launches.

2. For **ADAPTERS-04 only**, a third profile pointing at a real local Adapter (`agency-acp` or
   equivalent) and a running inference endpoint. That test is the one no automated test can
   settle; everything else here runs against `mock-acp`. ADAPTERS-04 was unblocked on 2026-09-18;
   read the note on the test itself for which versions make it meaningful.

   **Take the Adapter from nuget.org, never from a local build or a drop folder.**
   `AgencyDotNet.Acp` publishes there from the official build; the current public version is
   `0.1.198-ga453511f0e`. Materialise it with a throwaway project rather than unzipping the nupkg —
   the host needs its transitive dependencies beside it:

   ```powershell
   dotnet new console -o C:/tools/agency-acp
   dotnet add C:/tools/agency-acp package AgencyDotNet.Acp --version 0.1.198-ga453511f0e
   dotnet build C:/tools/agency-acp -r win-x64 --self-contained false
   # NuGet does not copy this out of lib/, and without it the host tries to run self-contained
   # and reports a missing hostpolicy.dll:
   copy "$env:USERPROFILE/.nuget/packages/agencydotnet.acp/0.1.198-ga453511f0e/lib/net10.0/Agency.Acp.runtimeconfig.json" `
        C:/tools/agency-acp/bin/Debug/net10.0/win-x64/
   ```

   **The Windows RID is not optional.** Without it the generic host's default EventLog provider
   resolves the non-Windows `System.Diagnostics.EventLog` stub and the process dies on
   `PlatformNotSupportedException` before writing one byte of protocol — which reads as a broken
   package rather than a restore that is missing Windows assets. Confirm the build first:
   `echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1,"clientCapabilities":{}}}' | dotnet <path>/Agency.Acp.dll`
   answers with `agentInfo.version`, and that is the only trustworthy way to identify what you are
   about to run.

   **The nupkg carries no `appsettings.json`** — the repository build output does, the package does
   not — so `session/new` hard-fails with *"Agent:DefaultModel is not configured"* unless the
   profile supplies its configuration. That is what `EnvironmentOverrides` is for:

   ```jsonc
   { "Id": "agency", "DisplayName": "Agency", "Command": "dotnet",
     "Args": ["C:/tools/agency-acp/bin/Debug/net10.0/win-x64/Agency.Acp.dll"],
     "UsesToolNamePrefix": false,
     "EnvironmentOverrides": {
       "Agent__DefaultModel": "google/gemma-4-e2b",
       "Agent__DefaultClientName": "local",
       "Agent__TurnTimeoutSeconds": "180"
     } }
   ```

   > [!WARNING]
   > Set these in `appsettings.Development.json`, **never** as real environment variables. The
   > environment-variable configuration provider rewrites every `__` into `:`, so
   > `Team__Acp__Adapters__2__EnvironmentOverrides__Agent__DefaultModel` binds as the key
   > `Agent:DefaultModel` — which no process reads. The symptom is the hard-fail above, with a
   > profile that looks correctly configured.

3. A Persona's Adapter is written in its frontmatter as `adapter: <id>` and travels with the file.
   Confirm with `O-FILE` rather than `O-DB` — there is deliberately **no** `persona_adapters`
   table, and looking for one is how you conclude wrongly.

## Tests

### ADAPTERS-01 — Two Personas in one Room on different Adapters, and neither Room nor Gate can tell

**Free** · about 25 min

*Proves the load-bearing claim of the whole feature: the Adapter is resolved before the session exists, so the Room, the Reply Gate, the Budget and the Transcript never learn which Adapter answered. Two Agents in one Room on two different Adapter Profiles must behave as two ordinary Agents. Both are mock profiles on purpose: Mentioning a Persona on the `claude` profile takes a real cloud Turn, and the mock's echo carries `@<name>` text back, so a cloud Persona in this Room would be woken and billed. The cloud-versus-local boundary is not what this test proves.*

**Before you start**

- Restart lane, three profiles configured (setup step 1).
- Two Personas exist: **Wren** with **no** `adapter:` line (so it runs on the default, `mock`) and **Rook** with `adapter: mock2`. Neither is on `claude`.
- Both read **Online** on their avatars in the Team's **Members** tab.

**Steps**

1. Open the Edit card for Rook. Confirm an **Adapter** select is present, above **Model**, and that its closed value reads `Mock 2`. Open Wren's Edit card and confirm its value reads `Use the default adapter`.
2. Close each card without saving. Confirm with `O-FILE` that `<data>\Agents\Rook\Rook.md` still carries `adapter: 'mock2'` in its frontmatter and that Wren's file has no `adapter:` line, and that no `persona_adapters` table exists (`O-DB`).
3. Run `O-MOCK-ADAPTERS`, then run `Get-CimInstance Win32_Process -Filter "Name='mock-acp.exe'" | Select-Object ProcessId, CommandLine`. Note that at least one command line ends `--mock` and at least one ends `--mock --second`.
4. Create a Room containing the Human, Wren and Rook.
5. Post one Message Mentioning **neither** of them. Wait 15 seconds.
6. Read the Room's budget note under the composer.
7. Post one Message Mentioning **both** by name.
8. Watch the Room, then read the budget note again.

**Pass if — all of these**

- In step 3 both a `--mock` and a `--mock --second` process exist, proving the two Personas really are on different Adapter Profiles and not silently both on the default.
- In step 5 nothing replies and no Draft appears: a three-Member Room is Mention-gated (F11, `ReplyGate.cs`), and a Message Mentioning neither wakes neither. No budget note is shown after it (the note renders only once at least one agent reply has been counted, `Chat.razor:160-164`).
- In step 8 both Agents reply in the same Room, in the same Transcript, rendered identically — nothing in the Room view distinguishes which Adapter answered.
- The budget note then reads `N of M agent replies since you last spoke.` with `N` at least 2: both replies count against the one allowance. `N` may be higher, because the mock echoes your `@` text back and can wake the other; that is the budget doing its job, not a failure.

**Fail if — any of these**

- Step 3 shows only one kind of command line → the Adapter is not per-Persona; something is reading a global setting.
- Either Agent reads Degraded with a message naming an unconfigured Adapter (`The Adapter 'x' is not configured; running on 'mock'.`, `AdapterProfileResolver.cs:39`) → the `Id` in the file does not match the `Id` in configuration (they are compared case-insensitively, so this is a spelling difference, not a casing one). With `mock` as the default this costs nothing, but the test then proves nothing.
- Either reply renders differently from the other in the Transcript → something downstream of `PersonaRunner` has learned which Adapter answered, which the design forbids.
- The Message in step 5 wakes either Agent → the Reply Gate is not treating the Room as Mention-gated.

**Inconclusive if**

Wren or Rook stays Offline with `No ACP adapter is installed for Persona '<Name>' on Adapter '<id>'` → the profile's `Args` or `Command` is wrong (setup step 1); fix it and rerun. Nothing in this test needs Node or the cloud.

---

### ADAPTERS-02 — A mock Adapter's reply lands whole in the Room and the Transcript, and the other Agent is unaffected

**Free** · about 10 min

*Proves that a reply from a scripted Adapter is persisted as one complete Message that matches what the Room rendered, and that one Agent's Turn does not disturb the other's. What this test cannot prove is incremental streaming or Stop: the mock sends its three chunks back to back with no delay (`MockBehaviour.cs:45-75`; latency injection is deferred to V2, Spec §6.10), so a human cannot see a Draft grow or click Stop mid-Turn. Both are unobservable here, not passed. They are exercised by the automated tests, and by any paid test that watches a real Draft.*

**Before you start**

- ADAPTERS-01 passed, and its Room still exists with Wren and Rook in it.
- `O-LOG` visible in `T-A`.

**Steps**

1. Post a Message Mentioning **only** Rook, with a distinctive sentence of a dozen words (for example "violet harbour lantern seven quiet rivers ahead of the morning tide").
2. Once Rook's reply has settled, compare what the Room shows with the Transcript (`O-TRANSCRIPT`).
3. Post another Message Mentioning **both** Personas.
4. Check the two Agents' status badges on the **Members** tab roster and the Room's health strip.

**Pass if — all of these**

- Rook's reply contains your sentence (the mock echoes the whole prompt, `P-LAUNCH-UAT`: judge "arrived and contains", never an exact body), and it is **one** Message in the Transcript whose text matches what the Room rendered.
- Wren did not reply to step 1's Message: it Mentioned only Rook.
- In step 3 both Agents answer.
- After step 3 no health state is raised anywhere, no `role="alert"` appears, and both tiles stay Online.

**Fail if — any of these**

- The persisted Message is **shorter** than what was rendered → this is the diagnosed dispatch-ordering race in [Known limits](../known-limits.md); record it as that known bug, and note the exact text lost. **Do not** file it as new.
- Wren answers step 1's Message → Mention gating is not per-recipient.
- Either tile leaves Online or any `role="alert"` appears after a normal reply.

**Inconclusive if**

You see a Draft row appear or not appear for a split second. Do not judge streaming on the mock: record at most "a Draft was / was not visible" as an observation.

---

### ADAPTERS-03 — Changing an Agent's Adapter resets Model and Effort, says so, and restarts the session

**Free** · about 10 min · *no Message is ever sent to the `claude` Persona, so no cloud Turn is taken*

*Proves the cascade and the consequence together: an Adapter change clears Model and Effort with an inline note, and saving restarts the session on the new Adapter. See [ADR-0015](../../adr/0015-model-and-effort-reset-when-the-adapter-changes.md). Not asserted here: that the restart "loses the memory". A new session's first Turn carries the last 20 Messages of the Room (F8, ADR-0024), and the mock echoes its whole prompt, so an Agent that "remembers" a word after the switch proves nothing either way.*

**Before you start**

- Restart lane, three profiles configured (setup step 1), and the Node adapter installed (`tools/acp/install.ps1`), so a `claude` Persona can start. Starting a session is not a prompt Turn and spends nothing.
- A Persona **Lark** on the `claude` Adapter (`adapter: claude` in `<data>\Agents\Lark\Lark.md`) with a **Model and an Effort explicitly chosen** (Haiku and `low`, per the convention) and Online.
- Do **not** post any Message to Lark's Rooms before or during this test.
- `O-ADAPTERS` prints `1` and note the `O-MOCK-ADAPTERS` count.

**Steps**

1. Open Lark's Edit card. Note the current Model and Effort.
2. Change **Adapter** to `Mock`.
3. Read the card without saving.
4. Save. Watch Lark's status badge on the **Members** tab roster, then run `O-ADAPTERS` and `O-MOCK-ADAPTERS` again.
5. Open `O-FILE` on `<data>\Agents\Lark\Lark.md`, and with `O-DB` select the rows for `Lark` from `persona_models` and `persona_efforts` (`PersonaModelStore.cs:26`, `PersonaEffortStore.cs:28`).

**Pass if — all of these**

- In step 3 both **Model** and **Effort** have cleared to their blank defaults, and an inline note reads `Changing the adapter reset Model and Effort to their defaults.` followed by `Each adapter advertises its own models…` (`AgentProfileCard.razor:326-331`).
- That note is a **status**, not an alert — it does not interrupt, and a screen reader announces it politely. (Inspect for `role="status"`; `role="alert"` is a failure.)
- If Lark also had a non-default Work mode, a second `role="status"` note says `Changing the adapter reset Work mode to its default.` (`AgentProfileCard.razor:453-461`, ADR-0033).
- In step 4 the tile passes through Starting and returns to Online, `O-ADAPTERS` falls to `0` (the `claude` process exits) and `O-MOCK-ADAPTERS` rises by one (the new process is `mock-acp`, which `O-ADAPTERS` cannot see).
- In step 5 the file says `adapter: 'mock'`, and neither table has a row for `Lark`.

**Fail if — any of these**

- Model and Effort survive the Adapter change → a cross-Adapter Model id is meaningless and would be sent to an Adapter that never advertised it.
- The note uses `role="alert"` → it interrupts for something the Human just deliberately did.
- No restart occurs (`O-ADAPTERS` stays `1`) → `PersonaSupervisor.NeedsRestart` is no longer comparing the whole `Persona` record.

**Inconclusive if**

Lark cannot start on `claude` (Offline, or Degraded with an install message), so there is no Model to reset. Install the adapter and rerun.

---

### ADAPTERS-04 — Does a real local Model call `get_help` unprompted? 💰

**Paid** · about 45 min · *needs a real local Adapter and a running inference endpoint*

> [!NOTE]
> **Unblocked 2026-09-18 — runnable on `AgencyDotNet.Acp` 0.1.197 or later**, which on nuget.org
> means `0.1.198-ga453511f0e`; `0.1.197` went to the private feed and never published there. This test was
> blocked from 2026-09-17 because the composed system prompt never reached the agent: 0.1.195
> read only `_meta.model` and dropped `_meta.systemPrompt` silently, so the Model was never told
> `get_help` existed and the result would have measured the missing prompt rather than the Model
> (D-1 in Live findings). 0.1.197 reads it, verified
> live. **Check the version first** — `initialize` reports it in `agentInfo.version`, and on
> 0.1.195 or 0.1.193 this test is still meaningless rather than merely failing.

*This is the test no automated test can settle, and the reason this area has a paid tier at all. Progressive discovery is a deliberate bet: the system prompt names exactly one tool — `get_help` — and that tool names the rest. The bet assumes a model strong enough to ask. `mock-acp` calls what it is scripted to call and proves nothing about this. A 7B model that never calls `get_help` is not broken in any way a test can catch; it simply never creates a Room.*

**Before you start**

- A third Adapter Profile pointing at a real local Adapter, with `UsesToolNamePrefix` set to **`false`** unless its client prefixes MCP tool names (Agency's `McpClientPool` does not).
- A running inference endpoint with a named model loaded. **Record which model and which endpoint** — this result is meaningless without them.
- A Persona on that Adapter, Online.

**Steps**

1. In a two-Member Room with that Persona, ask it something that requires a Room it does not have: "start a room with the other agents and introduce yourself".
2. Watch `O-TOOLCALLS` for the Persona (`<data>\logs\tool-calls\<Name>\`) for any tool call at all. `O-WIRE` cannot be used: it sees only the echo bot, never an in-process Agent (F14).
3. If it calls `get_help`, read whether the next line is `create_room`.
4. Repeat twice more with differently-worded requests.
5. Record, in the Tracker, **the model name, the endpoint, and what it did** — for each of the three attempts.

**Pass if**

- The model calls `get_help` unprompted at least once, and acts on what it learns.

**Fail if**

- The model names a tool that does not exist — for example `mcp__team__get_help` against an Adapter whose profile sets `UsesToolNamePrefix: false`. That is a real defect: the prefix is wrong for this Adapter, and the model is being told to call something unreachable.

**Inconclusive if**

The model never calls any tool. **This is the expected result for a small model and is not a defect** — record it as INCONCLUSIVE with the model name, not as a failure. It is the honest limit roadmap item 12 named and [Known limits](../known-limits.md) records: expect local Models to suit narrow Personas long before they suit coordinators.

> [!NOTE]
> The three attempts matter. One refusal tells you nothing; three refusals across three wordings
> is evidence about that model, and that is the only form this answer can take.

---

Back to [the manual test script](../manual-tests.md).
