# ACP session config options: models and effort

This page explains how to discover which models and thinking-effort levels an ACP
agent offers, and how to select them. It is for anyone working on Team's ACP
bridge (`src/Team.Acp`) or writing another client that drives an ACP agent over
stdio. [How Team does it](#how-team-does-it) covers Team's own implementation,
including the two decisions this doc otherwise leaves open.

ACP has no `models/list` or `effort/list` method. Both catalogs arrive as a
**side effect of the session handshake**: you call `initialize`, then
`session/new`, and read a `configOptions` array out of the `session/new` result.
Selecting a value uses that same array, so one round trip answers both
"what is available" and "how do I choose".

> [!NOTE]
> Verified against `@agentclientprotocol/claude-agent-acp` as vendored in
> `tools/acp/node_modules` and `dotacp.protocol` 2026.7.19, September 2026. The
> parallel `models` / `session/set_model` field described under
> [The unstable path](#the-unstable-path) is still marked unstable in the spec.
> This page covers config options only — not authentication, prompt turns, or MCP
> wiring. See [agent-guide.md](agent-guide.md) for those.

## The shape of a config option

Every knob an agent exposes is one entry in `session/new`'s `configOptions`
array, discriminated by `category`. A `select` entry looks like this:

```json
{
  "id": "effort",
  "name": "Effort",
  "description": "Available effort levels for this model",
  "category": "thought_level",
  "type": "select",
  "currentValue": "default",
  "options": [
    { "value": "default", "name": "Default" },
    { "value": "low", "name": "Low" },
    { "value": "high", "name": "High" },
    { "value": "max", "name": "Max" }
  ]
}
```

Four categories are defined, and `dotacp.protocol` exposes each as a
`SessionConfigOptionCategory` member:

| `category` | Selects | Team support |
| --- | --- | --- |
| `model` | Which model the session runs | Yes — [ModelConfigOptions.cs](../../src/Team.Acp/DotAcp/ModelConfigOptions.cs) |
| `thought_level` | Thinking effort for the current model | Yes — [EffortConfigOptions.cs](../../src/Team.Acp/DotAcp/EffortConfigOptions.cs) |
| `mode` | Agent mode (e.g. plan vs. edit) | No |
| `model_config` | Provider-level model configuration | No |

**Enumerate** by filtering the array to the category you want and reading
`options[]`; the active value is in `currentValue`. **Select** by sending
`session/set_config_option` with that entry's own `id` and one of its option
`value`s.

## Enumerating the catalog

Team reads the model catalog during session creation in
[DotAcpAgentHost.cs](../../src/Team.Acp/DotAcp/DotAcpAgentHost.cs), and exposes it
as `IAgentSession.Models`:

```csharp
IReadOnlyList<AgentModelOption> models = ModelConfigOptions.Read(response.ConfigOptions);
```

An agent may advertise a category, advertise it with an empty option list, or
omit it entirely. Treat "omitted" as **unknown**, not as "none available" —
several adapters take their model from config or CLI flags and advertise nothing
over the wire. `ModelConfigOptions.Read` returns an empty list for that case
rather than throwing.

To see a real catalog from the command line without writing code,
[block/buzz](https://github.com/block/buzz) ships a subcommand that performs
exactly this handshake and exits:

```bash
buzz-acp models --json --agent-command claude-code-acp
```

```json
{
  "agent": { "name": "claude-code-acp", "version": "0.6.1" },
  "stable": {
    "configOptions": [
      {
        "id": "model",
        "category": "model",
        "options": [
          { "value": "claude-opus-5", "name": "Opus 5" },
          { "value": "claude-sonnet-5", "name": "Sonnet 5" }
        ]
      }
    ]
  },
  "unstable": { "currentModelId": "claude-sonnet-5", "availableModels": [] }
}
```

That subcommand prints models only — it filters to `category == "model"`, so the
effort option never reaches its output. Its human-readable (non-`--json`) mode
also reads each option's label from `displayName`, which the schema does not
define — the required fields are `value` and `name` — so those labels silently
fall back to the raw id. Read `name`.

## Selecting a value

Resolve the value you want against a **fresh** `session/new` response, then send
`session/set_config_option` with the owning entry's `id`:

```csharp
if (ModelConfigOptions.TryResolve(response.ConfigOptions, options.Model, out SessionConfigId configId, out SessionConfigValueId value))
{
    await activeConnection.SetSessionConfigOptionAsync(
        new SetSessionConfigOptionRequest
        {
            SessionId = sessionId,
            ConfigId = configId,   // the option's own id — never hardcoded
            Type = "select",
            Value = value,
        },
        cancellationToken).ConfigureAwait(false);
}
```

A value that matches nothing is not worth aborting for. Team logs
`Requested model '{Model}' is not in the agent's advertised model catalog` and
continues on the agent's default, so a stale stored setting cannot brick a
session. Buzz behaves the same way.

Register the session with the connection's adapter **before** sending the first
`set_config_option`: the call can provoke a `session/update` carrying a
`ConfigOptionUpdate`, and an unregistered session would drop it.

## How effort works

Effort is a `thought_level` select whose id is `effort` on Claude Code's adapter.
Four behaviours make it different from the model option, and a client that
ignores them will show the user a wrong or stale value.

**It is model-dependent, and may be absent.** The adapter emits the option only
when the *current* model reports `supportsEffort` with a non-empty
`supportedEffortLevels`. Select a model without effort support and the entry
disappears from `configOptions` entirely.

**It is rebuilt on every model switch.** Switching models re-runs the option
build with the new model's levels. A pinned effort the new model does not support
is silently clamped back to `"default"`. A client must therefore re-read
`configOptions` after a model switch rather than caching the pre-switch list —
buzz reads its *post-switch* snapshot for exactly this reason.

**`"default"` is a real sentinel, not a placeholder.** It is always the first
option. Selecting it clears the adapter's flag layer and hands effort resolution
back to the Claude Code CLI's persisted per-model setting; selecting anything
else pins effort for the session.

**A successful set is not echoed in the snapshot you already hold.** The
`session/new` response you captured still carries the old `currentValue`. Buzz
patches the accepted value into its cached snapshot so its UI reflects what the
session is really running. Team needs the same, or it must re-read instead of
caching.

> [!WARNING]
> Do not hardcode `"effort"` as the config id. It is adapter-defined; discover it
> by scanning for `category == "thought_level"` on every session. Adapters also
> disagree on the key that holds it — the ACP spec says `configId`, Claude Code's
> adapter emits `id` — so read both and always write `configId` on the wire.

The effort vocabulary itself is the adapter's business. Buzz deliberately does
not validate it for Claude or Codex: any string passes through and the adapter
rejects what it does not know. A new level then works the day the adapter ships
it, at the cost of no pre-flight validation.

## How Team does it

Team reads and applies both `model` and `thought_level`. The ACP-layer pieces are
[`AgentEffortOption`](../../src/Team.Acp/Abstractions/AgentEffortOption.cs),
[`SessionConfigSelects`](../../src/Team.Acp/DotAcp/SessionConfigSelects.cs) (the
protocol quirks shared with `ModelConfigOptions`, factored out rather than
duplicated), [`EffortConfigOptions`](../../src/Team.Acp/DotAcp/EffortConfigOptions.cs),
and `DotAcpAgentHost.ApplyEffortAsync`. Two decisions the sections above do not
answer, because they only describe a session that already exists:

**The effort catalog is discovered per model.** Nothing above says how a client
learns a model's ladder *before* it starts a real session — every example reads
`configOptions` off a session already running. Team reuses
[`AgentSessionOptions.Model`](../../src/Team.Acp/Abstractions/AgentSessionOptions.cs)
and `IAgentSession.EffortLevels`:
[`ModelCatalogProbe`](../../src/Team.App/Acp/ModelCatalogProbe.cs) starts a
throwaway session **with that model selected** and reads the post-switch list
straight off it, caching the result per model id.

**The adapter's `"default"` sentinel is filtered at the app layer, never in
`Team.Acp`.** `EffortConfigOptions.Read` does not drop it —
`IAgentSession.EffortLevels` must stay a faithful report of what the agent
actually advertised, because a protocol reader that silently drops an
advertised option is exactly the failure class
[traps.md](../agencyteam/traps.md) exists for.
`ModelCatalogProbe.WithoutAdapterDefault` does the filtering instead, one layer
up. The Teammate card's blank "Use the agent's default" option *is* that
filtered-out choice, and it stores `null`, which means no
`session/set_config_option` is sent for effort at all.

The two items flagged above as needing a decision before this doc could answer
them both resolved to **no new mechanism**:

- **Live mid-session effort switching is not built.** A system prompt and a
  Model are already fixed at `session/new`
  ([traps.md](../agencyteam/traps.md)), and Team's established answer is
  restart-on-change; Effort joins them rather than getting its own path. Buzz
  has none either.
- **`currentValue` staleness does not arise.** Team never caches a
  `currentValue` at all — the Teammate card shows the **stored** choice, not
  the session's live value. The visible consequence: if the adapter clamps a
  requested level back to `"default"`, the card will not say so. That is
  recorded in [known-limits.md](../agencyteam/known-limits.md) rather than
  worked around.

## The unstable path

Alongside `configOptions`, some agents also return a `models` object — ACP's
`SessionModelState`:

```json
{
  "currentModelId": "claude-sonnet-5",
  "availableModels": [{ "modelId": "claude-opus-5", "name": "Opus 5" }]
}
```

Its counterpart setter is `session/set_model { sessionId, modelId }`. This path is
still marked unstable, has no effort equivalent, and Team does not read it. Prefer
`configOptions`, and fall back to `models` only for an agent that advertises
nothing else — buzz checks them in that order.

## Troubleshooting

**The agent returns no `configOptions` at all.** Check whether it needs
`authenticate` before `session/new`. Some adapters return a usable but
option-free session until authentication completes.

**Responses arrive out of order, or the process hangs.** Do not write all the
handshake requests at once and read afterwards. Agents interleave unsolicited
notifications with replies; read and correlate by JSON-RPC `id` as you go.

**An effort value that worked yesterday is ignored.** Most likely the session is
on a different model that does not support it — the option is silently clamped to
`"default"` rather than erroring. Re-read `configOptions` and check whether the
`thought_level` entry is present at all.
