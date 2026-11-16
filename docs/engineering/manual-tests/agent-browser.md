# Agent browser

Prove that every Agent has its own browser with no action by the Human, that the Human can switch it off, that reading a page runs freely and every click or typing asks first, and that a sign-in the Human types into the Agent's window stays with that Agent. Spec: [Huddle.AgentBrowser-Specifications.md](../../Huddle.AgentBrowser-Specifications.md). Decision: [ADR-0090](../../adr/0090-an-agent-has-its-own-browser-through-playwright-mcp.md).

**11 tests** · 0 free · 11 paid · about 1.8 hours. Written 2026-11-16 as a stub for issue #551: the cases and what each proves are set, and the starting data, steps and Pass-if lines are written with the paid run (slice 8). **Not yet run.**

> **Paid, Sonnet minimum.** Every case sends real Turns. Use Sonnet or better for each Agent, never Haiku. Budget stop: 5 USD. Ask the owner first, and record each result in the [Run log](run-log.md).

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names.

## Tests

### BROWSER-01 — A new Agent has the browser on, with no action 💰

**Paid** · about 10 min

*Proves the default: the Browser row shows On with no action by the Human, and the session log lists `mcp__playwright__` tools.*

### BROWSER-02 — Switching off removes the server 💰

**Paid** · about 10 min

*Proves that after the restart the Agent has no `playwright` tools and no `node cli.js` process runs for it.*

### BROWSER-03 — The Agent summarises a page that needs JavaScript 💰

**Paid** · about 10 min

*Proves a page `WebFetch` cannot read is read through the browser.*

### BROWSER-04 — A sign-in the Human types, and a page behind it 💰

**Paid** · about 15 min

*Proves the Human signs in to one site in the visible window and the Agent then reads a page behind that sign-in.*

### BROWSER-05 — A click asks, and Refuse stops it 💰

**Paid** · about 10 min

*Proves Allow once runs the click, and Refuse stops it with the Agent reporting the refusal without retrying.*

### BROWSER-06 — A type step's card shows the exact text 💰

**Paid** · about 10 min

*Proves the approval card for typing shows the exact text the Agent would type.*

### BROWSER-07 — A Team duplicate keeps a member's browser off 💰

**Paid** · about 10 min

*Proves that duplicating a Team with one member off gives a copy that is off.*

### BROWSER-08 — Resetting the Chief of Staff turns the browser on 💰

**Paid** · about 5 min

*Proves a reset to default returns the switch to on.*

### BROWSER-09 — Work Modes: `acceptEdits` asks, a mode that never asks has no server 💰

**Paid** · about 10 min

*Proves `acceptEdits` still asks, and that with `bypassPermissions` offered through `HiddenModes` no server is configured.*

### BROWSER-10 — The tool-call log holds the Playwright calls 💰

**Paid** · about 5 min

*Proves `logs/tool-calls/<agent>/` holds the Playwright calls with their arguments.*

### BROWSER-11 — Two Rooms at once: the second says the browser is in use 💰

**Paid** · about 10 min

*Proves one Agent in two Rooms at once gets an error in the second whose text matches the Skill ("already in use").*
