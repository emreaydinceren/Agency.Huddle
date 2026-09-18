# Adapters: choosing one per Persona, and running two at once

Prove, in a real browser, that the **Adapter** a Persona runs on is a per-Persona choice rather than an installation setting: that the Adapter select appears only when more than one Adapter Profile is configured, that two Personas on *different* Adapters can hold one Room between them, that a local Adapter's reply streams into the Room incrementally exactly as a cloud one's does, and that Stop leaves both resumable. One test here can be settled by no automated test at all and is the reason this area exists: whether a real local Model actually calls `get_help` unprompted.

**4 tests** · 2 free, 2 paid 💰 · about 1.4 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

Background, if you need it: [ADR-0013](../../adr/0013-an-adapter-is-a-property-of-the-persona.md)
(an Adapter is a property of the Persona), [ADR-0014](../../adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md)
(the tool-name prefix follows the Adapter) and the
[Adapters design](../../Huddle.Adapters-Specifications.md).

## Setup

Run [`P-BUILD`](common.md#p-build) then the **RESTART LANE** described in
[Model and Effort](model-effort.md) — teammate runners must actually start for anything here to
mean anything. This area adds:

1. **At least two Adapter Profiles must be configured**, or the Adapter select does not render at
   all and three of these four tests are unrunnable. That is correct behaviour, not a defect:
   a stock installation configures no `Team:Acp:Adapters` and gets exactly one synthesised
   profile, and a dropdown offering one option is a control nobody can use. Add to
   `appsettings.Development.json`:

   ```jsonc
   "Team": { "Acp": { "Enabled": true, "Adapters": [
     { "Id": "claude", "DisplayName": "Claude", "Description": "cloud, spends money per turn",
       "Command": "node", "UsesToolNamePrefix": true },
     { "Id": "mock",   "DisplayName": "Mock",   "Description": "a scripted adapter - no model",
       "Command": "E:/Repos/Huddle/src/Huddle.MockAdapter/bin/Debug/net10.0/mock-acp.exe",
       "UsesToolNamePrefix": false }
   ] } }
   ```

   The **first** profile in the list is the default, so a Persona with no `adapter:` field runs on
   `claude`. `mock-acp` is built by this solution's own `dotnet build Huddle.slnx` — it needs no
   Node, no subscription, no GPU and no money, which is what makes ADAPTERS-01 and -02 free.

2. For **ADAPTERS-04 only**, a third profile pointing at a real local Adapter (`agency-acp` or
   equivalent) and a running inference endpoint. That test is the one no automated test can
   settle; everything else here runs against `mock-acp`. **ADAPTERS-04 is currently blocked** —
   see the note on the test itself before setting any of this up.

   A real local Adapter usually ships **no `appsettings.json` of its own**, so
   `session/new` hard-fails with *"Agent:DefaultModel is not configured"* unless the profile
   supplies its configuration. That is what `EnvironmentOverrides` is for:

   ```jsonc
   { "Id": "agency", "DisplayName": "Agency", "Command": "C:/tools/agency-acp/agency-acp.exe",
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

**Free** · about 20 min

*Proves the load-bearing claim of the whole feature: the Adapter is resolved before the session exists, so the Room, the Reply Gate, the Budget and the Transcript never learn which Adapter answered. Two Teammates in one Room on two different Adapters must behave as two ordinary Teammates.*

**Before you start**

- Restart lane, two profiles configured (setup step 1).
- Two Personas exist: one with **no** `adapter:` line (so it runs on the default, `claude`) and one with `adapter: mock`.
- Both read **Online** on `/teammates`.

**Steps**

1. Open the Edit card for the `mock` Persona. Confirm an **Adapter** select is present, above **Model**, and that its closed value reads `Mock`.
2. Close the card without saving. Confirm with `O-FILE` that the Persona's `.md` file still carries `adapter: 'mock'` in its frontmatter, and that no `persona_adapters` table exists (`O-DB`).
3. Create a Room containing the Human and **both** Personas.
4. Post one Message Mentioning **both** by name.
5. Watch the Room.

**Pass if — all of these**

- Both Teammates reply in the same Room, in the same Transcript, rendered identically — nothing in the Room view distinguishes which Adapter answered.
- The Reply Gate treats them identically: a three-Member Room is Mention-gated for both, and a Message Mentioning neither wakes neither.
- The Room's Budget counts both replies against the same allowance.
- `O-ADAPTERS` shows **two different processes** — one `node`, one `mock-acp` — proving they really are on different Adapters and not silently both on the default.

**Fail if — any of these**

- Only one process appears in `O-ADAPTERS` → the Adapter is not per-Persona; something is reading a global setting.
- The `mock` Teammate reads Degraded with a message naming an unconfigured Adapter → the `Id` in the file does not match the `Id` in configuration (they are compared case-insensitively, so this is a spelling difference, not a casing one).
- Either reply renders differently from the other in the Transcript → something downstream of `PersonaRunner` has learned which Adapter answered, which the design forbids.

**Inconclusive if**

If the `claude` Teammate cannot start because the Node adapter is not installed, run this with **two** non-cloud profiles instead (point a second profile at the same `mock-acp` with a different `Id`) and record that substitution. It still proves the per-Persona claim; it just no longer proves it across a cloud/local boundary.

---

### ADAPTERS-02 — A local Adapter's reply streams into the Room incrementally, and Stop leaves both Teammates resumable

**Free** · about 20 min

*Proves what the mock's chunked-echo default exists to prove: that text arrives incrementally, becomes a Draft in the Room, and is persisted when the Turn ends. Then proves that stopping one Turn is not a failure of anything — the stopped Teammate is immediately usable again, and the other Teammate was never touched.*

**Before you start**

- ADAPTERS-01 passed, and its Room still exists with both Teammates in it.
- `O-LOG` visible in `T-A`.

**Steps**

1. Post a Message Mentioning **only** the `mock` Persona, long enough to be split into several chunks (a sentence of a dozen words is ample).
2. Watch the Room while it answers. Do not click anything yet.
3. Once it has finished, confirm the reply is in the Transcript (`O-TRANSCRIPT`), not only on screen.
4. Post another Message Mentioning **both** Personas.
5. While a Draft is visibly being written, click **Stop** on the `mock` Teammate's Turn.
6. Check the Teammate tiles on `/teammates` and the Room's health strip.
7. Post one more Message Mentioning **both**.

**Pass if — all of these**

- In step 2 the reply appears **progressively** — a Draft row that grows — not all at once when the Turn ends.
- The finished reply is one Message in the Transcript, and its text matches what was rendered.
- After the Stop in step 5: **no** health state is raised anywhere, **no** `role="alert"` appears, and neither tile leaves Online. A stopped Turn is not a fault.
- In step 7 **both** Teammates answer normally — the stopped one is resumable, and the other was never affected.

**Fail if — any of these**

- The reply appears in one jump → streaming is not reaching the Room; check `O-LOG` for `MessageDelta` envelopes before concluding it is the Adapter's fault.
- The reply renders progressively but the persisted Message is **shorter** than what was rendered → this is the diagnosed dispatch-ordering race in [Known limits](../known-limits.md); record it as that known bug, and note the exact text lost. **Do not** file it as new.
- The Stop marks the Teammate Degraded or Failed, or breaks a consecutive-failure streak → a stopped Turn is being told apart from a shutdown by exception type rather than by the run token.
- The other Teammate stops answering after the Stop → Stop is not scoped to one Agent.

---

### ADAPTERS-03 — Changing a Teammate's Adapter resets Model and Effort, says so, and restarts the session 💰

**Paid** · about 15 min · *spends one cloud Turn to prove the restart lost the memory*

*Proves the cascade and the consequence together: an Adapter change clears Model and Effort with an inline note, and saving restarts the session, which loses the conversation memory exactly as a Model change already does. See [ADR-0015](../../adr/0015-model-and-effort-reset-when-the-adapter-changes.md).*

**Before you start**

- A Persona on the `claude` Adapter with a **Model and an Effort explicitly chosen** (not left blank).
- It has had at least one exchange in a Room, so it has something to remember.

**Steps**

1. In its Room, ask it to remember a specific token — "remember the word *marmalade*". Confirm it acknowledges. **This is the paid Turn.**
2. Open its Edit card. Note the current Model and Effort.
3. Change **Adapter** to `Mock`.
4. Read the card without saving.
5. Save. Watch `/teammates` and `O-ADAPTERS`.
6. Return to the Room and ask what word it was asked to remember.

**Pass if — all of these**

- In step 4 both **Model** and **Effort** have cleared to their blank defaults, and an inline note explains that changing the Adapter reset them.
- That note is a **status**, not an alert — it does not interrupt, and a screen reader announces it politely. (Inspect for `role="status"`; `role="alert"` is a failure.)
- In step 5 the old Adapter process exits and a new one starts (`O-ADAPTERS`), and the tile passes through Starting.
- In step 6 the Teammate does **not** know the word. The restart lost the memory, which is the documented cost.

**Fail if — any of these**

- Model and Effort survive the Adapter change → a cross-Adapter Model id is meaningless and would be sent to an Adapter that never advertised it.
- The note uses `role="alert"` → it interrupts for something the Human just deliberately did.
- No restart occurs → `PersonaSupervisor.NeedsRestart` is no longer comparing the whole `Persona` record.
- The Teammate still remembers the word → the session was not actually restarted, only relabelled.

---

### ADAPTERS-04 — Does a real local Model call `get_help` unprompted? 💰

**Paid** · about 45 min · *needs a real local Adapter and a running inference endpoint*

> [!IMPORTANT]
> **BLOCKED as of 2026-09-17 — do not run this yet, and do not record a result for it.**
> Against `AgencyDotNet.Acp` 0.1.195 the composed system prompt never reaches the agent:
> Huddle sends it at `_meta.systemPrompt` and that adapter reads only `_meta.model`, so the
> prompt is dropped silently (D-1 in [Live findings](../../Huddle.Adapters-LiveFindings.md),
> and in [Known limits](../known-limits.md)). The prompt that names `get_help` is the only
> thing that tells the Model `get_help` exists — so this test would return INCONCLUSIVE every
> time, measuring the missing prompt rather than the Model. That is worse than not running it,
> because the Tracker would then carry something that reads as evidence about a Model and is
> not. Run it once a Persona's own text demonstrably arrives; until then the result column
> stays empty on purpose.

*This is the test no automated test can settle, and the reason this area has a paid tier at all. Progressive discovery is a deliberate bet: the system prompt names exactly one tool — `get_help` — and that tool names the rest. The bet assumes a model strong enough to ask. `mock-acp` calls what it is scripted to call and proves nothing about this. A 7B model that never calls `get_help` is not broken in any way a test can catch; it simply never creates a Room.*

**Before you start**

- A third Adapter Profile pointing at a real local Adapter, with `UsesToolNamePrefix` set to **`false`** unless its client prefixes MCP tool names (Agency's `McpClientPool` does not).
- A running inference endpoint with a named model loaded. **Record which model and which endpoint** — this result is meaningless without them.
- A Persona on that Adapter, Online.

**Steps**

1. In a two-Member Room with that Persona, ask it something that requires a Room it does not have: "start a room with the other teammates and introduce yourself".
2. Watch `O-WIRE` or `O-LOG` for any tool call at all.
3. If it calls `get_help`, watch whether it then calls `create_room`.
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
