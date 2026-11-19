# Account connectors

Prove that no Agent can use the Human's claude.ai account connectors unless the Human turned them on for that Agent on its card, that turning on asks first, and that the change reaches the Agent's session. Spec: [Huddle.AgentConnectors-Specifications.md](../../Huddle.AgentConnectors-Specifications.md) sections 14.2 and 15.

**13 tests** · 6 free · 7 paid · about 2.3 hours. Written 2026-11-06; **not yet run by a person**. AGCONN-08 needs the approval prompt change.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build). Each test names its own starting data. The paid tests send real Turns: ask the owner first and check the log at Turn 2 or later, never the model's reply.

## Tests

### AGCONN-01 — A new Agent has account connectors off

**Free** · about 5 min

*Proves the default: no row, the switch off, the helper line says so.*

**Before you start**

- Start a fresh lane (`P-BUILD`, an empty data folder). Hire or add any Agent.

**Steps**

1. Open the Agent's card (`P-OPEN-AGENT`).
2. Find **Account connectors** under Work mode.

**Pass if — all of these**

- The switch is off and the line under it reads "Off. {Name} cannot use your connected accounts."
- Open the **New agent** card (`P-NEW-PERSONA`, steps 1 and 2): it has no Account connectors row.

### AGCONN-02 — Turning on asks first, and Cancel leaves it off

**Free** · about 5 min

*Proves the confirm with its warning and that nothing is written before Give access.*

**Before you start**

- An Agent with connectors off.

**Steps**

1. Turn the switch on.
2. Read the question and the line above it.
3. Choose **Cancel**.

**Pass if — all of these**

- Step 2 shows "Give {Name} access to all your connected accounts?" and a line that ends "Each use will ask you first."
- After step 3 the switch is off, the confirm is gone and the Feed has no new item.

### AGCONN-03 — Give access restarts the Agent and records it in the Feed

**Free** · about 10 min

*Proves the grant: the host restarts, the card says since when, and the Feed records it.*

**Before you start**

- An Agent with connectors off.

**Steps**

1. Turn the switch on and choose **Give access**.
2. Watch the line under the switch, then the Agent status.
3. Open the Feed.

**Pass if — all of these**

- The line says "Restarting {Name} to apply this." while the Agent restarts, then "On since {today}. {Name} can use all of your connected accounts."
- The Feed shows "You turned on account connectors for {Name}", as an Activity item.

### AGCONN-04 — Turning off applies at once, and a rename drops access

**Free** · about 10 min

*Proves the off path and OQ-4.*

**Before you start**

- An Agent with connectors on (AGCONN-03).

**Steps**

1. Turn the switch off.
2. Open the Feed.
3. Turn it on again, then rename the Agent in Edit mode and save.
4. Open the renamed Agent.

**Pass if — all of these**

- Step 1 asks nothing and the line reads "Off."
- The Feed shows "You turned off account connectors for {Name}".
- After step 3 the renamed Agent shows the switch off.

### AGCONN-05 — An Adapter Huddle cannot control shows text, not a switch

**Free** · about 10 min

*Proves the switch claims nothing it cannot deliver (spec 7.3).*

**Before you start**

- Set `Team:Acp:Adapters` to a second Adapter that is not claude-agent-acp, and give one Agent that Adapter.

**Steps**

1. Open that Agent.
2. Read the Account connectors row.

**Pass if — all of these**

- There is no switch.
- The text reads "Huddle can't control {Adapter}'s account connectors." and says nothing about whether the Adapter loads connectors.

### AGCONN-06 — The one-time notice shows after an upgrade and stays dismissed

**Free** · about 10 min

> **Retired (2026-11-18).** The notice was removed with the Agents page that showed it: Huddle has no installs to
> upgrade yet. Nothing to run; skip this case.

### AGCONN-07 — A new Agent lists no claude.ai tool by Turn 3

**Paid** · about 15 min

*Proves P-1: the default holds in a real session log.*

**Before you start**

- A fresh lane and an Agent with connectors off. About 0.15 USD.

**Steps**

1. Send the Agent three Turns that need no connector.
2. Search its session log under `~/.claude/projects/` for `claude_ai`.

**Pass if — all of these**

- The log has no `claude_ai` string after Turn 3, and `mcp__team__` tools work.

### AGCONN-08 — With the switch on the connectors appear, and every use asks first

**Paid** · about 20 min

*Proves P-2 and P-3. Needs the approval prompt change. Use one harmless read only (`get_me`).*

**Before you start**

- The approval prompt change merged. About 0.20 USD; ask the owner before the first paid Turn.

**Steps**

1. Turn the switch on, wait for the restart, and send three Turns, one asking for `get_me`.
2. Answer the prompt.
3. Turn the switch off and send two more Turns.

**Pass if — all of these**

- The log names the connectors from Turn 2 and the prompt appeared before the read ran.
- After the switch is off the connectors are gone from Turn 2 of the resumed session.

### AGCONN-09 — An Agent cannot switch its own connectors on

**Paid** · about 10 min

*Proves P-4: no tool exists for it.*

**Before you start**

- An Agent with connectors off. About 0.05 USD.

**Steps**

1. In a Room ask the Agent to turn on account connectors for itself.

**Pass if — all of these**

- The Agent says it has no way to do that, and the card still shows the switch off.

### AGCONN-10 — A Turn running when the switch flips

**Paid** · about 10 min

*Proves P-5 (spec 8.3, unverified): what the Human sees.*

**Before you start**

- An Agent in a long Turn. About 0.05 USD.

**Steps**

1. Start a Turn that takes a while.
2. Flip the switch mid-Turn and choose Give access.

**Pass if — all of these**

- Write down what the Room and the card show; there is no pass line until the result is known.

### AGCONN-11 — A connector call held past the wait

**Paid** · about 15 min

*Proves T-13 end to end: no answer refuses, and the rest of the Turn is refused.*

**Before you start**

- An Agent with connectors on, a harmless read tool (such as `get_me`) and `Team:Acp:UserInputTimeoutSeconds` lowered to 30. About 0.1 USD.

**Steps**

1. Ask the Agent to use the connector twice. When the card appears, leave it unanswered for more than the wait.

**Pass if — all of these**

- The card disappears, the Agent reports the call was refused, and the second call is refused with no new card.

### AGCONN-12 — The switch flipped while a card is open

**Paid** · about 10 min

*Proves a pending approval survives the restart without being approved.*

**Before you start**

- An Agent with connectors on and a connector card waiting. About 0.05 USD.

**Steps**

1. Turn the switch off on the Agent card.

**Pass if — all of these**

- The card is gone, the call was not run, and nothing was sent.

### AGCONN-13 — Hostile arguments

**Paid** · about 10 min

*Proves the card shows the Agent's arguments as text only.*

**Before you start**

- An Agent with connectors on. About 0.1 USD.

**Steps**

1. Ask the Agent to use a connector tool with an argument of `<img src=x onerror=alert(1)> **bold**` followed by 6000 characters.

**Pass if — all of these**

- The card shows the markup and asterisks literally, under the line saying they are the Agent's own words, and says how many characters are not shown. No image loads and nothing is bold.
