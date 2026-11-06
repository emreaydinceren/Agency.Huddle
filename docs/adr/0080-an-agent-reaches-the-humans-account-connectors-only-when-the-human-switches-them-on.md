---
status: accepted
date: 2026-11-06
---

# An Agent reaches the Human's account connectors only when the Human switches them on

Every Agent session lists the Human's claude.ai connectors beside `mcp__team__`: mail, calendar, files, Teams. In the first
from-zero run all 11 Agents did, including `outlook_send_mail` and `outlook_batch_delete_messages`, and no Agent used them
(issue #357). Nothing in Huddle limited them, and the permission handler approves a call it has no guard for, so a connector
call would run with nobody asked. The connectors also add about 3,900 tokens of context to every Turn from the second.

## Decision

**Account connectors are off for every Agent. The Human can turn them on for one Agent, on its card, and only there.**

| | Decision |
| --- | --- |
| Default | Off, for every Agent, including the Chief of Staff and hand-written Agents |
| Mechanism | `_meta.claudeCode.options.env.ENABLE_CLAUDEAI_MCP_SERVERS = "false"` on the session; `strict-mcp-config` is not used |
| Who turns it on | The Human, on the Agent card. An Agent cannot: the setting is not in its file |
| Where it is stored | `persona_connectors` in `team.db`, like the Work Mode, written only by `PersonaStore.SetConnectors` |
| Scope | All connectors or none. Per-connector choice is a later option |
| `requires`, `connectors.json` | Grant nothing |
| Copying | A Team duplicate, `propose_agents` and Pack updates never carry the setting |
| Timing | A change restarts the Agent's host; the next Turn after takes it |
| Other Adapters | The card says what Huddle cannot control instead of claiming a protection |

A connector call under the switch stops for the Human's approval (spec OQ-1, the owner's decision), and the switch does not ship
before it does. Every Agent goes off by default first, so nothing waits on the prompt to close today's exposure.

## Why

The spike (stage 1 of #357, 0.77 USD, `acp-session-config.md`) showed the variable alone removes every connector for a
session, leaves `mcp__team__`, `git`, `dotnet` and sign-in working, and takes effect on a resume in a new Adapter process.
This changes a default about access: an install where Agents used connectors loses that use on upgrade.

## Rejected

| Alternative | Why not |
| --- | --- |
| Off for everyone, no switch | No Agent could ever use the Human's mail or calendar, including roles meant to |
| Per-connector choice now | Needs a list of connectors and upkeep; recorded as later |
| Leave on and document | Every Agent keeps access to the Human's accounts |
| `strict-mcp-config` as well | Hides the Work Dir's own `.mcp.json` servers (spike Q3) |
| The setting in frontmatter | An Agent or a proposal can write it |

## Consequences

- Existing Agents lose their connectors at the first start after upgrade. A release note and (spec OQ-3) a one-time notice say so.
- Agents cost about 11 percent less context per Turn from Turn 2.
- The switch's protection holds only on the stock `claude-agent-acp` Adapter.
- A shell command can still write `team.db`: the guards see path arguments only. This is the existing soft boundary.
