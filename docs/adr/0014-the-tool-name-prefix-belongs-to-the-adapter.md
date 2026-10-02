---
status: accepted
date: 2026-09-16
---

# The tool-name prefix belongs to the Adapter

Every App Tool is named twice. `AppToolServer` advertises it over MCP under the tool-server
name `team`, and the system prompt tells the model what to type. Until now those two names were
locked together by a constant: the server was called `team`, so the prompt said
`mcp__team__get_help`, and [Rules](../engineering/rules.md) made it binding that *"App Tool names
must be spelled `mcp__team__*` in the system prompt"*.

That rule exists because the failure it prevents is silent and expensive. Naming a tool
imprecisely does not raise an error — the model reports that no such tool exists, burns a Turn,
and learns not to trust the catalog. `get_help`'s own output is the only place many Agents ever
read a tool's name, so a wrong prefix propagates.

## Why one constant stopped being enough

`mcp__{server}__{tool}` is not an MCP convention. It is **`claude-agent-acp`'s** convention for
disambiguating tools from several MCP servers. Agency.NET's `McpClientPool` does not do it: it
surfaces a remote tool under the server's own name, unmodified. So against `agency-acp` the
model is offered `get_help`, and a prompt saying `mcp__team__get_help` names a tool that does
not exist — precisely the failure the rule was written to prevent, arriving through the rule
being obeyed.

## The decision

**The prefix is derived from the resolved Adapter Profile, in code, at session construction.**

```csharp
var toolNamePrefix = profile.UsesToolNamePrefix ? $"mcp__{ToolServerName}__" : string.Empty;
```

`ToolServerName` remains a single `private const string ToolServerName = "team";` and is still
handed unchanged to `AppToolServer`. **The MCP server name does not change.** What became
conditional is only whether the *model-facing* names carry it.

That distinction matters beyond tidiness. `CLAUDE.md` pins the `mcp__team__` tool prefix as one
of two identifiers that must **not** be renamed to match the Agency.Huddle brand. It is not
being renamed. It is being made conditional.

The prefix still flows to exactly two places — `GetHelpTool`'s constructor and
`SystemPromptComposer.Compose` — and is still never typed into a hook template. The sibling
rule, *"A Hook's text never contains `mcp__team__`; the prefix is filled in by code"*, survives
untouched and now holds **per Adapter**. A human still cannot misspell a tool into nonexistence.

## Two goldens, and one of them must never move

`PromptGoldenTests` pins the composed system prompt against a committed file. It now pins two:

| Golden | Profile | Names |
| --- | --- | --- |
| `systemPrompt.txt` | `UsesToolNamePrefix: true` | `mcp__team__get_help`, … |
| `systemPrompt.unprefixed.txt` | `UsesToolNamePrefix: false` | `get_help`, … |

The second golden asserts it contains no `mcp__` anywhere.

**The first golden did not change by a single byte**, and that is the load-bearing evidence for
this whole change. A stock installation configures no Adapters, so `AdapterCatalog` synthesises
one profile with `UsesToolNamePrefix: true`, so the expression above still evaluates to
`mcp__team__`. "Today's behaviour, byte for byte" is a claim a golden file can actually settle,
which is why the acceptance criterion for this work was framed as *an existing file must not
change*.

The two goldens differ in nothing but the tool names and the line-wrapping that follows from
shorter names. Diffing them is the clearest single statement of what this feature does.

## Consequences

- A profile's `UsesToolNamePrefix` flag now carries a second, unrelated-looking duty: it also
  gates whether `AgentProcessOptionsFactory` consults `AdapterLocator`. The coupling is
  deliberate and commented at both sites — a profile that does not take the `mcp__` prefix is
  not the Node adapter, and the locator knows only how to find the Node adapter. Without that
  gate, a bare-`Command` `agency` profile falls through and launches `claude-agent-acp` under
  the wrong name, with nothing thrown and nothing logged.
- Reversing this decision means re-pinning every golden, which is the bar this ADR exists to
  clear.
- A third Adapter with a third naming convention needs a third value, not a third branch: the
  flag is a `bool` today and would become a small enum or a format string. Nothing else moves.

## Rejected

**Renaming the tool server per Adapter.** The server name is the MCP identity `AppToolServer`
advertises and the bearer token is scoped to; changing it per profile would change the wire for
no benefit, and `CLAUDE.md` pins the name besides.

**Letting each Adapter's prompt be a separate hook template.** That puts `mcp__team__` back into
human-editable text, which is exactly what the sibling rule forbids and for a reason that has
not weakened.

**Detecting the convention at runtime from the agent's advertised tool list.** `session/new`
does not report how the agent will surface MCP tool names, and the prompt is composed before the
session exists. Configuration is the only thing available at the moment the decision is needed.
