# Agent browser

Prove that every Agent has its own browser with no action by the Human, that the Human can switch it off, that reading a page runs freely and every click or typing asks first, and that a sign-in the Human types into the Agent's window stays with that Agent. Spec: [Huddle.AgentBrowser-Specifications.md](../../Huddle.AgentBrowser-Specifications.md). Decision: [ADR-0090](../../adr/0090-an-agent-has-its-own-browser-through-playwright-mcp.md).

**11 tests** · 0 free · 11 paid · about 1.8 hours. Written 2026-11-16 for issue #551 and **run once, 2026-11-16, build `86eac5c2`**, by an agent acting as the Human on Sonnet: 10 Pass and BROWSER-04 Partial (see the [Run log](run-log.md)). **Owed to the owner:** BROWSER-04 step 1, a Human typing the sign-in into the Agent's visible window by hand.

> **Paid, Sonnet minimum.** Every case sends real Turns. Use Sonnet or better for each Agent, never Haiku. Budget stop: 5 USD. Ask the owner first, and record each result in the [Run log](run-log.md).

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names.

## Setup

Run [`P-BUILD`](common.md#p-build), then start a paid lane as [`P-LAUNCH-PAID`](common.md#p-launch-paid-) describes, with these settings (the 2026-11-16 run used them):

| Setting | Value |
| --- | --- |
| URL | `http://localhost:5551` |
| `Team:DataDir` | A fresh folder, for example `C:\HuddleLane551\data` |
| `Team:PipeName` | A name no other lane uses, for example `huddle-lane551` |
| `Team:Acp:Enabled` | `true` |
| `Team:Acp:AdapterPath` | The `dist/index.js` of the `claude-agent-acp` under `tools/acp`. On a checkout run with `dotnet run` the Browser row says "Huddle can't give Claude a browser" without it. |
| `Team:Acp:HiddenModes:0` | `plan`, so that `bypassPermissions` can be chosen (BROWSER-09 only) |

Remove `ANTHROPIC_*`, `CLAUDE_CODE_*` and `CLAUDECODE` from the environment before the launch, and confirm `claude auth status` says `loggedIn: true`. Every Agent is Sonnet or better. The Chief of Staff is seeded at every start: set it to Sonnet / low first, as `P-LAUNCH-PAID` says (the 2026-11-16 run left it on its built-in Opus and only its Greeting Turn was paid). The tests use three public sites: `https://quotes.toscrape.com/js/`, `https://www.saucedemo.com` and `https://demo.playwright.dev/todomvc`. The whole run cost about 0.84 USD.

**Reading the evidence.** The Playwright server is a `node cli.js --browser msedge --user-data-dir <data>\browser-profiles\<Name>` process: list it with `Get-CimInstance Win32_Process -Filter "Name = 'node.exe'" | Where-Object CommandLine -like '*browser-profiles*' | Select-Object ProcessId, CommandLine`. A card or refusal is also a line in [`O-LOG`](common.md#o-log), and every call is a line in [`O-TOOLCALLS`](common.md#o-toolcalls). Ask the Agent about its tools by name, but trust the process list over its answer: a model can contradict itself about its own tool list.

## Tests

### BROWSER-01 — A new Agent has the browser on, with no action 💰

**Paid** · about 10 min

*Proves the default: the Browser row shows On with no action by the Human, and the session log lists `mcp__playwright__` tools.*

**Before you start**

- A fresh paid lane (Setup). About 0.1 USD.

**Steps**

1. Create an Agent on Sonnet and name it Scout (`P-NEW-PERSONA`). Do nothing else to it.
2. Open Scout's card (`P-OPEN-AGENT`) and find the **Browser** row, directly under Account connectors.
3. List the Playwright processes (Setup).
4. Send Scout one Turn: "How many tools whose names start with mcp__playwright__ do you have? Is browser_run_code_unsafe one of them?"
5. Open the Agent card's prompt preview and search it for `You have your own browser`.

**Pass if — all of these**

- The row reads "On. Scout has its own browser window on this computer. Reading pages runs freely; every click, typing or form step asks you first. Sites signed in to in that window stay signed in for Scout only."
- A `node cli.js --browser msedge --user-data-dir …\browser-profiles\Scout` process runs.
- Scout reports Playwright tools (24 on 2026-11-16) and `browser_run_code_unsafe` is not one of them.
- The prompt preview contains `You have your own browser`.

### BROWSER-02 — Switching off removes the server 💰

**Paid** · about 10 min

*Proves that after the restart the Agent has no `playwright` tools and no `node cli.js` process runs for it.*

**Before you start**

- Scout from BROWSER-01 with the browser on.

**Steps**

1. On Scout's card switch the **Browser** row off.
2. Wait for the restart to finish, then list the Playwright processes (Setup).
3. Ask Scout: "How many tools whose names start with mcp__playwright__ are in your tool list, deferred tools included?"
4. Look in `O-DB` (`team.db`) for a `persona_browser_off` row for Scout.

**Pass if — all of these**

- The row reads "Off. Scout has no browser." at once, with no confirm.
- No process with Scout's profile folder is running (the Chief of Staff's still is).
- Scout counts 0 such tools.
- `persona_browser_off` has a row for Scout.

Switch it back on before BROWSER-03. If Scout then contradicts the 0, that is the model; the process list is the evidence.

### BROWSER-03 — The Agent summarises a page that needs JavaScript 💰

**Paid** · about 10 min

*Proves a page `WebFetch` cannot read is read through the browser.*

**Before you start**

- Scout with the browser on, in its one-to-one Room. About 0.05 USD.

**Steps**

1. Send: "Open https://quotes.toscrape.com/js/ and list the first three quotes with their authors. Use your browser."
2. Watch the Room and the log while it works.
3. Read `O-TOOLCALLS` for Scout.

**Pass if — all of these**

- Scout lists three quotes with authors (on 2026-11-16: Einstein, J.K. Rowling, Einstein).
- The tool-call log shows `browser_navigate` and `browser_snapshot`, and no approval card appears and no "Connector call asks" line is logged.

### BROWSER-04 — A sign-in the Human types, and a page behind it 💰

**Paid** · about 15 min

*Proves the Human signs in to one site in the visible window and the Agent then reads a page behind that sign-in.*

**Before you start**

- Scout with the browser on. Scout's browser window must be open and visible (ask Scout to open `https://www.saucedemo.com`, then Allow once if a card appears). The site publishes its demo credentials on its login page: `standard_user` and the shared demo password shown there. About 0.05 USD.

**Steps**

1. As the Human, click in Scout's own browser window, not your usual Edge, and type the demo user name and password into the sign-in form by hand. Submit it and check the product list shows.
2. Tell Scout: "Close your browser."
3. Send: "Open https://www.saucedemo.com/inventory.html. Do not type or click anything. Tell me whether it asked you to sign in, and list three products with prices."

**Pass if — all of these**

- Scout says the inventory page loaded without asking it to sign in.
- It lists products with prices (Sauce Labs Backpack $29.99, Sauce Labs Bike Light $9.99, Sauce Labs Bolt T-Shirt $15.99).
- No card asked for the page load, and Scout never saw or was given the password in a Message.

The 2026-11-16 run did not type into the window: an agent cannot drive the desktop window. With Scout's browser closed, a Playwright script (not shipped) opened Scout's own profile folder in Edge and signed in, which set the site's `session-username` cookie, and the Turn in step 3 was run as written. That proves the profile keeps the sign-in for the next session; the hand-typed step 1 is still owed.

### BROWSER-05 — A click asks, and Refuse stops it 💰

**Paid** · about 10 min

*Proves Allow once runs the click, and Refuse stops it with the Agent reporting the refusal without retrying.*

**Before you start**

- Scout with the browser on. About 0.05 USD.

**Steps**

1. Send: "Open https://demo.playwright.dev/todomvc, click the 'What needs to be done?' box, then type 'buy milk at 5pm' and press Enter."
2. When the card "Scout wants to click in its browser. Allow this one call?" appears, choose **Allow once**.
3. When the next card appears (type), choose **Refuse**.
4. Read the Room and the log.

**Pass if — all of these**

- The click card appeared before the click ran and named the target (`e8`, the "What needs to be done?" input). The log says it was allowed once.
- The log says the typing was refused by the Human, and Scout says it did not type or submit anything and is not retrying.

### BROWSER-06 — A type step's card shows the exact text 💰

**Paid** · about 10 min

*Proves the approval card for typing shows the exact text the Agent would type.*

**Before you start**

- Scout with the browser on and the same Turn as BROWSER-05 (it can be the same Turn: read the second card).

**Steps**

1. Send the BROWSER-05 message again, and Allow the click.
2. Read the type card before answering it, then **Refuse**.

**Pass if — all of these**

- The card reads "Scout wants to type text in its browser. Allow this one call?" and shows `text`: `buy milk at 5pm`, `submit`: `true`, `target`: `e8` and the element name, exactly as asked.

### BROWSER-07 — A Team duplicate keeps a member's browser off 💰

**Paid** · about 10 min

*Proves that duplicating a Team with one member off gives a copy that is off.*

**Before you start**

- A Team with two members of different roles (the run used Research Team with Ethan and Yusuf; an Agent with no role in its file is skipped by Duplicate, which is not this test). Switch one member's browser off on its card.

**Steps**

1. Duplicate the Team and give the copy a new name.
2. Open the copied member whose original was off, and read its Browser row.
3. Check `persona_browser_off` in `team.db` for the copy.
4. Delete the copy and read the confirm before choosing Delete.

**Pass if — all of these**

- The copy's row reads "Off." and `persona_browser_off` has its row from the moment of the hire.
- The delete confirm says "Its browser profile goes too, including the sites signed in to in its browser." and `browser-profiles\<copy>` is gone afterwards.

### BROWSER-08 — Resetting the Chief of Staff turns the browser on 💰

**Paid** · about 5 min

*Proves a reset to default returns the switch to on.*

**Before you start**

- A paid lane (Setup). No extra spend.

**Steps**

1. On the Chief of Staff's card switch the Browser row off.
2. Choose **Reset to default**, then **Reset**.
3. Read the row and `persona_browser_off`.

**Pass if — all of these**

- The row is On again and `persona_browser_off` has no row for the Chief of Staff.

### BROWSER-09 — Work Modes: `acceptEdits` asks, a mode that never asks has no server 💰

**Paid** · about 10 min

*Proves `acceptEdits` still asks, and that with `bypassPermissions` offered through `HiddenModes` no server is configured.*

**Before you start**

- Scout with the browser on, and `Team:Acp:HiddenModes:0` set to `plan` (Setup). About 0.1 USD.

**Steps**

1. Set Scout's Work mode to `acceptEdits` in Edit mode and save. Ask Scout to open `https://demo.playwright.dev/todomvc` and click the input box.
2. Set Scout's Work mode to `bypassPermissions` the same way. Ask it to open the page again.
3. List the Playwright processes during that Turn, then ask Scout to count its `mcp__playwright__` tools.

**Pass if — all of these**

- Under `acceptEdits` the Playwright server runs, and the click still posts the approval card.
- Under `bypassPermissions` no server runs for Scout during the Turn and Scout lists no Playwright tools. The Browser row is disabled with a reason.

The 2026-11-16 run set the mode through `team.db` and touched a Persona text to restart the Agent, not through the picker.

### BROWSER-10 — The tool-call log holds the Playwright calls 💰

**Paid** · about 5 min

*Proves `logs/tool-calls/<agent>/` holds the Playwright calls with their arguments.*

**Before you start**

- BROWSER-03 and BROWSER-05 done in the same lane.

**Steps**

1. Open `<data>\logs\tool-calls\Scout\<today UTC>.jsonl` (`O-TOOLCALLS`).
2. Find the `browser_navigate` and the refused `browser_type` lines.

**Pass if — all of these**

- Each Playwright call has a line with `tool` `mcp__playwright__…`, its arguments (the URL for navigate; `text` and `submit` for the refused type) and a `status`.

### BROWSER-11 — Two Rooms at once: the second says the browser is in use 💰

**Paid** · about 10 min

*Proves one Agent in two Rooms at once gets an error in the second whose text matches the Skill ("already in use").*

**Before you start**

- Scout with the browser on. About 0.1 USD.

**Steps**

1. In Scout's one-to-one Room ask it to open a page and leave the browser open.
2. Create a second Room with the Chief of Staff and Scout. In it, ask Scout to open a page.
3. Read Scout's reply.

**Pass if — all of these**

- The second Room's tool result says "Browser is already in use for …\browser-profiles\Scout, use --isolated to run multiple instances of the same browser", and Scout says another of its Rooms probably has the browser open and offers to retry later.
