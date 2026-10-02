# Huddle.WorkModes — Design Specification

**Date:** 2026-09-30 · **Status:** Phase 1 built 2026-09-30 (phases 2 and 3 designed, not built;
OQ-2, OQ-3 and OQ-5 answered live outside the app on 2026-10-01, so `plan` is now hidden by default;
the in-app manual runs not done) · **Decision record:**
[ADR-0033](adr/0033-a-persona-has-a-work-mode.md) · **Vocabulary:**
[language.md](engineering/language.md) (**Work Mode**). §14 lists where the build differs from
the design below.

This is the design for a **Work Mode**: a Persona setting, chosen from the modes its Adapter
advertises, that decides how much the Agent may do before it must ask. It uses the ACP `mode`
config option, the same mechanism Huddle already uses for Model and Effort. The first phase
here gives each Persona one Work Mode for the life of its session. A live per-Room switch and a
plan-approval card are designed in §10 and are not part of the first build.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Read §5 for the shape, §6 for the parts, §12 for the decisions and the
alternatives each beat, and Appendix A for the ordered, test-first task list.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file.
> [traps.md](engineering/traps.md) is binding before touching `Huddle.Acp`, and this spec does.
> Nothing here overrides any of them.

> [!NOTE]
> **Ownership.** `src/Huddle.Acp` belongs to the ACP effort, and this spec adds members to
> `AgentSessionOptions`, `IAgentSession`, `DotAcpAgentHost` and `SessionUpdateMapper`. Announce
> that before the first commit that touches it. Effort crossed the same line, so there is
> precedent. The shared `FakeAcpAgent` is **not** edited: tests build mode options as raw JSON,
> as the effort tests do.

> [!NOTE]
> **What this does not change.** [Roadmap §20](engineering/roadmap.md) records the Human's
> decision, on 2026-09-30, that auto-approve is acceptable for now. It stays. This spec adds
> one refusal to the handler (§6.9) and a way to choose a mode. Nothing in it asks the Human
> before a tool runs.

**Source of the Adapter facts.** Every statement about `claude-agent-acp` below was read from
the vendored 0.75.1 `dist/` in `tools/acp/node_modules` on 2026-09-30, and **none of it has
been run**. Each is marked *(source)*. Section 13 lists what a live run must confirm before
the code that depends on it is written.

---

## 1. Goal

Let a Persona have a Work Mode, chosen from what its Adapter advertises, and apply it reliably.

1. **A Persona may name a Work Mode.** It is the id of an advertised mode. Unset means the
   Adapter's own default, and the wire is then exactly what it is today.
2. **It is applied at every open and every resume**, after Model and Effort, and verified against
   the response, because an Adapter may clamp it.
3. **A Persona in `plan` mode stays in it.** The handler refuses the Adapter's request to leave.
4. **Dangerous modes are hidden by default,** and the hiding is enforced when a session opens.
5. **A stale, unknown or unadvertised mode never stops a Persona starting.** It is a warning.

**Why this matters.** Today the handler approves every request, so a reviewer Teammate can edit
files. A live Turn on 2026-09-30 also showed that an ordinary edit inside the Work Dir asks
first, and the handler approves it without the Human seeing anything. A mode changes what the
Agent is allowed to try, which a handler that answers after the Agent has asked cannot do.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| M1 | The Human gives the Reviewer Persona the **Plan** mode and asks it to review a file | The Reviewer reads and answers. It edits nothing. Its request to leave plan mode is refused. *Hidden by default since 2026-10-01 (D-17): the refused exit ends the Turn cancelled and no reply is posted* |
| M2 | The Human gives the Builder Persona **Accept edits** | Edits inside its Work Dir stop producing `session/request_permission` requests |
| M3 | A Persona has no Work Mode | Nothing new is sent. The wire is identical to today |
| M4 | The Human changes a Persona's Adapter in the card | The Work Mode clears with an inline note, as Model and Effort do |
| M5 | A Persona with a Work Mode resumes its Room Session after an app restart | The mode is re-applied if the resumed session reports a different one |
| M6 | The stored mode is no longer advertised, for example after an Adapter upgrade | A warning is logged and the session starts on the Adapter's default |
| M7 | A row for `bypassPermissions` is written into the database by hand | It is never sent. A warning is logged and the session starts on the default |
| M8 | The operator sets `Team:Acp:HiddenModes` to a list of one empty string (configuration cannot express an empty list; §6.6) | Every advertised mode appears in the picker |
| M9 | The Adapter advertises no mode option, as `agency-acp` may not | The picker is not shown. A stored value is ignored with a warning |
| M10 | An Agent enters plan mode itself | Nothing changes. The event is logged when it differs from the Persona's Work Mode |
| M11 | The Adapter clamps the requested `auto` to `acceptEdits` | A warning names both values. The session keeps running on the effective one |
| M12 | The Human edits only the Work Mode and saves | The Persona restarts and forgets its Room Sessions, like a Model change |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **A live switch of mode in a Room** | Needs new wire messages and a Room-keyed UI. §10, phase 2 |
| **A plan-approval card** | Needs the plan text in Huddle's model and the phase 2 switch. §10, phase 3 |
| **Asking the Human before a tool runs** | Roadmap §20: not designed, and not decided. A request that waits collides with the idle watchdog |
| **`session/set_mode` and the legacy `modes` field** | Deprecated, and the v2 draft removes them. Config options carry the same information |
| **A fixed list of mode ids in code** | Adapters differ, and the spec says a category carries no correctness (D-1) |
| **Setting a mode from `propose_teammates` or a Persona file** | `Candidate` cannot set Model, Effort or Adapter, for the same reason: each one grants capability or cost (D-9) |
| **Changing what the handler approves, beyond the plan guard** | Roadmap §20 owns that decision |
| **ACP elicitation** | Not advertised, and unrelated. [ADR-0022](adr/0022-an-agent-asks-the-human-with-a-question.md) D-13 |

---

## 4. Design principles

1. **The Adapter advertises; Huddle discovers.** A mode id is never a constant in code, with two
   named exceptions, each a safety rule: the hidden list (§6.6) and the plan guard (§6.9).
2. **Text never guards; code does.** Hiding a mode in the picker is not enough. The refusal is
   in `DotAcpPersonaHost.BuildOptions`, so a database row written by hand cannot select it.
3. **Copy Effort; do not generalise it.** Same table shape, same probe, same card behaviour,
   same reset rules. Where Work Mode differs, §8 says so, and the ADR records why.
4. **A stale value never blocks a session.** `rules.md` already says this for Model. It holds
   for a mode.
5. **Unset is invisible.** A Persona with no Work Mode causes no new request, log line or
   golden change.

---

## 5. Architecture overview

```text
 Settings
   Team:Acp:HiddenModes ─────────────────────────────┐
                                                     ▼
 Teammate card ── GetWorkModesAsync(adapter, model) ─▶ ModelCatalogProbe ─▶ throwaway session/new
   pick a mode ──▶ PersonaStore.Update(…, workMode)                          (one probe: models, efforts,
        │                                                                     modes; no prompt sent)
        ▼
   persona_work_modes  ─▶ Persona(… WorkMode) ─▶ PersonaSupervisor.NeedsRestart (record equality)
                                   │
                                   ▼
   DotAcpPersonaHost.BuildOptions
     ├─ WorkMode hidden or unadvertised?  → drop it, log a warning
     ├─ permission handler = PlanModePermissionHandler(WorkDirPermissionHandler)   (§6.9)
     └─ AgentSessionOptions.Mode
                                   │
                                   ▼
   DotAcpAgentHost.RegisterAndConfigureAsync           (start and resume share it)
     model ─▶ effort ─▶ mode           each reads the snapshot the previous set returned
       └─ mode: resolve in the catalog ─▶ skip if already current ─▶ session/set_config_option
                                        ─▶ read currentValue back ─▶ warn if the Adapter clamped it
                                   │
                                   ▼
   session events: current_mode_update / config_option_update ─▶ ModeChanged ─▶ RoomSession (log only)
```

| Component | Kind | New or changed |
| --- | --- | --- |
| `Acp/AgentModeOption.cs` | Record | New (`Huddle.Acp`) |
| `DotAcp/ModeConfigOptions.cs` | Facade over `SessionConfigSelects` | New |
| `AgentSessionOptions`, `IAgentSession` | `Mode`; `ModeOptions`, `CurrentModeId` | Changed |
| `DotAcpAgentHost.cs` | Applies and verifies the mode | Changed |
| `SessionUpdateMapper.cs` | Maps `config_option_update` when it carries a mode | Changed |
| `Acp/Persona.cs` | Gains `WorkMode` | Changed |
| `Data/PersonaWorkModeStore.cs` | Sibling table | New |
| `PersonaStore.cs` and its four callers | Join, `Add`, `Update`, `Remove`, rename | Changed |
| `Acp/WorkModePolicy.cs` | The hidden list | New |
| `Acp/PlanModePermissionHandler.cs` | Decorator that refuses plan exit | New |
| `IModelCatalog`, `ModelCatalogProbe`, `AdapterProbeOutcome` | Modes share the effort probe | Changed |
| `Acp/Sessions/RoomSessionStore.cs`, `RoomSession.cs` | Resume match rule | Changed |
| `TeammateCard.razor`, `Teammates.razor` | The picker | Changed |
| `AcpOptions` | `HiddenModes` | Changed |

---

## 6. Components

### 6.1 Reading the catalog

```csharp
/// <summary>One mode an Adapter advertises: the id to send, and what to show the Human.</summary>
public sealed record AgentModeOption(string Id, string Name, string? Description);
```

`ModeConfigOptions` mirrors `EffortConfigOptions` line for line, with the category fixed to
`SessionConfigOptionCategory.Mode`. That member already exists in dotacp 2026.7.19.

```csharp
internal static IReadOnlyList<AgentModeOption> Read(SessionConfigOption[]? configOptions);
internal static bool TryResolve(SessionConfigOption[]? configOptions, string mode,
    out SessionConfigId configId, out SessionConfigValueId value);
```

Both delegate to `SessionConfigSelects`, which already handles the flat-or-grouped union, the
struct-equality category match and the rule that the id to write back is the option's own
(traps.md: never hardcode a `configId`). `Read` does **not** filter any id (§8.1).

*(source)* `claude-agent-acp` 0.75.1 advertises the option as `id: "mode"`, `name: "Mode"`,
`category: "mode"`, `type: "select"`, with these values:

| Value | Name | Advertised |
| --- | --- | --- |
| `default` | Manual | Always |
| `acceptEdits` | Accept edits | Always |
| `plan` | Plan | Always |
| `auto` | Auto | Always, but clamped to `acceptEdits` on a model that does not support it |
| `bypassPermissions` | Bypass permissions | Unless the process is root. Windows has no `geteuid`, so **it is offered on Windows** |

`dontAsk` is accepted by the Adapter's parser and **not** advertised. Do not design for it.

### 6.2 Session options and the session

`AgentSessionOptions` gains a trailing optional `string? mode = null`, normalised like `Effort`
(`IsNullOrWhiteSpace` becomes null). It is trailing so no positional caller breaks; the only
positional caller is `DotAcpPersonaHost.BuildOptions`, and the probe passes `model:` by name.

`IAgentSession` gains two members, read by the probe and by tests:

```csharp
IReadOnlyList<AgentModeOption> ModeOptions { get; }
string? CurrentModeId { get; }
```

`DotAcpAgentSession` sets both through internal setters beside `SetEffortLevels`, because
`ISessionSink` deliberately does not expose them. `FakeAgentSession` gains matching members.

### 6.3 Applying and verifying, at open and at resume

`RegisterAndConfigureAsync` (`DotAcpAgentHost.cs`) serves both `StartSessionAsync` and
`ResumeSessionAsync`, so one change covers both. It becomes:

```text
 snapshot := initial configOptions
 snapshot := ApplyModel(snapshot)      returns the post-set snapshot          (exists)
 session.SetEffortLevels(read snapshot)                                       (exists)
 snapshot := ApplyEffort(snapshot)     now returns the post-set snapshot too  (changed: was void)
 session.SetModeOptions(ModeConfigOptions.Read(snapshot))
 snapshot := ApplyMode(snapshot)
 session.SetCurrentMode(current mode value in snapshot)
```

`ApplyMode`:

1. `options.Mode` is null: return. Nothing is sent.
2. `TryResolve` fails, or the Adapter advertises no mode option: log `LogModeNotInCatalog`,
   return. **A warning, never a failure.**
3. The snapshot's mode `currentValue` already equals the request: return. This is the normal
   resume case if the Adapter kept it, and it saves a request.
4. Send `SetSessionConfigOptionRequest { SessionId, ConfigId, Type = "select", Value }`.
   `Type` defaults to `""` and **must** be set (traps.md).
5. A `RemoteInvocationException` logs `LogModeConfigFailed` and the session continues.
6. **Read the response back.** If the mode's `currentValue` is not the requested value, the
   Adapter clamped it: log `LogModeClamped(requested, effective)`. The session keeps the
   effective value, and `CurrentModeId` reports it.

**Order.** Model, then Effort, then Mode, each against the snapshot the previous call returned.
*(source)* A model switch can itself change the mode: `auto` falls back to `acceptEdits` on a
model without support. Setting the mode last means the request is judged against the model the
session will run.

**Resume.** *(source)* The Adapter takes its initial mode from its own settings default, never
from the session being resumed (`acp-agent.js`, `initialPermissionMode`). A resumed session
should therefore come back in Manual, and step 3 above will find it different and set it. **Seen
live on 2026-09-30 (OQ-1):** a session set to `acceptEdits` and resumed from a new process
reported `default`. That is the answer to `known-limits.md` V-3 for this option.

### 6.4 Events

```text
 current_mode_update                      → ModeChanged(sessionId, modeId)        (exists)
 config_option_update with a mode select  → ModeChanged(sessionId, currentValue)  (new)
 config_option_update without one         → UnknownUpdate                         (unchanged)
```

No new `AgentEvent` type, so `ConsoleRenderer` and its exhaustive switches need no change.

*(source)* Setting a mode through `session/set_config_option` emits `current_mode_update`, and a
model switch that invalidates `auto` emits `config_option_update`. Both can arrive for one change,
so a consumer treats a `ModeChanged` equal to the last one as a no-op.

**`RoomSession`** reads `ModeChanged` today and ignores it. It now compares the value with the
Persona's `WorkMode` and logs at information level when they differ ("Agent switched mode to X;
the Persona's Work Mode is Y"). That is drift detection, and nothing acts on it. Any event still
resets the idle watchdog, as now.

### 6.5 The Persona and its storage

```csharp
public sealed record Persona(string Name, string Text, string? Model = null, string? Effort = null,
    string? Adapter = null, string? WorkMode = null);
```

A scalar, so `PersonaSupervisor.NeedsRestart` (`started is null || persona != started`) picks it
up with no edit. **Never add a list-typed member** to this record (rules.md).

**Why `WorkMode` here and `Mode` in the ACP layer.** `Mode` already means light or dark
(`ThemeDescriptor.Mode`), the card state (`TeammateCardMode`, and `TeammateCard` already has a
`[Parameter] Mode`) and `SystemPromptMode`. The Persona and the UI use **Work Mode**. The ACP
layer keeps ACP's own word, `Mode`, where it is unambiguous.

**Storage.** A sibling table, as rules.md requires. `CREATE TABLE IF NOT EXISTS` never adds a
column, so a new column on an existing table would leave an upgraded database without it.

```sql
CREATE TABLE IF NOT EXISTS persona_work_modes (
    persona_name TEXT PRIMARY KEY COLLATE NOCASE,
    work_mode    TEXT NOT NULL
)
```

`PersonaWorkModeStore` mirrors `PersonaEffortStore`: synchronous, created in its constructor,
`Get` returning `string?`, `Set` deleting the row when the value is null or blank, and `Remove`.
It is registered beside the other two in `ServiceCollectionExtensions`.

`PersonaStore` changes:

| Member | Change |
| --- | --- |
| `Get` | Joins the row: `new Persona(…, entry.Adapter, this.workModes.Get(entry.Name))` |
| `Add(identity, body, model, effort)` | Gains `workMode`, and sets the row |
| `Update(name, text, model, effort)` | Gains `workMode`, **with no default**, and sets or clears the row |
| `Remove` | Removes the row |
| Rename path | Moves the row with the others |

`Update` has no defaults on purpose, so a caller cannot silently wipe a value. Every caller must
therefore pass the mode, and one of them is easy to miss:

| Caller | Passes |
| --- | --- |
| `TeammateCard.razor` `SaveAsync` | The card's `workMode` |
| `Teams/TeamMembership.cs:120` | `persona.WorkMode`. **Missing this wipes the mode on every membership edit**; a test pins it |
| `Teammates/BuiltinTeammateReset.cs` | `null` |
| `Teammates/BuiltinTeammateSeeder.cs` | `null` |
| `Teammates/ProposalService.cs` | Nothing. `Add` defaults it to `null` |

The rename cascade (`PersonaRenameCascade`) moves the row through `PersonaStore.Update`, as it
does for Model and Effort.

### 6.6 The hidden list

```csharp
internal sealed class WorkModePolicy(IOptions<TeamOptions> options)
{
    internal bool IsOffered(string modeId);
    internal IReadOnlyList<AgentModeOption> Filter(IReadOnlyList<AgentModeOption> modes);
    internal string? EffectiveMode(string personaName, string? workMode, ILogger logger);
}
```

`AcpOptions.HiddenModes` is an `IReadOnlyList<string>?`, bound from `Team:Acp:HiddenModes`. The
`Team:` root is kept, per CLAUDE.md.

| Configured value | Meaning |
| --- | --- |
| Key absent (`null`), or an empty value | The default: `["bypassPermissions", "auto", "plan"]` |
| A list of one empty string | Nothing is hidden: it replaces the default and names no real mode |
| A list | Exactly those ids, compared ordinally |

**An empty list cannot be written in configuration.** The binder reads an empty value as absent, and
`[]` in JSON produces no keys, so both fall back to the default. *(As built: this table first said
`[]` hides nothing, and a test caught it.)* The list of one empty string is the spelling that works,
and `WorkModePolicyTests` pins both facts.

**The default lives in `WorkModePolicy`, not as an initialiser on the property.** The .NET
configuration binder writes an array into a pre-populated one by index. An operator who wrote
`["auto"]` over a default of `["bypassPermissions", "auto", "plan"]` would get
`["auto", "auto", "plan"]`, which silently unhides `bypassPermissions`. A test binds each shape (WM-T6).

**`plan` is in the default list.** *(As built, 2026-10-01: this was `["bypassPermissions", "auto"]`
in the first design.)* A live run found that a refused plan exit ends the Turn cancelled with no
plan text, and `RoomSession` posts no cancelled Turn, so a plan-mode Persona is silent in the Room
(OQ-2, §6.9). The spec had pre-decided this fallback. The plan guard stays, and is reached when an
operator lifts the block through `Team:Acp:HiddenModes`.

`Filter` is applied in the catalog (§6.7), so the picker never offers a hidden mode.
`EffectiveMode` is applied in `BuildOptions` (§6.8), so a stored one is never sent.

### 6.7 The catalog

Modes come from the same throwaway `session/new` as the effort ladder, so they add no extra
Adapter spawn.

| Type | Change |
| --- | --- |
| `AdapterProbeOutcome` | Gains `IReadOnlyList<AgentModeOption> Modes`, as an `init` property so the two-argument construction every fake uses is unchanged |
| `AdapterProcessProbeRunner` | Returns `session.ModeOptions` in the outcome |
| `IModelCatalog` | `ValueTask<IReadOnlyList<AgentModeOption>> GetWorkModesAsync(string? adapterId, string? model, CancellationToken ct)` |
| `ModelCatalogProbe` | The effort cache value becomes a small `SessionCatalog(EffortLevels, Modes)`, so one probe fills both. Keyed per (resolved Adapter id, model) as now |

Behaviour is inherited: failures are never cached, a successful empty answer is cached, the
probe runs from the Teammate definitions folder, and it runs even when `Team:Acp:Enabled` is
false. **`WithoutAdapterDefault` is not applied to modes** (§8.1). The `HiddenModes` filter is
applied on the way out, and the cache holds the unfiltered list, so changing the setting needs
no re-probe.

`FakeModelCatalog` and `FakeAdapterProbeRunner` gain mode members. **No test may reach the real
probe** (rules.md).

### 6.8 Enforcing it at session open

`DotAcpPersonaHost.BuildOptions` (`:171-217`) passes the mode to the options, after Effort:

```text
 mode := persona.WorkMode
 mode is null                        → null
 not policy.IsOffered(mode)          → log warning "Work Mode 'x' is hidden; starting on the default"; null
 otherwise                           → mode
```

**This is the enforcement.** The picker hiding a mode is convenience. This makes a hand-edited
row for `bypassPermissions` inert (M7). It does **not** check the mode against the catalog:
that is `ApplyMode`'s job (§6.3), where the catalog is in hand.

### 6.9 The plan guard

*(source)* When a model in `plan` mode finishes planning, `claude-agent-acp` asks permission to
leave it. The tool call is `ExitPlanMode`, presented with the title **"Approve Plan"**, kind
`switch_mode`, and the plan text in its content. Its options are:

| Option | Kind | Effect if chosen |
| --- | --- | --- |
| Yes, clear context and use `auto` / `bypassPermissions` / `acceptEdits` | `allow_always` | Exits plan mode into the elevated mode, on a fresh context |
| Yes, and use `auto` / `bypassPermissions` / `acceptEdits` | `allow_always` | Exits plan mode into the elevated mode |
| Yes, manually approve edits | `allow_once` | Exits plan mode into Manual |
| No, keep planning | `reject_once` | Stays in plan mode |

`WorkDirPermissionHandler` prefers `allow_once`, so it would choose **"Yes, manually approve
edits"** on the first request and take the Persona out of plan mode. A Work Mode of `plan` would
then last until the first plan was finished.

```csharp
internal sealed class PlanModePermissionHandler(
    IPermissionHandler inner, string? workMode, ILogger<PlanModePermissionHandler> logger)
    : IPermissionHandler
```

- When `workMode` is `"plan"` and the request's `ToolCall.Kind` is `ToolKind.SwitchMode`,
  refuse: pick `RejectOnce`, else `RejectAlways`, else `Cancelled`. Log it at information level.
- Otherwise delegate to `inner`, unchanged.
- `BuildOptions` wraps the handler only when the Persona's effective mode is `plan`. Every other
  Persona gets the handler it has today.

The id `"plan"` is a named constant, the second place an id appears in code (§4). It is
Adapter-specific knowledge: the ACP spec's own examples use other ids. If an Adapter's plan mode
has a different id, the guard is inert and the Persona behaves as it does today. That failure is
benign, so no per-Adapter table is added.

`EnterPlanMode` has no `switch_mode` presentation in the Adapter *(source)*, so it is
unaffected: the guard does not stop a model in Manual from entering plan mode, and does not
stop it leaving one it entered itself.

**OQ-2 ✓, answered 2026-10-01: the plan does not reach the Room as text.** A live run (the
vendored 0.75.1 on Haiku, driven over stdio outside the app, one run) set `plan` and asked for a
plan. The model used `Find` and `ToolSearch`, then the Adapter sent `session/request_permission` of
kind `switch_mode` titled "Approve Plan", with the whole plan as markdown in `rawInput.plan`. The
options were `allow_once` "Yes, manually approve edits", `allow_always` "Yes, clear context (26%
used) and use auto mode", `allow_always` "Yes, and use auto mode", and `reject_once` "No, keep
planning". The guard refused with `reject_once`. The Turn then ended with stop reason `cancelled`,
and the only assistant text was 126 characters of preamble, not the plan. No file was written.
`RoomSession` does not post a cancelled Turn (`isPostable` excludes `Cancelled`), so a plan-mode
Persona produces **no visible reply**; the plan exists only in the tool call's `rawInput.plan`.
Consequence: `plan` is in the default hidden list (§6.6), and the prerequisite for offering it
again is phase 3 (§10), which captures the plan from the tool call.

### 6.10 The resume match

`RoomSessionEntry` gains `string? WorkMode`, as a **trailing optional** parameter (it was designed
"after `Effort`", which would have changed 27 positional constructions). `OpenOrResumeAsync` adds:

```csharp
&& string.Equals(entry.WorkMode, this.persona.WorkMode, StringComparison.Ordinal)
```

- The entry is written at Turn end from `this.persona.WorkMode`.
- The store's `WhenWritingNull` policy omits a null, so **existing files deserialise to
  `null` and still match a Persona with no Work Mode.** No migration.
- Update the prose that names the rule: `RoomSession.cs`, `RoomSessionPool.cs`,
  `PersonaRunner.cs`, `PersonaSupervisor.cs`, and rules.md.

### 6.11 The Teammate card

A `MudSelect` after Effort, following its markup and rules exactly.

| Aspect | Rule |
| --- | --- |
| Parameter and field | `[Parameter] WorkMode` and a private `workMode`. **Not** `Mode` or `mode` |
| Label | *Work mode* |
| Blank option | *Use the agent's default* (null) |
| Choices | The catalog's modes. A stored id absent from it is **synthesised**, as `EffortChoices` does |
| Helper text | The selected mode's `Description`, which the Adapter supplies. Model descriptions are rendered nowhere today; this one is |
| While probing | `Disabled`, not `ReadOnly` (rules.md) |
| Not shown | When the catalog is empty and nothing is stored (M9) |
| View mode | A *Work mode* paper beside Model and Effort, showing the mode's name, or *the agent's default* |
| Adapter change | Clears `workMode` with a `role="status"` note, extending ADR-0015 (M4) |
| Model change | Keeps it. Modes are not model-dependent, except `auto` (§6.3) |
| Save | Passes `workMode` to `Add` and `Update` |

Traps the card already carries, that this must respect:

- **`BeginEditAsync` seeds with `persona?.Model ?? this.model`**, so a null read-back keeps a
  stale value. Assign `workMode` directly from the Persona.
- **Modes load in `LoadEffortsAsync`** (renamed `LoadSessionCatalogAsync`), from the same probe
  result, under the same generation counter, so the faster-probe-wins rule holds. No new
  in-flight path is created, so no second `StateHasChanged` is needed.
- A string `[Parameter]` is written with a leading `@` (`TeammatesRazorSourceTests` guards it).
- No colour literals, and no `role="alert"` for a consequence of the Human's own action.

`Teammates.razor` `BuildViewParameters` passes `WorkMode` beside `Model` and `Effort`.

---

## 7. Storage

| State | Where | Survives restart |
| --- | --- | --- |
| A Persona's Work Mode | `persona_work_modes`, SQLite | Yes |
| The catalog | `ModelCatalogProbe`, memory | No |
| A Room Session's mode | `RoomSessionEntry.WorkMode`, JSON | Yes, as part of the resume match |
| The hidden list | `Team:Acp:HiddenModes`, configuration | Yes |
| The session's *current* mode | The Adapter, and `IAgentSession.CurrentModeId` | No. Re-applied at every open |

---

## 8. Core rules

### 8.1 Where Work Mode differs from Effort

| | Effort | Work Mode |
| --- | --- | --- |
| Ladder depends on the Model | Yes | No, except `auto` (§6.3) |
| Reset when the Model changes | Yes | No |
| Reset when the Adapter changes | Yes | Yes |
| An advertised `default` id | A sentinel, filtered, stored as null | A real mode, **Manual**, kept |
| A stored value not advertised | Warning | Warning |
| Applied | At open | At open **and verified**, because the Adapter may clamp it |
| Hidden by policy | No | Yes (§6.6) |

### 8.2 What is sent

| `Persona.WorkMode` | Hidden? | Advertised? | Already current? | Result |
| --- | --- | --- | --- | --- |
| Null | — | — | — | Nothing |
| Set | Yes | — | — | Dropped in `BuildOptions`, warning |
| Set | No | No | — | Warning, in `ApplyMode` |
| Set | No | Yes | Yes | Nothing |
| Set | No | Yes | No | `session/set_config_option`, then read back |

### 8.3 Who may set it

Only the Human, in the Teammate card. It is not a field of a Persona file, not a
`Candidate` field, and not an argument of any App Tool. Each of those would let an Agent choose
its own permissions.

---

## 9. Edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| E-1 | The Adapter is upgraded and stops advertising the stored mode | Warning at open, session on the default. The card shows the stored id, synthesised (M6) |
| E-2 | `auto` on a model without support | The Adapter clamps to `acceptEdits` and emits a message chunk. Huddle logs both values (M11) |
| E-3 | That message chunk arrives before any Turn | Measured (OQ-5): it arrives as a `session/update` with no Turn running, and `RoomSession` drops a chunk with no active Turn. A race with the first Turn would append it to that reply. `auto` stays hidden by default |
| E-4 | Resume and the Adapter reports Manual | Step 3 of §6.3 finds a difference and sets the stored mode |
| E-5 | The Persona restarts while a Room Session exists | `ForgetAll` runs, as for a Model change, so no resume is attempted |
| E-6 | A shared session (`SessionPerRoom: false`) | One mode for every Room, and it never resumes (RS P-15), so the mode is applied at every open |
| E-7 | The Human edits the mode on a Persona with a Turn running | The restart follows the existing edit path: a Stop is not a failure (rules.md) |
| E-8 | Two `ModeChanged` events for one change | The second equals the first and is ignored |
| E-9 | The probe finds no mode option | The picker is hidden, and nothing is stored unless it already was (M9) |
| E-10 | The plan guard refuses and the model retries `ExitPlanMode` | Each request is refused. The Turn ends by the existing paths: EndTurn, idle timeout or Stop |
| E-11 | `IsOffered` is false for a mode the Human just saved, because the operator hid it later | Dropped at open with a warning. The card still shows it, synthesised |

---

## 10. Later phases

Neither phase is built by this spec. Each has a prerequisite, named so it is not forgotten.

**Phase 2: a live switch per Room, with no restart.**
- `IAgentSession.SetModeAsync` calls `session/set_config_option`, between Turns.
- New additive wire messages and a small per-(Agent, Room) store shaped like `TurnActivity`,
  with a chip near the Room header. A new `[JsonDerivedType]` and **no `ProtocolVersion` bump**
  (traps.md). The wire names must differ from the ACP event `ModeChanged` (traps.md: two types
  named for one ACP concept).
- The chosen mode is written to the Room Session entry, so a resume restores it.
- Prerequisite: OQ-1 (answered: a resumed session comes back in Manual), because a switch that does
  not survive resume is a bug.

**Phase 3: plan approval, without blocking.** It is also the prerequisite for offering `plan` by
default: until the plan is captured, a plan-mode Persona is silent in the Room (OQ-2).
1. The guard still refuses the exit, so the Turn is not held open ([ADR-0022](adr/0022-an-agent-asks-the-human-with-a-question.md)).
2. The plan is captured from the tool call, where it arrives as `rawInput.plan` and as a
   content block, and shown on a card beside `ProposalCard`. `ToolCallInfo` carries
   `RawInputJson` today, and drops content.
3. **Approve** calls the phase 2 switch to `acceptEdits`, then posts a Message that starts the
   next Turn.

This is deliberately not roadmap §20's card, which answers the Adapter's request while it
waits and therefore needs the idle watchdog paused and a Room bound to the session. It avoids
all three costs, and it depends on phase 2. Choosing option ids is the alternative *(source:
each option's id decides the next mode)*, and is what a blocking design would do.

---

## 11. Testing

Every automated test uses fakes and costs nothing. **`TEAM_E2E` is never set** (traps.md). What
only a real model can show is in the manual tests, and the ones marked † spend tokens on the
Human's subscription.

**Golden files do not change.** `SystemPromptComposer` receives the Persona but does not read
`Model` or `Effort`, and this adds no prompt text. Task WM-G1 asserts `PromptGoldenTests` passes
unchanged; a golden diff means something leaked into a prompt.

**Manual tests**, to add as `manual-tests/work-mode.md` and to
[manual-tests.md](engineering/manual-tests.md):

| Id | Steps | Pass |
| --- | --- | --- |
| MW-1 | Open a Claude Persona's card | The picker lists Manual and Accept edits. Plan, Auto and Bypass permissions are absent |
| MW-2 † | Give it Accept edits. With `Team:Acp:TraceWire=true`, ask it to create and edit a scratch file in its Work Dir | The trace shows no `session/request_permission` for either call. Roadmap §20 measured two. *Measured outside the app 2026-10-01: an edit inside the Work Dir produced no request and the Turn ended `end_turn`* |
| MW-3 † | With `Team:Acp:HiddenModes:0` set to `auto`, so `plan` is offered, give it Plan and ask it to add a function | No file is written. The trace shows one `switch_mode` request refused. **No plan text reaches the Room** (OQ-2): the Turn ends cancelled and nothing is posted, which is expected until phase 3 |
| MW-4 † | Give it Accept edits and ask it to write a file into `~/.claude` | The file is not written (OQ-3). *Measured outside the app 2026-10-01 with a temp directory outside the Work Dir: a `session/request_permission` still arrived and the refusal kept the file unwritten* |
| MW-5 † | Give it Accept edits, exchange a message, restart the app, message it again | The trace shows `session/resume`, then a `set_config_option` for the mode if the Adapter reported another (OQ-1) |
| MW-6 | Give it a mode, then change its Adapter in the card | The mode clears with a `role="status"` note |
| MW-7 | Write `bypassPermissions` into `persona_work_modes` by hand and restart | The session starts on the default, a warning is logged, and no mode request is sent |
| MW-8 | Set `Team:Acp:HiddenModes:0` to an empty value (a list of one empty string; §6.6) | The picker also lists Plan, Auto and Bypass permissions |
| MW-9 | Open a card for an Adapter that advertises no mode | No picker is shown |

---

## 12. Decisions

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-1 | **Discover modes from the Adapter** | A fixed Huddle list | Adapters differ, and the spec says a category must not carry correctness. Two ids in code, each for safety (§4) |
| D-2 | **Set it with `session/set_config_option`** | `session/set_mode` | Deprecated, and removed in the v2 draft. Same effect, and the path Effort already uses |
| D-3 | **A sibling table, `persona_work_modes`** | A Persona-file field; a column on `persona_models` | Model and Effort stay out of the file. A new column never reaches an upgraded database. rules.md |
| D-4 | **`WorkMode` on the Persona, `Mode` in the ACP layer** | One name for both | `Mode` already means light and dark, the card state and `SystemPromptMode` |
| D-5 | **Applied after Model and Effort, read back** | Applied first; trusting the request | `auto` depends on the model, and the Adapter clamps silently (§6.3) |
| D-6 | **A change restarts the Persona** | A live switch now | Consistent with Model and Effort, and needs no wire change. Phase 2 removes the cost where it matters |
| D-7 | **Keep `default`; do not apply Effort's filter** | Filtering it out | It is Manual, a real mode, not a sentinel (ADR-0033) |
| D-8 | **Hide `bypassPermissions` and `auto` by default, enforced in `BuildOptions`** (`plan` joined them in D-17) | Offer everything; hide in the picker only | Bypass is advertised on Windows. Auto hands decisions to the model and changes with it. A hand-edited row must be inert |
| D-9 | **Only the Human sets it** | A `Candidate` field; a frontmatter field | It grants what runs unasked. `Candidate` already cannot set Model, Effort or Adapter |
| D-10 | **The plan guard refuses exit for a `plan` Persona** | Leaving the handler as is | The handler's own `allow_once` preference exits plan mode on the first request |
| D-11 | **The guard keys on the id `"plan"`** | A per-Adapter table of plan-mode ids | A wrong id leaves the guard inert, which is today's behaviour. A table is unwarranted |
| D-12 | **Modes share the effort probe and cache entry** | A separate probe | One spawn yields both, and a probe is up to 20 seconds |
| D-13 | **The hidden list defaults in `WorkModePolicy`** | An initialiser on the option | The configuration binder merges arrays by index into a pre-populated one |
| D-14 | **Match on Work Mode when resuming** | Ignoring it | A session opened in one mode must not be resumed for a Persona now set to another |
| D-15 | **No new `AgentEvent` type** | `ConfigOptionsChanged` | `ModeChanged` already exists and `ConsoleRenderer` needs no change |
| D-16 | **Phase 3 is non-blocking** | Answering the exit request with the Human's choice | A waiting request needs the idle watchdog paused and a Room bound to the session (roadmap §20). ADR-0022 |
| D-17 | **Hide `plan` by default** (added 2026-10-01) | Offering `plan` now, the first design | A live run (OQ-2) showed a refused plan exit ends the Turn cancelled with no plan text, and `RoomSession` posts no cancelled Turn, so a plan Persona is silent. The plan is only in `rawInput.plan`. The guard stays for an operator who lifts the block; phase 3 is the prerequisite for offering it again |

---

## 13. Open questions

Run these before the code that depends on them. OQ-4 and OQ-1 were answered on 2026-09-30 by a
script that drove the vendored adapter over stdio (task WM-0). OQ-2 and OQ-3 were answered on
2026-10-01 by the same kind of script, run on Haiku (one run each, Windows, a Claude subscription,
outside the Huddle app, answering permission requests the way Huddle's handlers would). The in-app
manual tests stay the acceptance run.

| # | Question | What it decides | How |
| --- | --- | --- | --- |
| OQ-4 ✓ | Does the probe's `session/new` response carry the mode option, with the values in §6.1? | The whole catalog design | **Yes.** `id: "mode"`, `category: "mode"`, the five values in §6.1, each with a `description` and an adapter-specific `_meta.kind`. The legacy `modes` field carries the same ids. Recorded in [acp-session-config.md](engineering/acp-session-config.md) |
| OQ-1 ✓ | Does a resumed session come back in Manual? The source says so | Whether §6.3 step 3 ever fires, and phase 2 | **Yes.** A session set to `acceptEdits` resumed as `default`. A session that never ran a prompt cannot be resumed at all (`-32002`), so this needs one turn, not none. Recorded in [known-limits.md](engineering/known-limits.md) |
| OQ-2 ✓ | After "No, keep planning", does the reply contain the plan? | Whether `plan` may ship before phase 3. If not, add `plan` to the default hidden list | **No.** The Adapter asked a `switch_mode` "Approve Plan" request with the plan in `rawInput.plan`. The guard refused with `reject_once`; the Turn ended `cancelled` with only 126 characters of preamble, and `RoomSession` posts no cancelled Turn. `plan` is now in the default hidden list (D-17). §6.9. Re-check in-app with MW-3 |
| OQ-3 ✓ | Under Accept edits, does a write outside the Work Dir still reach the handler? | Whether the `~/.claude` guard survives the mode | **Yes.** A write outside the Work Dir produced a `session/request_permission` (kind `edit`, title "Write <path>", `rawInput` `{file_path, content}`). Refused, the file was not written. `WorkDirPermissionHandler` reads `rawInput.file_path`, so its `~/.claude` refusal still sees such writes. Caveats: the path was a temp directory, not `~/.claude` itself, and `allow_always` was never chosen. Re-check in-app with MW-4 |
| OQ-5 ✓ | Where does the message chunk from an `auto` clamp land when no Turn is running? | Whether `auto` may ever be offered | **It arrives as a `session/update` notification.** `auto` on a model without support (Haiku, with no Turn running) answered `acceptEdits` in the `set_config_option` response, then sent an `agent_message_chunk` reading "Auto mode unavailable: the selected model does not support Auto mode; using Accept edits instead." and a `current_mode_update` to `acceptEdits`. Opus, Sonnet and the default accepted `auto`. Setting `auto` on Opus and then switching the model to Haiku moved the mode to `acceptEdits` with only a `current_mode_update`, no message chunk. Run 2026-10-01 with no prompt, so it cost nothing |

---

## 14. As built, 2026-09-30

Phase 1 is built and the whole solution passes (5,052 tests, 0 failed). Tasks WM-0 to WM-I11 and
WM-G1 are done, and no golden file changed. These are the places the build differs from the design
above, each found by a test or by reading the code.

| Where | Designed | Built | Why |
| --- | --- | --- | --- |
| §6.6 hidden list | `[]` hides nothing | A list of one empty string hides nothing; an empty value is read as absent | The configuration binder cannot express an empty list. A test caught it |
| §6.6 hidden list | `WorkModePolicy(IOptions<AcpOptions>)`, `string[]?` | `WorkModePolicy(IOptions<TeamOptions>)`, `IReadOnlyList<string>?`, plus `EffectiveMode` | The repo reads `AcpOptions` through `TeamOptions`, and `Args` already uses `IReadOnlyList<string>?`. `EffectiveMode` is what `BuildOptions` calls |
| §6.8 enforcement | Inline in `BuildOptions` | `WorkModePolicy.EffectiveMode` and `DotAcpPersonaHost.GuardPermissions` | `BuildOptions` takes twenty constructor arguments to reach, so each decision is a small internal method a test can call. An end-to-end conformance test covers the wiring |
| §6.10 resume match | `WorkMode` after `Effort` | A trailing optional parameter | 27 positional constructions would otherwise change. Old files load as null, as designed |
| §6.7 catalog | `AdapterProbeOutcome` gains `Modes` | An `init` property | Every fake builds the two-argument form |
| §6.11 card | Closed select shows the stored value | Shows the mode's **name** (`ToStringFunc`), and the id while the catalog is unread | An id such as `acceptEdits` is for the wire. Model and Effort still show ids |
| §6.11 card | Adapter change clears it with a note | A separate `role="status"` note beside the Model and Effort note | The existing note's text is pinned by tests, and a third clause would have moved it |
| §6.11 card | Hidden when the catalog is empty and nothing is stored | The same, but shown (disabled) while the probe is out | Hiding it while unread would make it pop in when the probe lands |
| §13 OQ-1 | "Free" | Needed one prompt attempt | A session that never ran a prompt cannot be resumed (`-32002`) |
| §6.6 hidden list (2026-10-01) | `["bypassPermissions", "auto"]` | `["bypassPermissions", "auto", "plan"]` | OQ-2: a refused plan exit ends the Turn cancelled and nothing is posted (D-17). The picker by default lists Manual and Accept edits only |

**Also found.** The mode option's choices carry an adapter-specific `_meta.kind`: `standard`,
`plan`, `auto_review` and `full_access`. It is not part of the ACP spec, so nothing here depends on
it. It could replace the two ids held in code (`plan` for the guard, and the hidden list) if more
than one Adapter ever advertises modes; that is the point at which D-11's "no per-Adapter table"
should be revisited.

**Run live, 2026-10-01.** OQ-2, OQ-3 and MW-2 at wire level were answered by a script that drove the
vendored `claude-agent-acp` 0.75.1 over stdio on Haiku (Windows, a Claude subscription, one run
each), outside the Huddle app. OQ-2: the plan does not reach the Room, so `plan` is hidden by
default (D-17). OQ-3: the write guard survives Accept edits. MW-2: under Accept edits an edit
inside the Work Dir produced no permission request, the file was written, and the Turn ended
`end_turn`. The facts are real wire facts; the in-app runs stay the acceptance.

**OQ-5, run 2026-10-01 with no prompt.** `auto` on a model without support (Haiku, with no Turn running) answered `acceptEdits` in the `set_config_option` response, then sent an `agent_message_chunk` reading "Auto mode unavailable: the selected model does not support Auto mode; using Accept edits instead." and a `current_mode_update` to `acceptEdits`. Opus, Sonnet and the default accepted `auto`. Setting `auto` on Opus and then switching the model to Haiku moved the mode to `acceptEdits` with only a `current_mode_update`, no message chunk. So the clamp is visible in the read-back (§6.3 step 6),
and the notice chunk is dropped by `RoomSession` unless a Turn happens to be open. `auto` stays hidden by
default: a model switch can still move the mode silently, and the in-app behaviour is untried.

**Not run.** The in-app manual tests in
[manual-tests/work-mode.md](engineering/manual-tests/work-mode.md).

---

## Appendix A — Test-first task plan

Each `.t` task ends red for the right reason. Each `.i` task ends with `dotnet test Huddle.slnx
--` green. The trailing `--` is required. Test names follow `Method_Scenario_Expectation`. Each
new test copies the named effort test.

| # | Kind | Task | Done when |
| --- | --- | --- | --- |
| WM-0 ✓ | Spike | OQ-4 and OQ-1 from §13. Record the real option and the resume result in `acp-session-config.md` and `known-limits.md` | The two answers are written down. **Done 2026-09-30** |
| WM-T1 | Unit | `ModeConfigOptionsTests`, copying `EffortConfigOptionsTests`: flat, grouped, no mode option, null, model category only, `default` kept, `TryResolve` match, not in catalog, no select, and the configId is the option's own (use `permission` as the id) | Fails |
| WM-I1 | Impl | `AgentModeOption`, `ModeConfigOptions` | T1 green |
| WM-T2 | Unit | `AgentSessionOptionsTests.Mode_DefaultsToNull` and `Mode_BlankIsNormalisedToNull` | Fails |
| WM-I2 | Impl | `AgentSessionOptions.Mode` | T2 green |
| WM-T3 | Unit | `DotAcpAgentHostModeTests`, copying `DotAcpAgentHostEffortTests`: catalog mapped; no mode option starts anyway; grouped; not in catalog warns; Adapter rejects, session usable; null sends nothing; writes `configId` and `select`; **sets Model, then Effort, then Mode**; resolves against the post-model snapshot; a clamped response warns and reports the effective value; resume applies when different; resume skips when equal | Fails |
| WM-I3 | Impl | `DotAcpAgentHost`, the `IAgentSession` members, `FakeAgentSession` | T3 green |
| WM-T4 | Unit | `SessionUpdateMapperTests`: a `ConfigOptionUpdate` with a mode maps to `ModeChanged`; one without stays `UnknownUpdate`. The `AvailableCommandsUpdate` case, which Adapter Commands (ADR-0035) maps to `AvailableCommandsUpdated`, and its tests are not touched; the new case is added beside it, never in its place | Fails |
| WM-I4 | Impl | `SessionUpdateMapper` | T4 green |
| WM-T5 | Unit | `PersonaWorkModeStoreTests`, copying `PersonaEffortStoreTests`, **including** the pre-existing-database case. `PersonaStoreTests`: join, rename moves the row, remove deletes it. `TeamMembershipTests.Edit_PreservesWorkMode` | Fails |
| WM-I5 | Impl | `Persona.WorkMode`, the store, DI, `PersonaStore`, and the callers in §6.5 | T5 green |
| WM-T6 | Unit | `WorkModePolicyTests`: key absent, an empty value, a list of one empty string and a list all bind through `IConfiguration` from an in-memory source, and give the meanings in §6.6; ordinal comparison | Fails |
| WM-I6 | Impl | `AcpOptions.HiddenModes`, `WorkModePolicy` | T6 green |
| WM-T7 | Unit | `ModelCatalogCacheTests`, copying its effort tests: modes share the effort probe (one probe, not two); a failed probe is not cached; hidden modes are filtered out and the cache is unfiltered; `default` is kept | Fails |
| WM-I7 | Impl | `AdapterProbeOutcome`, the probe runner, `IModelCatalog`, `ModelCatalogProbe`, both fakes | T7 green |
| WM-T8 | Unit | `PlanModePermissionHandlerTests`: refuses `SwitchMode` under `plan`; `RejectAlways` fallback; `Cancelled` when neither is offered; delegates every other kind; delegates `SwitchMode` when not in `plan`; delegates a kind-`other` request | Fails |
| WM-I8 | Impl | `PlanModePermissionHandler` | T8 green |
| WM-T9 | Unit | `DotAcpPersonaHostTests`: the mode reaches the options; a hidden mode is dropped with a warning; null passes null; the guard wraps only for `plan` | Fails |
| WM-I9 | Impl | `BuildOptions` | T9 green |
| WM-T10 | Unit | `RoomSessionResumeTests`: the entry stores the mode; add a mode case to `Reopen_ModelEffortOrAdapterChanged_NeverResumes`; an old entry with no `workMode` still resumes for a Persona with none | Fails |
| WM-I10 | Impl | `RoomSessionEntry`, `RoomSession`, prose | T10 green |
| WM-T11 | Unit | `PersonaSupervisorLifecycleTests`: a changed Work Mode restarts the host; an unchanged one does not | Fails |
| WM-T12 | bUnit | `TeammateCardWorkModeTests`, copying `TeammateCardModelEffortTests`: offered in create mode; the stored mode preselected; a stored mode not in the catalog is still offered; an Adapter change clears it with `role="status"`; inert while loading; the view paper; hidden when the catalog is empty and nothing is stored; no `role="alert"` | Fails |
| WM-I11 | Impl | `TeammateCard.razor`, `Teammates.razor` | T12 green |
| WM-G1 | Guard | `PromptGoldenTests` passes with no golden change | Passes |
| WM-D | Docs | Appendix B | Reviewed |

## Appendix B — Documentation to update

- **`language.md`**: define **Work Mode**, *Avoid*: mode alone, permission mode, and ACP mode.
  Extend **Persona** to name the third table.
- **[ADR-0015](adr/0015-model-and-effort-reset-when-the-adapter-changes.md)**: a line saying
  ADR-0033 extends the reset to Work Mode. **ADR-0033** is `accepted`.
- **`rules.md`**: the resume match row gains Work Mode. New rows: modes are enforced in
  `BuildOptions`, and Work Mode keeps Adapter's `default` while Effort filters it.
- **`acp-session-config.md`** (the ACP effort's): the `mode` row becomes "Yes", and its
  `src/Team.Acp/` links are stale. Announce first.
- **`known-limits.md`**: the answers to OQ-1, OQ-2 and OQ-3.
- **`acp-agent-guide.md`** §3.4 (the ACP effort's) is stale on `rawInput` at request time.
  Roadmap §20 already records this; correct it in the same change as the ACP layer.
- **`code-map.md`**, **`Huddle.EngineeringGuide.md`** (map row), **`roadmap.md`** (a new item), and
  `manual-tests/work-mode.md` with its index row.
