---
status: accepted
date: 2026-11-16
---

# An Agent has its own browser through Playwright MCP

Agents had no browser. A QA Engineer accepted a web UI Task with "I did not exercise the UI in a browser", and no Agent
could read a page that needs JavaScript (`WebFetch` runs no script). The owner also wants Agents to work inside sites the
Human signs into, such as email. The Human's own browser profile must never be used for this (issue #551).

## Decision

**Every Agent has its own browser, on by default. The Human can turn it off per Agent, on its card, and only there. Every
call that acts on a page asks the Human first.**

| | Decision |
| --- | --- |
| Mechanism | Microsoft's Playwright MCP server (`@playwright/mcp`), pinned at an exact version in `tools/acp/package.json` beside the Claude Adapter and installed by the same step. It runs on Huddle's own Node (`Team:Acp:Command`). Never `npx` at run time |
| How it reaches the session | In the session `_meta`, as `options.mcpServers.playwright`. Nothing is written to disk, so an Agent cannot remove it or change its arguments. No `.mcp.json` |
| Default | On for every Agent. The browser starts only when the Agent first calls a browser tool, so an Agent that never browses opens no window |
| Who changes it | The Human, on the Agent card. The setting is not in the Agent's file and no Agent tool, `propose_agents` or Pack update writes it |
| Where it is stored | `persona_browser_off` in `team.db`. A row means the Human switched it off; no row means on |
| Approval | Read tools run freely. Every other call asks through the connector approval card. An unknown Playwright tool asks. `browser_run_code_unsafe` is denied outright, so it is never offered |
| Profile | One persistent profile folder per Agent, `<DataDir>/browser-profiles/<Name>`. Deleted with the Agent. Sign-ins stay for that Agent only |
| Window | Visible. The Human types a password or code into that window; the Agent never types it |
| Sessions | One stdio server per session. A second session of the same Agent fails with "Browser is already in use" while the first holds the profile |
| Linux with no display | The server runs headless, public pages work and sign-in does not. The card says so |
| Work Modes | Ignored in a Work Mode that never asks before a tool runs (`bypassPermissions`, `auto`): the server is not configured, as for connectors (ADR-0080) |
| Adapters | Claude Adapter with isolation only. Another Adapter, or a non-isolating Claude profile, gets no browser |

The design is in [Huddle.AgentBrowser-Specifications.md](../Huddle.AgentBrowser-Specifications.md). The `_meta` route, the
`mcp__playwright__*` ask rule and the env pin were measured in a spike before the rest was built; the numbers are in
that page.

## Why

- **Playwright MCP already is the tool set.** Navigate, page snapshot, click, type, screenshot, tabs and a profile option
  come with it. Huddle adds only the switch, the entry it passes, the approval rule and the text.
- **The `_meta` route cannot be edited by the Agent.** A `.mcp.json` in the Work Dir is a file the Agent can rewrite. The
  `permissions.ask` list is protected the same way, because it is also in the `_meta` (ADR-0080).
- **Acting is where the harm is.** Reading a page changes nothing. A click can send, buy or delete, so each one asks and
  the card shows the exact arguments, including the text to be typed.
- **One profile per Agent.** Sign-ins stay with the Agent that was given them, and a later Agent with the same name never
  inherits them, because deleting the Agent deletes the profile.
- **On by default is safe enough.** Acting asks regardless, the browser starts on first use, and the Human can switch it
  off. The cost is an idle node process (95 to 125 MB) in every isolating session, which `Team:Browser:Enabled` removes
  for the whole install.

## Rejected

| Alternative | Why not |
| --- | --- |
| Custom C# browser tools with `Microsoft.Playwright` | Huddle would write and keep up the tools, the profile handling and the browser launch that Playwright MCP already has. The owner chose the existing server |
| A project `.mcp.json` in the Agent's Work Dir | The Agent can edit or remove it, point it at another profile or turn on more options. It would also need approval settings to load, and it can clash with the Agent's own file |
| One shared Playwright MCP over loopback HTTP | One window shared by every Room, Rooms disturbing each other, and a port with no authentication: any local process could drive the signed-in browser with no approval |
| One profile per Room session | No lock clash, but a sign-in in one Room is not seen in another, which defeats signing in once |
| `--isolated` (a throwaway profile) | Loses every sign-in when the browser closes |
| `--allowed-origins` as the limit | Playwright's own README says it is not a security boundary |
| Off by default | The owner decided on (2026-11-16). Acting still asks; a per-role default is a possible v2 |

## Consequences

- Every isolating session starts one MCP node process, with a browser or not. It is about 95 to 125 MB idle.
- A browser is 8 to 10 processes and about 490 to 510 MB while it is open. `--idle-timeout` closes it after 30 minutes.
- The Human watches and signs in on the PC that runs Huddle. There is no live view from another device.
- Reading tools run freely, so `browser_network_requests` and `browser_console_messages` can show the model a signed-in
  site's tokens. This is recorded in [known-limits.md](../engineering/known-limits.md).
- An Agent with `Bash` can still read the profile folder or run Playwright itself. The switch governs only the server
  Huddle configures.
- The read list is pinned to the package version. A version bump that adds a tool fails a test until someone sorts it.
- One Agent in two Rooms at once cannot browse in both: the second gets "Browser is already in use".
