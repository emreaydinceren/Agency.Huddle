# App Tools a real model calls (progressive discovery)

Prove that the in-process App Tools are registered, discoverable and obeyed by a REAL model, and that the progressive-discovery design holds: the system prompt names only the help tool, and the help tool names the rest. A default Persona is offered 17 tools (`DotAcpAgentHostFactory.cs:169-225`, `SkillGrants.cs:78-99`): the seven chat tools this area exercises (`mcp__team__get_help`, `mcp__team__list_agents`, `mcp__team__create_room`, `mcp__team__invite_agent`, `mcp__team__post_message`, `mcp__team__follow_room`, `mcp__team__unfollow_room`), plus `ask_human` (always), seven Task tools (`create_task`, `get_task`, `list_tasks`, `update_task`, `close_task`, `reopen_task`, `add_evidence`; offered while `Tasks.Enabled` is true, its default) and `watch_folder` / `unwatch_folder` (File Changes on and the Adapter reads files). The other ten catalog tools are offered only through a Skill. Wherever a test below says "the seven", it means the seven chat tools, as a subset of that list. The automated suite answers every one of these questions with a fake agent, so none of it is evidence here. The failures this area exists to catch are silent: a tool server that never receives traffic while the tile says Online; a model that recites the tool names from the system prompt and never calls a tool; a mis-resolved room id that comes back as ordinary prose and changes nothing on screen; a budget refusal that the model retries or routes around; a follow that outlives the reason it was set, or that quietly buys a Turn past the Budget. Tests APPTOOLS-01 to APPTOOLS-03 are free and cost no tokens. Everything from APPTOOLS-04 onward starts a real adapter, so it bills the user's Claude subscription (APPTOOLS-04 only for the Chief of Staff's Greeting Turn); each says how much. A test that sends no new Turn (APPTOOLS-09, -20, -21) is Free, as is APPTOOLS-28, which runs on the mock adapter.

**28 tests** · 7 free, 21 paid 💰 · about 4.3 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB` / `O-TOOLCALLS`, the five resets,
`P-NEW-PERSONA`, `P-ECHO-BOT` and the standing conventions. This area adds:

1. ONE-TIME (adapter): from the repo root, run `pwsh tools/acp/install.ps1`. It must leave a `tools/acp/node_modules` folder behind. Without it, every paid test here is INCONCLUSIVE, not failed.
2. ONE-TIME (auth): meet the Claude login requirement in [§0.1](../manual-tests.md#01-what-you-need), and clear `ANTHROPIC_API_KEY` as [§0.2](../manual-tests.md#02-the-cost-guard) requires. Without both, every paid test here is INCONCLUSIVE, not failed.
3. ONE-TIME (safety): work from a throwaway checkout. `<data>\Teammates\<Name>\work\` becomes the agent's working directory and is NOT a sandbox — agent-side `Bash`, `Read` and `Write` run against the real disk, and the Adapter auto-loads `CLAUDE.md` and `.claude/settings.json` from that folder.
4. Free tests (APPTOOLS-01 to -03) use `P-LAUNCH-FREE`; everything from APPTOOLS-04 uses `P-LAUNCH-PAID`, whose first step makes the built-in Chief of Staff cheap (Haiku / low). Without it every paid launch on fresh data also spends an unbudgeted Greeting Turn on the account-default model.
5. LOG LEVEL: Development already gives `Agency.Huddle` at `Debug`, which prints `App tool server listening…` and `App tool server received {Method}.` (so setting Debug yourself is redundant). Which tool a Persona ran comes from `O-TOOLCALLS`, not from this log: the `received` line names only the JSON-RPC method, and the `replied` line carries the response, never the tool called. For any test whose oracle needs the tool's actual JSON output, set `${env:Logging__LogLevel__Agency.Huddle} = "Trace"` before starting — the double underscore replaces the colon and the dot in the category stays a dot, so the key is `Logging:LogLevel:Agency.Huddle`. The category must be spelled `Agency.Huddle` exactly; a key spelled `Team` matches nothing and silently disables the filter with no error.
6. PERSONAS YOU WILL NEED for the paid tests: `P-NEW-PERSONA` twice, both Haiku/low — `Nova` (Title `Coordinator`, Alias `nova`) and `Jarvis` (Title `Researcher`, Alias `jar`). The alias `jar` on `Jarvis` is load-bearing for APPTOOLS-10.
7. Reset between scenarios with `P-RESET-ALL`. `team.db` is created with `CREATE TABLE IF NOT EXISTS`, so nothing ever migrates — a half-deleted `App_Data` produces confusing results.
8. TEST ORDER: run APPTOOLS-01 through -04 first. -01 to -03 are free and -04 spends only the Chief of Staff's one cheap Greeting Turn; together they tell you whether the wiring is sound. Do not send a prompt until APPTOOLS-04 passes: `acp-agent-guide.md` §3.6 records two sessions lost to debugging 'the model cannot find the tool' when the tool server had never received a single request.

## Tests

### APPTOOLS-01 — The prompt groups are readable in the browser and no default contains the literal mcp__team__

**Free** · about 8 min

*Proves the catalog's prompt defaults are all rendered and editable, in their groups, and that the tool-name prefix is substituted from code at render time rather than typed into any default — the design that stops a human misspelling a tool into nonexistence. Each field's textarea is now a `MudTextField` (Stage 3 of the MudBlazor migration), which renders as a real `<textarea>` inside the unchanged `.prompts-field` wrapper — there is no `prompts-field-value` class on it any more.*

**Before you start**

- `P-LAUNCH-FREE` (`Team__Acp__Enabled=false`, confirmed with `O-ADAPTERS` printing `0`).
- A fresh `<data>`, or at least no `<data>\prompts.json`. If it already exists, run `P-RESET-SETTINGS` and relaunch.

**Steps**

1. Open http://localhost:5100 in a browser.
2. Click **Settings** in the left sidebar.
3. Confirm the browser URL is `http://localhost:5100/settings` and the page heading reads `Settings`.
4. Confirm the tab rail shows six tabs, in this order: **Prompts**, **Appearance**, **Personas**, **Skills**, **Team Types**, **Library** (`Settings.razor:36-76`), and that **Prompts** is the one currently shown (it is the default — you did not have to click it).
5. Read the second intro paragraph and confirm it names a file path and says the file 'does not exist until the first time you save here, so it being absent is expected, not a bug'.
6. Scroll the page and confirm the group headings, in this order, start **System prompt**, **Turn**, **Get help**, **Tool descriptions**, and that **Tasks** and **Cadence** follow (`PromptFieldFactory.cs:32-40`). Do not count the groups' fields: the catalog grows, and the checks below name fields, not totals.
7. Confirm **System prompt** contains these fields, in this relative order: **Orientation**, **Identity**, **Chat rules**, **Tools**.
8. Confirm **Turn** contains these fields, in this relative order: **Room label**, **Message**, **Catch-up header**, **Catch-up line**.
9. Confirm **Get help** contains these fields, in this relative order: **Help: introduction**, **Help: Rooms**, **Help: messages**, **Help: mentions**, **Help: replying**, **Help: budget**, **Help: tools heading**, **Help: tool entry**, **Help: footer**.
10. Confirm **Tool descriptions** contains these fields, in this relative order: **get_help description**, **list_agents description**, **create_room description**, **invite_agent description**, **post_message description**, **follow_room description**, **unfollow_room description**. Other tools' descriptions may follow or interleave; the page does not promise one field for every offered tool.
11. Read the **Tools** field (under System prompt). Confirm its text contains the token `{{toolNames}}` (followed by a full stop, at the end of the line that follows `These tools run inside the application process:`) and contains NO literal tool name.
12. Read the **Orientation** field. Confirm it contains the token `{{helpTool}}`.
13. Read the **Help: tool entry** field. Confirm it contains both `{{toolName}}` and `{{toolDescription}}`.
14. Read the **Room label** field. Confirm its whole text is exactly `[Room: {{roomName}} (id: {{roomId}})]`.
15. Look at the **Next session** badges next to field labels. Confirm they sit on Orientation, Identity, Chat rules and Tools under System prompt, and on every `… description` field under Tool descriptions. Confirm no field under **Turn** carries one.
16. Open the browser developer tools (F12), go to the Console tab, and paste exactly: `document.querySelectorAll('.prompts-field textarea').length === document.querySelectorAll('.prompts-field-label').length` then press Enter.
17. Confirm the console prints `true`: every field label has exactly one textarea. Do not compare the count with a number.
18. In the same console paste exactly: `[...document.querySelectorAll('.prompts-field textarea')].filter(t => t.value.includes('mcp__team__')).map(t => t.closest('.prompts-field').querySelector('.prompts-field-label').textContent)` then press Enter.
19. Confirm the console prints an empty array `[]`.
20. In File Explorer, open `<data>` (`<repo>\src\Huddle.App\App_Data\`) and confirm there is NO file named `prompts.json`.

**Pass if — all of these**

- The group headings start System prompt, Turn, Get help, Tool descriptions, in that order.
- The console reports `true` for the textarea-per-label check.
- The console reports an empty array for the `mcp__team__` search — no prompt default anywhere contains that literal string.
- **Tools** contains `{{toolNames}}`; **Orientation** contains `{{helpTool}}`; **Help: tool entry** contains both `{{toolName}}` and `{{toolDescription}}`; **Room label** is exactly `[Room: {{roomName}} (id: {{roomId}})]`.
- **Next session** badges on the four named System prompt fields and on every Tool descriptions field; none on a Turn field.
- `<data>\prompts.json` does not exist.

**Fail if — any of these**

- The console array is NOT empty — a prompt default has been hand-edited to hard-code a tool name such as `mcp__team__list_agents`. This reintroduces exactly the silent failure the design removed: the prefix is derived in code from the single constant `ToolServerName = "team"` and applied only when the resolved Adapter profile has `UsesToolNamePrefix` true (ADR-0014; `src/Huddle.App/Acp/DotAcpAgentHostFactory.cs:234`), and a hard-coded copy can drift from it, or be wrong for an Adapter that takes bare names, with no build error and no runtime error — the model simply reports the tool does not exist. Report the field names the console printed.
- **Tools** shows seven literal tool names instead of `{{toolNames}}` -> the substitution token was replaced by its rendered output, so the tool list is now frozen text that will not follow a change to the tool roster.
- **Room label** has lost `(id: {{roomId}})` -> every `invite_agent` and `post_message` call is silently reduced to rooms the agent created itself; the model can never learn a room id any other way.
- The console prints `false` -> a catalog entry has a label but no textarea (or the reverse), so a prompt is shown that cannot be edited.
- A **Next session** badge appears on a Turn field, or is missing from a System prompt or Tool descriptions field -> the timing metadata is wrong, and testers will later be told a live edit needs a restart (or worse, that a restart-only edit is live).

**Inconclusive if**

If the Settings page shows a red `MudAlert` error banner (the old `composer-error` paragraph, converted in Stage 3 of the MudBlazor migration), or the tab rail is missing, the app did not start cleanly — read the console window for an unhandled startup exception and fix that before judging this test. If `<data>\prompts.json` already existed before you started, the defaults you are reading may be overrides; that is INCONCLUSIVE, not a fail: run `P-RESET-SETTINGS`, relaunch and rerun. Do NOT file the absence of `prompts.json` as a bug — its absence is the documented normal state.

> [!NOTE]
> This is the cheapest test in the area and the one most likely to catch a regression introduced by a well-meaning edit to prompt text. Run it first, every time.

### APPTOOLS-02 — PromptValidator reports a missing placeholder and a typo, and never blocks the save

**Free** · about 10 min

*Proves the validator is wired to every field and behaves as designed: an issue is advice, never a refusal. A validator that blocks is one users route around, and prompt experimentation is the whole point of this surface.*

**Before you start**

- APPTOOLS-01 has passed.
- App running under `P-LAUNCH-FREE`.
- `<data>\prompts.json` does not exist yet.

**Steps**

1. On http://localhost:5100/settings, Prompts tab, scroll to the **Turn** group and find the field labelled **Room label**.
2. Click into its textarea, press Ctrl+A to select all, then type exactly: `[Room: {{roomName}}]`
3. Look immediately below the textarea and its grey helper line. Confirm a red issue line appears naming the missing placeholder `{{roomId}}` and repeating the helper text about it being the only way an agent learns a Room's id.
4. Look at the field's label row. Confirm a badge reading **Unsaved** has appeared next to `Room label`, alongside **Modified** (the text now differs from the default; `PromptFieldFactory.cs:147-150`).
5. Scroll to the bottom of the form and confirm the **Save** button is ENABLED (not greyed out).
6. Click **Save**.
7. Confirm no red error paragraph appears at the top of the page.
8. Confirm the **Room label** field's badge row now reads **Modified** and no longer reads **Unsaved**.
9. In File Explorer open `<data>\prompts.json` (`<repo>\src\Huddle.App\App_Data\prompts.json`) in a text editor. Confirm it exists and contains exactly one key, `turn.roomLabel`, and no other prompt key.
10. Back in the browser, click into the **Room label** textarea, press Ctrl+A, and type exactly: `[Room: {{roomName}} (id: {{roomID}})]` (note the capital D in roomID).
11. Confirm TWO issue lines now appear under the field: one red line about the missing required `{{roomId}}`, and one amber line naming the token `{{roomID}}` and saying it is 'most likely a typo'.
12. Click the **Reset** button in that field's action row.
13. Confirm the textarea's text returns to exactly `[Room: {{roomName}} (id: {{roomId}})]`, all issue lines disappear, and the badge row shows **Unsaved** (Reset stages the default; it does not write).
14. Click **Save**.
15. Confirm the **Modified** and **Unsaved** badges are both gone from **Room label**.
16. Reopen `App_Data/prompts.json` and confirm it is now an empty JSON object `{}` — saving a value equal to the catalog default removes the key rather than storing a redundant copy.

**Pass if — all of these**

- A red issue line appears under **Room label** naming `{{roomId}}` as soon as it is deleted, with no page reload and no Save.
- The **Save** button stays enabled while the error is showing, and the save succeeds.
- The typo `{{roomID}}` produces a second, amber (warning, not error) issue line that names the token.
- **Reset** restores the exact default text and stages it as **Unsaved** rather than writing immediately.
- `prompts.json` appears on the first save holding only `turn.roomLabel`, and returns to `{}` after Reset + Save.

**Fail if — any of these**

- The **Save** button is disabled, or the save is refused, while an error issue is showing -> the validator has been made a gate. `PromptValidator` is explicitly documented as reporting and never refusing; a blocking validator is the bug, not the missing placeholder.
- No issue line appears at all when `{{roomId}}` is deleted -> the validator is not wired to this field, and the single silent failure it exists to catch (a Room label with no id) will now ship unnoticed.
- The typo `{{roomID}}` produces no amber warning -> the unrecognised-token check is broken, and a one-character prompt typo becomes invisible.
- **Reset** writes to `prompts.json` immediately without a Save -> the single commit point has been bypassed and a mis-click is unrecoverable.
- After Reset + Save the key `turn.roomLabel` is still present in `prompts.json` with the default value -> the store is accumulating redundant overrides, which makes 'what have I actually changed?' unanswerable from the file.

**Inconclusive if**

If `App_Data/prompts.json` cannot be written (a permissions error paragraph appears at the top of the page reading `Could not save: …`), this is an environment problem, not a defect: the app is running from a folder it cannot write to. Move the checkout or run the app with write access, then rerun. If the textarea does not accept typing at all, the Blazor circuit has disconnected — look for the reconnect dialog, reload the page, and rerun.

> [!NOTE]
> Restore the default before moving on (the last two steps do this). Leaving `turn.roomLabel` overridden will silently break APPTOOLS-08 and every invite/post test after it.

### APPTOOLS-03 — With Team:Acp:Enabled=false no App Tool server exists at all, and the chat surface still works

**Free** · about 10 min

*Establishes the free baseline: proves the app never turns ACP on by itself, that an Agent which merely never started produces no alarm, and that the chat surface can be exercised without spending anything.*

**Before you start**

- Two Personas exist (`Nova`, `Jarvis`), both Haiku/low, besides the built-in Chief of Staff (seeded at every start). If they do not, create them now with `P-NEW-PERSONA` — creating a Persona is free.
- `pwsh` available and a THIRD terminal free (`T-C`) — the bot in step 11 is `P-ECHO-BOT`, a PowerShell pipe client, so this test stays free.

**Steps**

1. `P-STOP` the app.
2. Relaunch with `P-LAUNCH-FREE`; confirm `O-ADAPTERS` prints `0`.
3. Wait for `Now listening on: http://localhost:5100`.
4. Run `O-ADAPTERS` and note the count (`0`). Do not count `node.exe` in Task Manager: on a development machine unrelated `node.exe` processes are normal.
5. In the browser open http://localhost:5100/teammates.
6. Confirm every Persona tile is listed (Chief of Staff, Nova, Jarvis), and that each tile's avatar badge reads **Offline** (hollow ring in the error red, `--mud-palette-error`; red by design, never grey).
7. Hover each **Offline** tile's status text. Confirm no tooltip reason appears (an Agent that merely never started has no reason).
8. Click the **Nova** tile to open its card. Confirm the card shows **Offline** and shows NO reason line beneath the status.
9. Close the card with the × button.
10. Read the app's console window from the top. Confirm there is NO line containing `App tool server listening at`.
11. In `T-C`, from the repo root, run `pwsh tools/echo-bot.ps1 -Name mybot` and wait for a line containing `"type":"welcome"`.
12. In the browser, click **New chat** in the left sidebar, tick the checkbox next to `mybot`, then click **Start chat**.
13. In the room that opens, type exactly `hello` and press Enter.
14. Confirm `mybot` replies within a few seconds and its reply appears as a message row with sender `mybot`.
15. Confirm no amber `member-health-alert` strip appears anywhere between the transcript and the composer (it is a `Severity.Warning` alert, `Chat.razor:76`).
16. Run `O-ADAPTERS` again and confirm it still prints `0`: no adapter was started during any of the above.

**Pass if — all of these**

- Every Persona tile reads **Offline** with no reason, on the tile and on the card.
- No `App tool server listening at` line anywhere in the console.
- `O-ADAPTERS` prints `0` before and after.
- `mybot` connects and replies, proving the chat surface works with zero spend.
- No `member-health-alert` strip appears in the room.

**Fail if — any of these**

- A permanent amber alert strip appears in the room listing every offline Agent -> an always-on alert is one nobody reads; Offline-with-no-reason is deliberately not an alarm. This is a regression in `Chat.razor`'s `UnhealthyMembers` filter.
- `O-ADAPTERS` counts an adapter, or `App tool server listening at` appears in the log, while `Team__Acp__Enabled` is `false` -> code is turning ACP on by itself. This is the most serious possible finding in this area: the flag exists precisely because starting an adapter spends the user's money.
- `mybot` does not reply -> the chat surface itself is broken, independent of App Tools. Stop and fix that first; every later test in this area will be unreadable.
- A Persona tile reads **Online** with ACP disabled -> the status resolver is reading something other than real pipe liveness.

**Inconclusive if**

If `O-ADAPTERS` printed a non-zero count before the app took any action (a leftover adapter from an earlier run), you cannot attribute it — stop it with `Stop-Process -Id <ProcessId>`, restart the app, and rerun. If `mybot` does not appear in the **New chat** panel, the bot never registered; check `T-C` for the `welcome` line before judging. Do NOT file the fact that opening a Create/Edit teammate card spawns a `node` probe in this mode — that is a documented, deliberate and free behaviour (no Turn is started, so no tokens are spent).

> [!NOTE]
> `O-ADAPTERS` is the honest oracle here, not the log. A missing log line proves the server did not bind; only the adapter count proves no adapter was launched.

### APPTOOLS-04 — The tool server binds and completes the MCP handshake before any model call — the wiring pre-flight

**💰 Spends money** · about 12 min

*Proves the App Tool server is actually reachable by the adapter and that the adapter registered the tools, WITHOUT sending a prompt of your own. This is the gate that separates a configuration fault from a discovery fault. It is not free: with ACP on, the built-in Chief of Staff takes one Greeting Turn into its empty direct Room (`PersonaRunner.cs:481-495`).*

**Before you start**

- `pwsh tools/acp/install.ps1` has been run and `tools/acp/node_modules` exists.
- The `claude` CLI is logged in and `ANTHROPIC_API_KEY` is not set.
- At least one Persona (`Nova`) exists, Haiku/low.
- COST: the Chief of Staff's Greeting Turn, on Haiku at effort low only because `P-LAUNCH-PAID` set it so first (a few hundred tokens). You send no prompt, so Nova spends nothing.
- You are prepared for real `node` processes to start.

**Steps**

1. `P-STOP` the app.
2. Open a NEW `T-A` PowerShell window (so the `Team__Acp__Enabled=false` from the previous test is gone).
3. Launch with `P-LAUNCH-PAID`, including its Chief of Staff Haiku / low step. (Development already sets `Agency.Huddle` to `Debug`, so no log-level variable is needed.)
4. Wait for `Now listening on: http://localhost:5100`.
5. Open http://localhost:5100/teammates in the browser and leave it open. Do NOT open any room and do NOT type any message.
6. Watch the badge on the **Nova** tile's avatar (hover it to read its tooltip). Confirm it reads **Starting** (pulse glyph), then changes to **Online** (check glyph), within about 30 seconds (Degraded is a dash glyph, Offline a hollow ring).
7. Look at the left sidebar. Confirm a room named `Nova` has appeared in the room list.
8. Read the app's console window. Find a line of the form `App tool server listening at http://127.0.0.1:<port>/mcp.` and note the port number.
9. In the same console, find a line reading `App tool server received initialize.`
10. In the same console, find a line reading `App tool server received tools/list.`
11. In File Explorer confirm the folder `<data>\Teammates\Nova\work\` (`<repo>\src\Huddle.App\App_Data\Teammates\Nova\work\`) exists (`TeammatePaths.cs:25-28`).
12. Confirm the transcript area of the `Nova` room contains no messages (you have typed nothing, so Nova spent nothing; only the Chief of Staff's Greeting Turn was paid).

**Pass if — all of these**

- The Nova tile goes **Starting** then **Online**, and a room named `Nova` appears in the sidebar.
- The console contains `App tool server listening at http://127.0.0.1:<port>/mcp.`
- The console contains BOTH `App tool server received initialize.` AND `App tool server received tools/list.`
- `<data>\Teammates\Nova\work\` exists on disk.
- You have sent no message, and Nova's Room is empty.

**Fail if — any of these**

- The tile says **Online** but NEITHER `App tool server received initialize.` nor `App tool server received tools/list.` ever appears -> this is the high-value silent failure. The tool server bound but the adapter never talked to it, so the model has no tools at all. Every paid test after this one would fail in a way that looks like model stupidity. STOP HERE: do not spend money. This is a wiring fault in `DotAcpAgentHostFactory`, not a prompting problem.
- The tile flips to **Degraded** with a reason line underneath, and/or the room shows an amber `member-health-alert` strip (`Chat.razor:76`) reading `Nova is Degraded: <reason>` -> read the reason; it is usually the adapter path or the CLI login. This is a configuration fault and is the OPPOSITE fix from the case above; acp-agent-guide.md §3.6 is explicit that the two must be told apart before spending anything.
- The tile stays **Offline** forever -> the supervisor never tried to start it. Confirm `Team__Acp__Enabled` is not still `false` in this shell.
- `App tool server listening at` appears but the port is not on `127.0.0.1` -> the tool server is bound to a non-loopback address; report it, it is a security regression.
- `<data>\Teammates\Nova\work\` is missing while the tile says Online -> the working directory was never created, and agent-side file tools will behave unpredictably.

**Inconclusive if**

If `tools/acp/node_modules` is missing, this test is INCONCLUSIVE, not failed — run `pwsh tools/acp/install.ps1` and rerun. If the tile reaches **Degraded** with a reason mentioning authentication or login, the `claude` CLI is not logged in: this is INCONCLUSIVE — run `claude` once interactively, log in, and rerun. If several Personas exist you will see several `App tool server listening at` lines on different ports: that is the design (one server per Persona), not a leak.

> [!NOTE]
> Only the Chief of Staff's one cheap Greeting Turn is paid, but it is the single most important test in this area. Do not begin any paid test until this one passes. Keep this app instance running — the next several tests reuse it.

### APPTOOLS-05 — An Agent reads its own Room's id off the [Room: ...] label

**💰 Spends money** · about 5 min

*Proves the Room label actually reaches the model carrying a usable room id — the only place an Agent can ever learn one. This is the cheapest possible pre-flight for every invite_agent and post_message test that follows.*

**Before you start**

- APPTOOLS-04 has passed. `Nova` is **Online**.
- COST: one very short turn on Haiku at effort low. A few hundred tokens.

**Steps**

1. In the left sidebar click the room named `Nova`.
2. Read the browser address bar and write down the room id — the part after `/rooms/`. It is 32 hexadecimal characters with no dashes, e.g. `019a4f1c8b7d7c9e8f0a1b2c3d4e5f60`.
3. Click into the composer textarea at the bottom (its placeholder reads `Message… (/invite @agent)`).
4. Type exactly: `What is the id of the room we are in right now? Answer with just the id, nothing else.`
5. Press Enter to send (Shift+Enter would insert a newline instead).
6. Wait for the streaming row to finish and a message row from `Nova` to appear.
7. Compare Nova's answer character for character with the id you wrote down from the URL.

**Pass if — all of these**

- Nova's reply contains the room id exactly as it appears in the `/rooms/<id>` URL — all 32 characters, same case, no dashes.
- Exactly one message row appears from Nova.

**Fail if — any of these**

- Nova returns the room NAME (`Nova`) instead of an id -> the `{{roomId}}` placeholder is not reaching the rendered label. Every `invite_agent` and `post_message` test below will now fail for a reason that has nothing to do with those tools. Check the **Room label** prompt on /settings before going further.
- Nova returns a plausible-looking but different 32-character string -> it invented one. Treat every subsequent tool test as unreliable until this passes.
- Nova says it cannot tell, or asks you for the id -> the label is absent from the prompt entirely. Stop and fix; do not spend on the invite/post tests.
- Two message rows appear from Nova with the same content -> see APPTOOLS-15; the model both replied and posted.

**Inconclusive if**

If no streaming row ever appears and nothing arrives, the turn never reached the model — check the console for `Persona 'Nova' declined a turn` (a spent budget from an earlier test; type any message to reset it, or restart the app) or for a refusal warning. If Nova answers with an id that matches the URL except for case, record it as a PASS but note the case difference — ids are lowercase hex and a case-shifted id will still fail a lookup later.

> [!NOTE]
> Run this before APPTOOLS-09 through APPTOOLS-14. It costs almost nothing and it converts a whole class of confusing downstream failures into one clear finding.

### APPTOOLS-06 — get_help's BUDGET and MESSAGES sections prove progressive discovery is live

**💰 Spends money** · about 8 min

*The cheapest single-turn proof that the model actually CALLED get_help rather than reciting the system prompt. The BUDGET and MESSAGES sections exist only in get_help's output; the system prompt does not contain them.*

**Before you start**

- `Nova` is **Online**.
- For the strongest oracle, the app was started with `${env:Logging__LogLevel__Agency.Huddle} = "Trace"`. If it was started at Debug, the test still works from the reply text alone — note which you used.
- COST: one short turn on Haiku at effort low.

**Steps**

1. Open the room named `Nova` from the sidebar.
2. Click into the composer.
3. Type exactly: `Without guessing: what happens in a Room when the agent-message budget runs out, and what does a message marked "context only" mean?`
4. Press Enter.
5. Wait for the reply to finish streaming.
6. Read the reply and check it covers all four of these points about the budget: that the Room stops accepting agent messages; that the human is told and can allow more; that a refusal saying the budget is spent is FINAL and must not be retried; and that the agent must not work around it by posting to another Room.
7. Check the reply also defines 'context only' as messages the agent was NOT addressed in, which it should read for background and not answer.
8. Run `O-TOOLCALLS` for `Nova`: `Get-Content "<data>\logs\tool-calls\Nova\$((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')).jsonl" -Tail 1`. Confirm a line whose `tool` field names `get_help`. The `tool` value is the title the Adapter gave the call, so judge it by containing `get_help`, not by an exact string.
9. If you are running at Trace, switch to the app's console window and find a line beginning `App tool server replied to tools/call:` whose payload is a JSON string. Decode it (newlines appear as `\n`, and apostrophes, quotes and em-dashes as `\uXXXX`), then confirm the decoded text contains a `BUDGET` section and a `MESSAGES` section. Do not compare the payload with `getHelp.txt`: that golden is built from a fixed roster (`GetHelpToolTests.cs:60-66`) and the live catalog differs from it.

**Pass if — all of these**

- The reply states that a budget refusal is final, must not be retried, and must not be worked around by posting to another Room.
- The reply defines 'context only' as messages the agent was not addressed in, to be read but not answered.
- `O-TOOLCALLS` has a `get_help` line for Nova, and at Trace the decoded payload contains the BUDGET and MESSAGES sections.

**Fail if — any of these**

- The reply is vague, hedged, or invented — e.g. 'the room probably stops the agent' with no mention of not retrying and not rerouting -> `get_help` was not called. Progressive discovery is not working end to end, and the system prompt alone cannot supply this answer because it carries neither section.
- The reply describes the budget correctly but says nothing about 'context only', or vice versa -> partial recall; check the Trace log. If there is no `tools/call` line, the model answered from the system prompt and guessed the rest.
- At Trace level, the decoded payload lacks the BUDGET or MESSAGES section -> a prompt override is in force. Check `<data>\prompts.json` and the **Help: budget** / **Help: messages** fields on /settings.
- `O-TOOLCALLS` shows no `get_help` line and the newest line names some other tool -> see the INCONCLUSIVE note. A call to another product's tool (`mcp__claude_ai_ms365__…`) never reaches this server at all, so the console cannot show it; `O-TOOLCALLS` can.

**Inconclusive if**

If the console shows the model calling a semantically adjacent tool from a different product (`mcp__claude_ai_ms365__teams_list_chats` is the recorded real-world example), that is INCONCLUSIVE — it is evidence about the model's tool search index, not proof that `mcp__team__get_help` is unregistered. Rerun with the tool named explicitly: `Call mcp__team__get_help, then answer: what happens when a Room's agent-message budget runs out?`. If the explicit call succeeds, record the original as an index miss, not a registration failure. If you are running at Debug rather than Trace, judge the call by `O-TOOLCALLS` and the reply text alone, and say which you used; the `received tools/call.` console line names no tool.

> [!NOTE]
> This is the cheapest probe that progressive discovery is live. Prefer it over APPTOOLS-07 when budget is tight — APPTOOLS-07 is more thorough but costs more and its failure mode (a) is harder to read.

### APPTOOLS-07 — Asked what tools it has, a teammate calls get_help and reports the real catalog with descriptions

**💰 Spends money** · about 10 min

*The full progressive-discovery check: the system prompt names only get_help plus a bare list of names, so a reply that reproduces each tool's own DESCRIPTION can only have come from a get_help call.*

**Before you start**

- `Nova` is **Online**. APPTOOLS-06 has been run (its result tells you how to read a partial result here).
- Trace logging strongly recommended.
- COST: one full turn on Haiku at effort low — longer output than the other probes, roughly 1–2k output tokens.

**Steps**

1. Open the room named `Nova`.
2. Click into the composer.
3. Type exactly: `What tools can you call in this application? For each one give its exact name and, in that tool's own words, what it is for.`
4. Press Enter.
5. While the turn runs, confirm a streaming message row appears showing `Nova`, a **Stop** button, and (at least briefly) a `Tool calls` row beneath the draft text (`MessageList.razor:115-137`).
6. Wait for the reply to finish.
7. Confirm the reply names at least these seven chat tools with the full prefix (the model is offered 17 tools in all, so it may name others too; extra names are not a failure): `mcp__team__get_help`, `mcp__team__list_agents`, `mcp__team__create_room`, `mcp__team__invite_agent`, `mcp__team__post_message`, `mcp__team__follow_room`, `mcp__team__unfollow_room`.
8. Now check the DESCRIPTIONS, which is the part that matters. For `mcp__team__post_message`, confirm the reply reproduces or closely paraphrases the phrase `because your reply in the current turn is only ever delivered to that Room, never to another one`.
9. For `mcp__team__invite_agent`, confirm the reply mentions that you give the Room's id, the id shown in the `[Room: ...]` line at the start of every message.
10. For `mcp__team__list_agents`, confirm the reply mentions that entries include a job description drawn from the Persona's frontmatter.
11. Run `O-TOOLCALLS` for `Nova` and confirm a line whose `tool` field contains `get_help`. At Trace, also find `App tool server replied to tools/call:` and decode its payload (newlines appear as `\n`): confirm it contains a `TOOLS` section naming the seven chat tools. Do not compare it with `getHelp.txt`, which is built from a fixed roster (`GetHelpToolTests.cs:60-66`).

**Pass if — all of these**

- All seven chat tool names appear with the `mcp__team__` prefix and are spelled exactly.
- At least three of the seven carry a real description, not just a name — specifically including the post_message phrase about the reply only ever reaching the current Room.
- `O-TOOLCALLS` has a `get_help` line for Nova; at Trace the decoded `tools/call` payload carries the `TOOLS` section.
- A streaming row with a **Stop** button and a `Tool calls` row was visible during the turn.

**Fail if — any of these**

- Correct names but NO per-tool descriptions, or one-line descriptions the tester invented plausible wording for -> failure mode (a): the model recited the system prompt's Tools block, which carries the names only. Progressive discovery did NOT happen. The names alone cannot distinguish this case, which is exactly why the description check is the pass condition.
- The reply says no such tools exist, or that it has no tools in this application -> failure mode (b): a name-lookup miss in deferred-tool mode. Cross-check against APPTOOLS-04's log lines: if `tools/list` arrived there, the tools ARE registered and this is a model-side lookup problem.
- The reply names tools from a different product entirely -> failure mode (c). Evidence about the model's tool search index, not about registration. Record it as such.
- The reply describes the tools by quoting file paths or source code -> failure mode (d): it read the repository instead of calling the tool. See APPTOOLS-18.
- A tool name is misspelled (e.g. `mcp__team_get_help` with one underscore) -> report the exact string; it points at a prefix-composition bug.

**Inconclusive if**

If the names appear with vague one-line summaries that could plausibly be either recited or retrieved, do NOT guess. Read `O-TOOLCALLS`: a `get_help` line for this turn makes it a PASS; no line makes it failure mode (a). The `received tools/call.` console line names no tool, so it cannot settle which one ran. If the streaming row never appeared but the reply did, judge the reply and record the missing streaming row separately against APPTOOLS-16.

> [!NOTE]
> Run APPTOOLS-06 first. If APPTOOLS-06 passed, a weak result here is about the prompt's phrasing rather than about registration.

### APPTOOLS-08 — Tool activity is visible under the Draft while a call is in flight

**💰 Spends money** · about 5 min

*Proves the ACP tool_call update reaches the UI as rows in the Draft's "Tool calls" list. Cheap to observe: it can be watched during any other paid test at no extra cost.*

**Before you start**

- `Nova` is **Online**.
- Trace logging on, so you can correlate the screen against the log.
- COST: none extra if you observe it during APPTOOLS-06 or APPTOOLS-07. One short turn if run standalone.

**Steps**

1. Open the room named `Nova` and position the browser so the transcript area is fully visible before you send.
2. Click into the composer and type exactly: `Call mcp__team__list_agents and tell me how many agents it returned.`
3. Press Enter and watch the transcript area continuously — do not switch windows.
4. Confirm a streaming message row appears carrying the sender name `Nova` and a **Stop** button.
5. Confirm that, while the turn runs, a list labelled `Tool calls` appears beneath the draft text with one row per call, each carrying a short title supplied by the adapter (`MessageList.razor:115-137`).
6. Confirm that when a second call starts (if one does) it is added as a further row, oldest first. The list holds at most six rows (`Drafts.cs:38`); a seventh call pushes the oldest out.
7. Confirm the entire streaming row, tool-call rows included, disappears when the turn ends and is replaced by a single ordinary message row.
8. Run `O-TOOLCALLS` for `Nova` and confirm it has a line for this turn's call; the line's time falls inside the window in which you saw the row on screen. At Trace the console also shows an `App tool server replied to tools/call:` line in that window.

**Pass if — all of these**

- A streaming row with sender `Nova` and a **Stop** button appears while the turn runs.
- At least one row appears in the `Tool calls` list beneath the draft while a call is open.
- The list never shows more than six rows.
- The streaming row and its tool-call rows vanish when the turn ends, leaving a single posted message.

**Fail if — any of these**

- No tool-call row ever appears even though `O-TOOLCALLS` shows a call for this turn -> the ACP `tool_call` update is not reaching the Draft's tool-call list. The human loses all visibility into what an agent is doing mid-turn.
- More than six rows accumulate -> the list cap (`Drafts.MaxToolCalls`) is not applied.
- A tool-call row is STILL on screen after the turn ended -> the final message delta terminator never arrived, and the draft was never properly closed. This is the more serious shape of failure here; report it.
- The **Stop** button is missing from the streaming row -> the human has no way to halt a runaway turn.

**Inconclusive if**

Do NOT judge the exact wording of a row's title — it is supplied by the adapter, not by this application, and it will change between adapter versions. If the turn completes so fast that you cannot tell whether a row appeared, that is INCONCLUSIVE: rerun with a prompt that forces more than one call, e.g. `Call mcp__team__get_help and then mcp__team__list_agents, then summarise both in one sentence.` If no row ever appears AND `O-TOOLCALLS` shows no call for this turn, this test is INCONCLUSIVE — the model never called a tool, so there was nothing to display; fix that first.

> [!NOTE]
> Free if piggybacked on APPTOOLS-06 or APPTOOLS-07 — just watch the screen during those turns instead of running this one separately.

### APPTOOLS-09 — Tool activity is not written to the Transcript and does not survive the turn

**Free** · about 5 min

*Confirms a deliberate limit rather than a defect, so a tester does not file it as a bug — and catches the one real defect in this area, an activity line that never clears.*

**Before you start**

- A tool-calling turn has just completed in the `Nova` room (run this immediately after APPTOOLS-08, APPTOOLS-06 or APPTOOLS-07).
- Free: no new turn is sent.

**Steps**

1. In the `Nova` room, scroll the transcript up and down and confirm no tool-activity line remains anywhere.
2. Confirm the transcript shows only ordinary message rows — each with a sender name, a time, and a body.
3. Press F5 to reload the page.
4. Confirm the transcript still shows only the message rows, with no tool-activity content.
5. Stop the app with Ctrl+C, restart it with the same command, and reopen the `Nova` room.
6. Confirm the transcript again shows only the message rows.
7. Open the room's transcript file at `<data>\rooms\<roomId>.jsonl` in a text editor, using the id from the `/rooms/<id>` URL.
8. Confirm every line in that file is a message record and none of them describes a tool call.

**Pass if — all of these**

- No tool-activity content survives the end of the turn, a page reload, or an application restart.
- `<data>\rooms\<roomId>.jsonl` contains only message lines.

**Fail if — any of these**

- A tool-activity line is STILL on screen after the turn ended -> this is the real bug shape: the final message delta terminator never arrived and the draft was never closed. Report it against `PersonaRunner`.
- A tool call appears as a line in the `.jsonl` transcript file -> tool activity is leaking into durable storage, which it is documented never to do.

**Inconclusive if**

If the transcript is empty after the restart, check you reopened the right room: the sidebar entry may have been renamed by an earlier invite test. Match the `/rooms/<id>` URL against the `.jsonl` filename rather than trusting the room name. Do NOT file 'the tool call history disappears' as a defect — that is a recorded decision in `docs/engineering/known-limits.md`: scrollback shows what an Agent said, not what it did.

> [!NOTE]
> Free and fast. Run it right after whichever paid test produced a tool call.

### APPTOOLS-10 — list_agents reports real names, real online state, and real job descriptions

**💰 Spends money** · about 12 min

*Proves the agent answers from the live directory rather than from invented names or names lifted from the repository's own fixtures — the single most common plausible-looking wrong answer in this area.*

**Before you start**

- Two Personas (`Nova`, `Jarvis`) exist; at least `Nova` is **Online**.
- COST: one turn on Haiku at effort low.

**Steps**

1. Open http://localhost:5100/teammates. On paper or in a text file, write down every tile's Name, its title, its alias caption, and the state word in its avatar badge's tooltip (Online / Starting / Degraded / Offline).
2. Click **New chat** in the left sidebar and write down every checkbox name in the panel and each one's status mark (glyph and tooltip). Click **New chat** again to close the panel without starting one.
3. Open the room named `Nova` from the sidebar.
4. Click into the composer and type exactly: `Who else is here? List every agent and every persona you can see, name for name, and say which are online.`
5. Press Enter and wait for the reply.
6. Compare the reply's list of names against the two lists you wrote down. Every name must appear, and no name may appear that is not on your lists.
7. Confirm the reply distinguishes registered Agents (with an online/offline state each) from the Personas list. `ListAgentsTool` lists every Persona by name, whether or not it has ever come online (`ListAgentsTool.cs:56-65`).
8. Confirm the online/offline states in the reply match the status marks you recorded.
9. Switch to the console. At Trace, find the `App tool server replied to tools/call:` line whose payload is a JSON-RPC response (it begins `{"jsonrpc"`) and whose `text` field begins `Agents:`. Decode it, then confirm its shape: `- <Name> (online)` or `- <Name> (offline)` lines, each optionally followed by indented job-description lines, then a blank line and `Personas:`.

**Pass if — all of these**

- Every name in the reply appears on the /teammates page or in the New chat panel, and every name on those surfaces appears in the reply.
- Agents are separated from Personas, each Agent carries an online/offline state, and every Persona appears by name.
- The online/offline states match the status marks on screen.
- At Trace: the decoded tool text has the documented `Agents:` / `Personas:` shape.

**Fail if — any of these**

- The reply names `Alice`, `Bob`, a room called `bananas`, or any other name that is NOT on your two lists -> the agent read the repository's docs and test fixtures instead of calling the tool. This is the plausible-looking wrong answer this test exists to catch. Note which names it invented; they are traceable to `docs/` and `tests/`.
- A Persona that is visible on /teammates is missing from the reply -> the directory and the tool disagree.
- Online and offline are inverted relative to the status marks -> the gateway liveness check in `ListAgentsTool` is reading the wrong thing.
- No job descriptions appear at all for Personas that have a Title and Alias -> `ComposeJobDescription` is returning empty, which will also break APPTOOLS-11.

**Inconclusive if**

If the reply lists the right names but gives no online/offline state, rerun once with a more explicit prompt: `Call mcp__team__list_agents and paste its output verbatim.` If the second attempt is correct, record the first as a prompt-following weakness, not a tool defect. If you cannot tell whether a name came from the tool or from the repository, read the Trace payload — it is the tool's literal output and settles it. If you are at Debug rather than Trace you cannot settle it; rerun at Trace rather than guessing.

> [!NOTE]
> Writing down the UI truth BEFORE asking is essential. Judging the reply against memory is how invented names get recorded as passes.

### APPTOOLS-11 — An Alias is advertised by list_agents and then accepted by invite_agent

**💰 Spends money** · about 10 min

*Catches the documented trap: a tool that rejects a handle it just advertised costs a turn and teaches the model not to trust the catalog. Also compares the tool path against the two free paths that do the same job.*

**Before you start**

- Two Personas online: `Nova` (alias `nova`) and `Jarvis` (alias `jar`). Both Haiku/low.
- A bot `mybot` connected in a third terminal (`pwsh tools/echo-bot.ps1 -Name mybot`, `P-ECHO-BOT`) — it is free and gives the room its third member.
- A room with three or more members. Create it: click **New chat**, tick `Nova` and `mybot`, click **Start chat**.
- COST: one turn on Haiku at effort low, plus one free re-check.

**Steps**

1. Open http://localhost:5100/teammates and confirm the `Jarvis` tile shows the title `Researcher` and an alias caption reading `@jar` (separate elements, `Teammates.razor:98-104`).
2. FREE PATH FIRST — in the three-member room (`Nova, mybot`), click into the composer and type exactly `/invite @jar` then press Enter.
3. Confirm the sidebar entry and the chat header `<h1>` both rename to include `Jarvis`, with no page refresh.
4. Now make a room Jarvis is genuinely not in so the tool path can be tested: click **New chat**, tick `Nova` and `mybot`, click **Start chat**. A multi-agent New chat always creates a fresh Room (`ChatService.cs:477-490`), so no reset is needed.
5. In that fresh room, click into the composer and type exactly: `List the agents here with their handles, then use mcp__team__invite_agent to add jar to this room.`
6. Press Enter and wait for the turn to finish.
7. Confirm Nova's reply quotes `Alias: jar` (or clearly reports `jar` as Jarvis's handle) from the job description it read.
8. Confirm the sidebar entry and the chat header `<h1>` have BOTH renamed to the comma-joined agent names including `Jarvis`, with no page refresh.
9. Confirm the member-names line under the header now lists `Jarvis`.
10. In the console, find a line reading `Invited agent 'Jarvis' (<agentId>) into room '<roomId>'.`

**Pass if — all of these**

- The `Jarvis` tile shows the title `Researcher` and the alias caption `@jar`.
- `/invite @jar` typed by the human works (the free comparison path).
- Nova's reply shows it read `Alias: jar` out of `list_agents`' job description.
- The tool call succeeds: the sidebar and the header `<h1>` rename to include `Jarvis` with no refresh, and the member-names line lists Jarvis.
- The console carries `Invited agent 'Jarvis' (<agentId>) into room '<roomId>'.`

**Fail if — any of these**

- The tool returns `Unknown agent 'jar'. Agents that do exist: …` -> this is the documented failure: the tool rejected a handle it had just advertised. Confirm it is specific to the tool path by checking that `/invite @jar` and the **Add teammate** button both still work; if only the tool path fails, that is the bug and it lives in `InviteAgentTool`'s alias resolution.
- Nova's reply never mentions `jar` or an Alias at all -> `ComposeJobDescription` is not emitting the Alias line, so a reading agent can never learn that the handle works. Cross-check against APPTOOLS-10's Trace payload.
- The rename only appears after you press F5 -> a `RoomEvents` subscription bug in `RoomList.razor` or `Chat.razor`; report it separately from the alias behaviour.
- The header renames but the member-names line does not -> the member list and the room name are being recomputed from different sources.

**Inconclusive if**

If Nova asks you which room to invite into rather than reading the id from its own label, run APPTOOLS-05 first — a broken Room label makes this test unreadable and the result is INCONCLUSIVE, not a failure of the alias path. If the model calls `create_room` instead of `invite_agent` (a second room appears in the sidebar), that is prompt-following, not alias resolution: record it and rerun once with `Use mcp__team__invite_agent, not create_room, to add jar to THIS room.`

> [!NOTE]
> Step 4 makes a fresh room so the tool path is tested against a room Jarvis is genuinely not in. Reusing the room from step 2 makes the tool return the 'already a member' text and proves nothing.

### APPTOOLS-12 — invite_agent, called with the id from the agent's own label, renames the room live

**💰 Spends money** · about 12 min

*The core invite test, and the one whose failure is silent: a wrong room id comes back as ordinary result text, the model reads it as prose, the sidebar does not change, and nothing is logged as an error.*

**Before you start**

- APPTOOLS-05 has passed (the agent can read its own room id).
- `Nova` and `Jarvis` both **Online**, Haiku/low.
- A bot `mybot` connected in a third terminal (`pwsh tools/echo-bot.ps1 -Name mybot`, `P-ECHO-BOT`).
- A three-member room containing `Nova` and `mybot` but NOT `Jarvis`. Create it via **New chat**: tick `Nova` and `mybot`, click **Start chat**.
- COST: one turn, plus one short turn for Jarvis's answer. Haiku at effort low.

**Steps**

1. Open the three-member room and write down its id from the `/rooms/<id>` URL.
2. Confirm the chat header `<h1>` reads `Nova, mybot` and the comma-joined member-names line under it (`Chat.razor:44`) lists `Nova` and `mybot` (the human's own name may also appear there; judge only the agents).
3. Click into the composer and type exactly: `@Nova use mcp__team__invite_agent to add Jarvis to this room. Take the room id from the label at the start of this message. When you are done, tell me the exact id you used.`
4. Press Enter and wait for the turn to finish.
5. WITHOUT pressing F5, confirm the sidebar entry for this room has renamed to `Nova, mybot, Jarvis`.
6. WITHOUT pressing F5, confirm the chat header `<h1>` has renamed to `Nova, mybot, Jarvis`.
7. Confirm the member-names line under the header now includes `Jarvis`.
8. Compare the id Nova reported in its reply against the id you wrote down from the URL, character for character.
9. In the console, find `Invited agent 'Jarvis' (<agentId>) into room '<roomId>'.` and confirm the `<roomId>` matches the URL id.
10. Click into the composer and type exactly: `@Jarvis are you there?` then press Enter.
11. Confirm a message row from `Jarvis` appears.

**Pass if — all of these**

- The sidebar entry and the header `<h1>` both change to `Nova, mybot, Jarvis` with NO page refresh.
- The member-names line includes `Jarvis`.
- The id Nova reports equals the id in the `/rooms/<id>` URL, character for character.
- The console shows `Invited agent 'Jarvis' (<agentId>) into room '<roomId>'.` with the matching room id.
- `@Jarvis are you there?` gets a reply from Jarvis, proving delivery reaches the newly invited member.

**Fail if — any of these**

- Nothing changes in the sidebar, and Nova's reply reports something like `Could not invite Jarvis: Unknown room '<whatever>'.` -> this is THE silent failure of this area. The tool returned ordinary result text, the model read it as prose, and nothing was logged as an error. Record the exact id string Nova used and compare it against the URL — a truncated, name-substituted or hallucinated id each point somewhere different.
- Nova asks YOU for the room id instead of reading its own label -> the `[Room: ...]` label is not reaching the prompt. Go back to APPTOOLS-05.
- Nova passes the room NAME (`Nova, mybot`) as the id -> it did not recognise the id in the label.
- Nova calls `create_room` instead and a SECOND room appears in the sidebar -> wrong tool chosen. The original room is unchanged; note both.
- The rename only appears after F5 -> a `RoomEvents` subscription bug, not a tool bug. Report it separately.
- `@Jarvis are you there?` gets no reply -> Jarvis is a member on paper but is not receiving the room's messages.

**Inconclusive if**

If `Jarvis` is **Offline** or **Degraded** on /teammates when you start, this test is INCONCLUSIVE — the invite may succeed while the reply step fails for an unrelated reason. Bring Jarvis Online (APPTOOLS-04) and rerun. If the model's reply reports success but you cannot find `Invited agent` in the console, do not conclude either way from the reply alone: the console line is the authority, and its absence means the invite did not happen regardless of what the model said.

> [!NOTE]
> Always write the URL id down BEFORE sending. Reading it afterwards, after the room has renamed, is how a mis-resolved id gets recorded as a pass.

### APPTOOLS-13 — invite_agent is idempotent and says so rather than duplicating a member

**💰 Spends money** · about 6 min

*Proves a repeat invite is a no-op that reports itself as text, not an exception and not a duplicated member.*

**Before you start**

- APPTOOLS-12 has passed: `Jarvis` is already a member of the room now named `Nova, mybot, Jarvis`.
- COST: one short turn on Haiku at effort low.

**Steps**

1. Open the room named `Nova, mybot, Jarvis` and note the exact text of the comma-joined member-names line under the Room title (`Chat.razor:44`) and of its header `<h1>`.
2. Click into the composer and type exactly: `@Nova add Jarvis to this room again with mcp__team__invite_agent, then tell me exactly what the tool said back.`
3. Press Enter and wait for the reply.
4. Confirm Nova quotes back text of the form `Jarvis is already a member of room '<id>'. Nothing to do.`
5. Confirm the member-names line is unchanged from what you noted.
6. Confirm the header `<h1>` still reads `Nova, mybot, Jarvis` and has not become `Nova, mybot, Jarvis, Jarvis`.
7. Confirm the sidebar entry for the room is likewise unchanged.
8. Confirm the console shows NO new `Invited agent 'Jarvis'` line for this attempt.

**Pass if — all of these**

- Nova quotes the `is already a member of room '<id>'. Nothing to do.` text.
- The member-names line, the header `<h1>` and the sidebar entry are all unchanged.
- No new `Invited agent` line in the console.

**Fail if — any of these**

- `Jarvis` appears twice in the member-names line -> the membership write is not idempotent and a room can accumulate duplicate members.
- The room renames to `Nova, mybot, Jarvis, Jarvis` -> the name is composed from a member list that now contains a duplicate.
- The turn ends with an error or the persona flips to **Degraded** -> these tools are designed to return errors as TEXT the model can read and correct itself from; a thrown exception here breaks that contract.
- A new `Invited agent 'Jarvis'` line appears in the console -> the already-a-member check ran after the write instead of before it.

**Inconclusive if**

If Nova paraphrases rather than quoting the tool result verbatim, that alone is not a failure — judge it on the three observable UI facts (member-names line, header, sidebar) plus the absence of the log line. If Nova declines to call the tool at all ('Jarvis is already here'), the test is INCONCLUSIVE: rerun once with `Call mcp__team__invite_agent with agent Jarvis and this room's id even though Jarvis is already here, and paste the exact result text.`

> [!NOTE]
> Cheap. Run it immediately after APPTOOLS-12 while the room is in the right state.

### APPTOOLS-14 — invite_agent with an unknown name comes back with the names that do exist

**💰 Spends money** · about 6 min

*Proves the self-correction affordance: an unknown-name error lists the real names, so a model can fix itself rather than guessing again or claiming success.*

**Before you start**

- Any room with a Persona that is **Online**.
- No agent or Persona named `Zephyr` exists. Confirm on /teammates and in the **New chat** panel.
- COST: one short turn on Haiku at effort low.

**Steps**

1. Open the room and note the exact text of its sidebar entry and header `<h1>`.
2. Click into the composer and type exactly: `Use mcp__team__invite_agent to add someone called Zephyr to this room, then tell me word for word what the tool replied.`
3. Press Enter and wait for the reply.
4. Confirm the reply quotes text of the form `Unknown agent 'Zephyr'. Agents that do exist: <names>.`
5. Confirm the `<names>` list contains the real agent names you can see on /teammates and in **New chat** — not an empty list and not `none`.
6. Confirm the sidebar entry and the header `<h1>` are unchanged.
7. Confirm the console shows NO `Invited agent` line.
8. Confirm the model either corrects itself using one of the listed real names, or reports the failure plainly. It must not claim the invite succeeded.

**Pass if — all of these**

- The tool result quoted back is `Unknown agent 'Zephyr'. Agents that do exist: <real names>.`
- The `<names>` list holds the actual agent names visible in the UI.
- The sidebar entry and header `<h1>` are unchanged and no `Invited agent` line appears.
- The model does not claim success.

**Fail if — any of these**

- A bare 'unknown agent' message with no list of alternatives -> the self-correction affordance is gone. This is the same affordance `create_room` gives, and losing it means a model that mistypes a name has no path back.
- The listed names are wrong, stale, or include names not present in the UI -> the directory lookup behind the error text disagrees with the directory the UI reads.
- The model reports that Zephyr was added -> it claimed success against a failed call. Verify against the sidebar (unchanged) and the console (no `Invited agent` line) and report it: a model that reports false success here will do so for every tool.
- The room renames or gains a member -> something was actually invited under a name that does not exist.

**Inconclusive if**

If an agent named `Zephyr` does exist in your environment (someone created one), this test is INCONCLUSIVE — pick a name you have confirmed is absent from BOTH /teammates and the **New chat** panel and rerun. If the model refuses to attempt the call at all ('there is no Zephyr, so I will not try'), that is INCONCLUSIVE for the tool's error text: rerun once with `Call mcp__team__invite_agent with agent "Zephyr" anyway and paste its exact result text.`

> [!NOTE]
> One short Haiku turn, so cheap and fast. It is the cheapest test of the error-as-text design that all seven chat tools share.

### APPTOOLS-15 — An agent's reply is delivered once — it must not also post the same text with post_message

**💰 Spends money** · about 6 min

*Proves the Replying rule holds against a real model. A double delivery also double-spends the Room Budget, because the Budget counts Messages and not Turns.*

**Before you start**

- `Nova` **Online**, Haiku/low. Use Nova's own two-member direct room (the one named just `Nova`), so no mention is needed.
- COST: one short turn on Haiku at effort low.

**Steps**

1. Open the room named `Nova` from the sidebar and note how many message rows it currently holds.
2. Click into the composer and type exactly: `In one sentence, what is this application for?`
3. Press Enter and wait for the turn to finish completely — wait an extra ten seconds after the streaming row disappears.
4. Count the message rows from `Nova`. Confirm exactly ONE new row was added.
5. Open `<data>\rooms\<roomId>.jsonl` (id from the `/rooms/<id>` URL) and confirm exactly one new line was appended for this turn, apart from your own question's line.
6. Look below the transcript for the grey budget note line. Confirm it reads `1 of 4 agent replies since you last spoke` (`P-LAUNCH-PAID` sets the Budget to 4; use your own number if you changed it), not `2 of …` (`Chat.razor:162`).

**Pass if — all of these**

- Exactly one new message row from `Nova`.
- Exactly one new agent-authored line in the room's `.jsonl` file.
- The budget note shows the reply was counted once.

**Fail if — any of these**

- Two identical messages from `Nova` seconds apart -> the model both replied and called `post_message` for the same room. The system prompt and get_help both say a reply is just answer text; this is a real finding about the wording. It also double-spends the Budget, which will make every budget test read wrong.
- The budget note reads `2 of …` after a single question -> the same double-delivery, visible even if the two rows look slightly different.
- Two lines in the `.jsonl` with the same text and different ids -> confirms the double post at the storage layer.

**Inconclusive if**

If the model legitimately sends two DIFFERENT messages (an answer plus a follow-up question), that is not this failure — judge only on duplicate text. If the room already had a spent budget from an earlier test, the reply may be refused entirely and no row appears at all; that is INCONCLUSIVE here — type any human message first to reset the budget, then rerun.

> [!NOTE]
> Worth re-running specifically after any edit to the **Replying** prompt (getHelp.replying) or the **Tools** prompt (systemPrompt.tools) — those two are the only text that carries this instruction.

### APPTOOLS-16 — create_room makes a new Room that appears in the sidebar live, named after its Agents

**💰 Spends money** · about 10 min

*Proves the create path works end to end and that the room list updates from events rather than needing a refresh.*

**Before you start**

- `Nova` and `Jarvis` both **Online**, Haiku/low.
- COST: one turn on Haiku at effort low.

**Steps**

1. Open the room named `Nova` (Nova's own two-member direct room).
2. Write down every entry currently in the sidebar room list.
3. Click into the composer and type exactly: `Use mcp__team__create_room to start a new room with Jarvis, then tell me the id it returned.`
4. Press Enter and wait for the turn to finish.
5. WITHOUT pressing F5, confirm exactly one NEW entry has appeared in the sidebar room list.
6. Confirm the new entry is named `Nova, Jarvis` — the calling Agent first, then the named one. The human is a member but is deliberately NOT in the name.
7. Confirm Nova's reply quotes a result of the form `Created room '<name>' (id <id>).`
8. Click the new sidebar entry.
9. Confirm the browser URL is `/rooms/<id>` with the same id Nova reported.
10. Read the member-names line under the Room title (a plain text line, `Chat.razor:44`). Confirm it lists `Nova` and `Jarvis` among three comma-separated names — the third is the Human — so the room is mention-gated. Judge only the agent names.
11. In the console, find a line of the form `Created room '<roomId>' (<name>) with 3 members.`

**Pass if — all of these**

- Exactly one new sidebar entry appears with NO page refresh.
- The new entry is named `Nova, Jarvis`.
- Nova's reported id matches the `/rooms/<id>` URL of the new room.
- The member-names line lists three names: `Nova`, `Jarvis` and the Human.
- The console shows `Created room '<roomId>' (<name>) with 3 members.`

**Fail if — any of these**

- Nothing appears in the sidebar until you press F5 -> `RoomEvents` is not publishing the change, or `RoomList.razor` is not subscribed. The human never sees a room an agent created.
- The result is `Unknown agent(s): <name>. Agents that do exist: …` -> the model passed a name that does not exist. Note the name it used; check whether it invented one or used an alias that should have resolved.
- The new room's name includes `You` or the human's name -> the naming rule (agents only) has regressed.
- The member-names line shows only two names -> the human was not added, and mention-gating will behave unexpectedly.
- No `Created room` line in the console while the model claims success -> the model reported a room it did not create.

**Inconclusive if**

If you ask an agent to create a room containing ONLY ITSELF, the call falls through to the single-agent branch and returns the agent's EXISTING direct room rather than creating a new one. That is correct behaviour and is easily mistaken for a defect — if the model chose to name only itself, the test is INCONCLUSIVE: rerun naming `Jarvis` explicitly. If `Jarvis` is Offline, creation should still succeed (a Persona can be a member without being online) but the room will be quiet; judge only the creation, not any reply.

> [!NOTE]
> Write down the sidebar contents before sending. 'A new entry appeared' is only checkable against a list you recorded first.

### APPTOOLS-17 — post_message delivers into a Room other than the one the agent was addressed in, and the agent does not answer its own post

**💰 Spends money** · about 12 min

*Proves the one capability that only post_message provides — speaking into another Room — and, in the same turn, proves an Agent never receives its own Message, which is the structural guard against echo loops.*

**Before you start**

- `Nova` and `Jarvis` both **Online**, Haiku/low.
- The room Budget is 2 for this test: `P-STOP`, then repeat the `P-LAUNCH-PAID` launch block with `$env:Team__AgentMessageBudget = '2'` (its Chief of Staff step is already done). The test deliberately hunts an echo loop, so the Budget is the only thing bounding the bill.
- COST: one turn on Haiku at effort low. Combining create_room and post_message in one turn keeps this to a single turn.

**Steps**

1. Open the room named `Nova` (Nova's direct room) and write down the sidebar room list.
2. Click into the composer and type exactly: `Use mcp__team__create_room to start a room with Jarvis, then use mcp__team__post_message to post the exact text "kickoff" into that new room. Reply here with only the new room's id.`
3. Press Enter and wait for the turn to finish.
4. Confirm the CURRENT room (`Nova`) shows Nova's plain reply — the id — as an ordinary message row, and that the word `kickoff` does NOT appear in this room.
5. Confirm a new entry has appeared in the sidebar with no refresh.
6. Click the new sidebar entry.
7. Confirm its URL `/rooms/<id>` matches the id Nova reported.
8. Confirm the new room contains a message row whose sender is `Nova` and whose body is exactly `kickoff`.
9. Wait sixty seconds with this new room open. Confirm NO further messages appear — in particular, confirm Nova does not reply to its own `kickoff` post.
10. Open `<data>\rooms\<newRoomId>.jsonl` and confirm it holds exactly one line, whose sender name is `Nova`.
11. Refresh that file after another minute and confirm the line count has not grown.
12. Run `O-TOOLCALLS` for `Nova` and read the lines for this turn. Confirm one line whose `tool` field contains `create_room` and one whose `tool` field contains `post_message`. The console cannot show this: the Trace `replied to tools/call` line carries the response, never the tool called, and the `tool` value is the title the Adapter gave the call, so judge by containment.

**Pass if — all of these**

- `kickoff` appears in the NEW room, not in the room Nova was addressed in.
- Nova's plain reply (the id) appears in the original room as a separate, ordinary message.
- The reported id matches the `/rooms/<id>` URL of the new room.
- `<data>\rooms\<newRoomId>.jsonl` holds exactly one line, sender `Nova`, and does not grow.
- No second draft row, no echo, and no follow-up turn from Nova in the new room.

**Fail if — any of these**

- `kickoff` lands in the CURRENT room instead -> the model ignored the `roomId` argument, or passed the current room's id. Note which id it used.
- The result is `Could not post the message: '<agentId>' is not a member of room '<id>'.` -> it posted into a room it is not in. That is a real limit of the tool, not a crash; note the room id it tried.
- Nothing appears in the new room while the model claims success -> the post silently failed. Check the `.jsonl` file: if it is empty, the model reported a post that never happened.
- Nova replies to its own `kickoff` post, or the `.jsonl` line count keeps growing -> the echo-loop guard has failed. This is severe: a recorded failure of this shape once produced 4299 messages in two seconds. If the budget does not stop it either, that is a second and separate severe finding — report both.
- Two agents in the new room begin quoting each other's `@name` and trading replies -> the related hazard; the Budget should halt it, and if it does not, report that too.

**Inconclusive if**

If the model creates the room but stops before posting, the test is INCONCLUSIVE for post_message: the create half passed (record it against APPTOOLS-16) and you should rerun the post half alone with `Use mcp__team__post_message to post the exact text "kickoff" into room <id>.` using the id from the sidebar. If Jarvis happens to reply to `kickoff` in the new room, that is Jarvis, not Nova — it is a three-member room so Jarvis should only reply when mentioned; note it as a separate mention-gating observation, not as an echo loop.

> [!NOTE]
> This is the only test that proves the post_message capability at all, and it folds in the 'never receives its own Message' check at no extra cost. Run it before the other budget tests. A loop caught at 2 agent Messages proves the same defect as one caught at the default 40, at a twentieth of the cost.

### APPTOOLS-18 — Tools are the source of truth — the agent must not answer about agents or Rooms from the codebase

**💰 Spends money** · about 10 min

*Catches an answer that looks right and is not: the working directory is not a sandbox, and an agent that greps the repository will produce fixture names that read as plausible.*

**Before you start**

- `Nova` **Online**, Haiku/low.
- The app is running from a checkout of this repository, and `<data>\Teammates\Nova\work\` is Nova's working folder, which is not a sandbox: `Bash`, `Grep` and `Read` can reach the repository's `docs/` and `tests/`, which hold fixture names. That is the realistic case the test is for.
- No Trace logging is needed. `O-TOOLCALLS` for `Nova` is the oracle for which tool ran.
- COST: two short turns on Haiku at effort low.

**Steps**

1. Open http://localhost:5100/teammates and write down every tile name and status, and open **New chat** and write down every checkbox name. Close the panel.
2. Open the room named `Nova`.
3. Click into the composer and type exactly: `Do not read any files. Using only your tools, tell me who is in this room and what other agents exist.`
4. Press Enter and wait for the reply. Write the answer down.
5. Run `O-TOOLCALLS` for `Nova` and confirm a line for this turn whose `tool` field contains `list_agents` (judge by containment; the value is the title the Adapter gave the call). The built-in Chief of Staff is a real Teammate on every install, so it belongs in the list you wrote down in step 1 and in both answers.
6. Click into the composer again and type exactly: `Who is in this room, and what other agents exist?`
7. Press Enter and wait for the reply.
8. Compare both answers against the list you wrote down in step 1.
9. Run `O-TOOLCALLS` for `Nova` again and confirm whether a line containing `list_agents` was added during the SECOND turn as well.

**Pass if — all of these**

- Both answers match the live UI exactly — every name present, no name absent, no extra name.
- `O-TOOLCALLS` has a `list_agents` line for both turns.
- Neither answer mentions a file path, a source file, or a shell command.

**Fail if — any of these**

- Either answer names `Alice`, `Bob` or `Jarvis` when no such Persona exists in your run, or a room called `bananas` -> these are fixture names that live in `docs/` and `tests/`. The agent ran `Bash`, `Grep` or `Read` over the repository instead of calling the tool, and produced a plausible-looking wrong answer. Both the system prompt and get_help's footer forbid this, so this is a real finding about the wording, not about the tools.
- The second answer differs from the first -> the agent used a different source of truth when not explicitly forbidden from reading files. Report both answers.
- The model names files it read, or quotes source code -> the same failure, stated openly.
- `O-TOOLCALLS` shows no `list_agents` line for a turn while the model nevertheless produces names -> it answered from the codebase or from memory. This is the cleanest signal available.

**Inconclusive if**

If your Persona names genuinely ARE `Nova` and `Jarvis` (as the setup suggests), you cannot distinguish a fixture name from a real one by name alone — that is why `O-TOOLCALLS` matters here. The console cannot settle it at any log level: `App tool server received tools/call.` names the method, not the tool, and the Trace `replied to` line carries the response, never the tool called. If `O-TOOLCALLS` has no log folder for `Nova` at all, the Persona made no tool call and the answer came from somewhere else; judge it as the Fail above. To make the test decisive, create one Persona with an unmistakably unique name (e.g. `Quillhaven`) before running it — no fixture anywhere in the repo carries that name.

> [!NOTE]
> The unique-name trick in the INCONCLUSIVE note turns this from a judgement call into a fact. Consider doing it as standard.

### APPTOOLS-19 — A budgetExhausted refusal from post_message is terminal: the agent stops, does not retry and does not reroute

**💰 Spends money** · about 15 min

*The only check that exists that a real model obeys the terminal wording of a budget refusal. No automated test can prove this, and the failure — minting a fresh Room to carry on in — is invisible except in the sidebar.*

**Before you start**

- `Nova` **Online**, Haiku/low.
- The room Budget is 2 for this test: `P-STOP`, then repeat the `P-LAUNCH-PAID` launch block with `$env:Team__AgentMessageBudget = '2'` (its Chief of Staff step is already done). Without it the Budget is the `P-LAUNCH-PAID` value of 4, so the room would not stop at two messages.
- Use Nova's own two-member direct room (`Nova`), so no mention is needed and the human is the only other member.
- COST: one turn on Haiku at effort low, which deliberately spends the room's whole budget. Keeping the budget at 2 keeps this to a single turn's work.

**Steps**

1. `P-STOP` the app.
2. Repeat the `P-LAUNCH-PAID` launch block, adding `$env:Team__AgentMessageBudget = '2'` before the `dotnet run` line.
3. Wait for the `Nova` tile on /teammates to read **Online**.
4. Open the room named `Nova` and write down the sidebar room list.
5. Click into the composer and type exactly: `Using mcp__team__post_message, post four separate short messages into this room, one at a time, with the texts "one", "two", "three" and "four". Stop and tell me if anything refuses you.`
6. Press Enter and watch the transcript.
7. Count the agent message rows that appear. Confirm exactly TWO appear, with bodies `one` and `two`.
8. Confirm an amber warning panel (`Severity.Warning`, class `budget-prompt`, `Chat.razor:132`) appears between the transcript and the composer reading `Agents have sent 2 replies since you last spoke, and are paused.` (`Chat.razor:134`) with two buttons, **Continue** and **Leave paused**.
9. Confirm NO third or fourth message body (`three`, `four`) appears in this room.
10. Compare the sidebar room list against what you wrote down. Confirm NO new room has appeared.
11. In the console, find lines of the form `Room '<roomId>' refused a message from 'Nova': its budget of 2 agent messages since a human last spoke is spent.` (Warning, `ChatService.cs:682`). Count them.
12. Confirm you see ONE or TWO such lines, not a long run of them.
13. In the console, also look for `Persona 'Nova' had a message refused: budgetExhausted - …` (Warning, `PersonaRunner.cs:690`) — this is Nova's own explanatory reply being dropped, and it is correct.
14. Open `<data>\rooms\<roomId>.jsonl` and confirm it holds exactly two agent-authored lines for this turn (plus the Human's question line).
15. Return to /teammates and confirm the `Nova` tile still reads **Online** and has NOT gone **Degraded**.

**Pass if — all of these**

- Exactly two agent messages land: `one` and `two`.
- The amber panel reads `Agents have sent 2 replies since you last spoke, and are paused.` with **Continue** and **Leave paused** buttons.
- No third or fourth message lands anywhere.
- No new room appears in the sidebar.
- The console shows a small number of refusal warnings (one or two), not a run of them.
- `<data>\rooms\<roomId>.jsonl` holds exactly two agent-authored lines for this turn.
- The `Nova` tile stays **Online** — a spent Budget is not a fault.

**Fail if — any of these**

- A third or fourth message actually lands -> the cap is not being enforced at the post path.
- A NEW room appears in the sidebar containing `three` or `four` -> the agent worked around the refusal by creating a fresh Room, which starts with a full Budget. `create_room` is uncapped by design, so the terminal wording is the only thing standing between this and an unbounded spend. This is the most important failure this test can find.
- A long run of refusal warnings in the console -> the agent retried repeatedly despite being told the refusal is final.
- The `Nova` tile flips to **Degraded** -> a spent Budget is being reported as a fault, which it is not; a Degraded badge here marks an agent that is working exactly as designed.

**Inconclusive if**

THE CONFUSING PART, and it is NOT a bug: after the refusal, Nova's own explanatory reply is ALSO an agent message and is also refused, so its answer never reaches the transcript. You will see a streaming draft row build up and then vanish leaving no message behind. That is correct — do not record it as a failure. If the model only posts once and then stops on its own, the budget was never actually reached: that is INCONCLUSIVE for this test (it proves nothing about the refusal wording); rerun with the same prompt. If the app was not relaunched with `Team__AgentMessageBudget` set to 2, you will be running against the `P-LAUNCH-PAID` value of 4 and the panel will say 4, not 2 — stop and relaunch instead.

> [!NOTE]
> Setting the budget to 2 before launching is not optional. Also, the Budget counter is in memory and per Room: restarting the app un-pauses every Room and shows a fresh Budget over a transcript that already spent one. Do not restart mid-test.

### APPTOOLS-20 — A turn declined for Budget never reaches the model at all — the decline path is not the refusal path

**Free** · about 8 min

*Proves the two budget paths are distinct and correctly ordered. Reversing the ordering in ReplyGate silently exempts every mentioned Agent from the cap, and nothing on screen would show it.*

**Before you start**

- This test relaunches the app on its own, so it does not depend on APPTOOLS-19 (a relaunch clears that test's in-memory pause). Run APPTOOLS-21 right after APPTOOLS-19, and this one separately.
- `P-STOP`, then `P-LAUNCH-UAT` with `$env:Team__AgentMessageBudget = '1'` set first, so a single agent message spends the whole Budget.
- `mybot` running (`P-ECHO-BOT`, `pwsh tools/echo-bot.ps1 -Name mybot`). Ada is the built-in mock agent from `P-LAUNCH-UAT`.
- Free: the decline happens before any prompt is sent, and the mock and echo agents bill nothing.

**Steps**

1. `P-STOP` the app, set `$env:Team__AgentMessageBudget = '1'`, then launch with `P-LAUNCH-UAT`. Start `mybot` in its own terminal (`P-ECHO-BOT`).
2. Click **New chat**, choose `Ada` and `mybot` as members and create the room. It has three members, so an agent replies only when mentioned.
3. Type `@mybot hello` and press Enter.
4. Confirm `mybot` replies once. That is agent message 1 of 1, so the Budget is now spent.
5. Confirm an amber warning panel (`Chat.razor:132`) appears reading `Agents have sent 1 replies since you last spoke, and are paused.` (`Chat.razor:134`).
6. Click **Leave paused**. Confirm the panel is replaced by a grey line reading `Paused — 1 of 1 agent replies since you last spoke.` (`Chat.razor:158`).
7. Confirm no streaming draft row appears and no tool-activity line appears, and that Ada posts nothing.
8. In the console, find a line of the form `Persona 'Ada' declined a turn in room <roomId>: the room has spent its budget of 1 agent messages.` (Warning, `PersonaRunner.cs:648-652`). Ada was not mentioned, yet the line names her: `ReplyGate.Decide` checks the Budget first for every member (`ReplyGate.cs:73-82`), before the mention rule.
9. Confirm that line is at Warning level and that NO prompt was sent for it (there is no streaming row and no `tools/call` in the log at that moment).
10. Optionally, earlier in the log, find the Information line `Persona 'Ada' read a message in room <roomId> as context only: it was not mentioned and the room has 3 members.` for your `@mybot hello` message (`PersonaRunner.cs:634`).

**Pass if — all of these**

- The amber panel reads `Agents have sent 1 replies since you last spoke, and are paused.`
- **Leave paused** replaces it with `Paused — 1 of 1 agent replies since you last spoke.`
- No draft row and no tool activity appear in the paused room.
- The console carries `Persona 'Ada' declined a turn in room <roomId>: the room has spent its budget of 1 agent messages.` at Warning level.
- No message from Ada saying it is paused, and none at all.

**Fail if — any of these**

- A streaming draft row appears in a paused room -> the Budget check is running AFTER the Room rule instead of before it. That ordering is the rule, because reversing it silently exempts every mentioned Agent from the cap. This costs real money every time it happens and produces no error.
- Ada posts a message explaining that it is paused -> it WAS prompted, which contradicts the decline path entirely.
- No `declined a turn` line naming Ada appears after mybot's reply -> the decline is happening silently, or not at all.

**Inconclusive if**

Testers commonly expect the paused agent to 'say it is paused'. It cannot, and must not — it was never prompted. Do not record its silence as a defect. If `mybot` never replies to `@mybot hello`, no Budget was spent and there is nothing to decline: the test is INCONCLUSIVE (check the bot terminal), not passed. Typing another Human message into the room RESETS the Budget and ends the pause — do not do it while this test is running.

> [!NOTE]
> A two-member room cannot reach this state: no Message is ever delivered to a paused two-member room, because the Budget gate runs on delivery (`PersonaRunner.cs:643-653`). The third member (Ada) is what receives mybot's reply.

### APPTOOLS-21 — Continue re-delivers the paused Message, and the prompt survives a page reload

**Free** · about 8 min

*Proves the pause prompt is read from state rather than from an event (so it survives prerender), that the Continue button cannot be double-clicked into granting two budgets, and that Continue on a room whose last Message is a teammate's own says so and wakes nobody.*

**Before you start**

- Run this right after APPTOOLS-19, in the same app run: the room `Nova` is paused at Budget 2 and its last stored Message is Nova's own `two`. Do not relaunch (that clears the in-memory pause). If APPTOOLS-20 or anything else relaunched the app, redo APPTOOLS-19 first.
- Free: the last Message is Nova's own, and a teammate is never delivered its own Message (`ChatService.cs:340-385`), so Continue grants Budget but wakes nobody and sends no prompt.

**Steps**

1. With the room paused and the amber warning panel showing, press F5 FIRST, before clicking anything.
2. Confirm the panel with `Agents have sent 2 replies since you last spoke, and are paused.` and the **Continue** / **Leave paused** buttons is STILL there after the reload.
3. Confirm the panel also carries the warning `Continue will deliver the last message again, and it is a teammate's own — a teammate is never delivered its own message, so this will wake nobody. Say something instead.` BEFORE any click (`Chat.razor:140-145`, `ContinueWouldWakeNobody`).
4. Click **Continue** once. The button may read `Continuing…` and be disabled, but only for a moment now that no agent runs; do not fail the test if you do not catch it. The single `extended to 4` line in step 8 is the guard against a double grant.
5. Confirm the blue note `Budget granted, but there was nobody to wake. Say something to start the room again.` appears below the transcript (`Chat.razor:174-176`).
6. Confirm the grey budget line now reads `2 of 4 agent replies since you last spoke.` (`Chat.razor:162`).
7. Confirm no agent message, no streaming draft row and no tool activity appears: Nova was not woken.
8. In the console, find a line of the form `Room '<roomId>' was extended to 4 agent messages.` (Information, `ChatService.cs:689`). Confirm exactly ONE such line appeared for your single **Continue** press.

**Pass if — all of these**

- The panel survives F5 — it is still on screen after the reload, before any click.
- The wake-nobody warning shows before the click.
- After **Continue**, the blue `Budget granted, but there was nobody to wake…` note and the grey `2 of 4 agent replies since you last spoke.` line appear.
- No agent message, draft or tool activity appears.
- Exactly one `Room '<roomId>' was extended to 4 agent messages.` line per Continue press.

**Fail if — any of these**

- The panel VANISHES after F5 -> the budget is being read from an event rather than from the room's stored state, so a reload loses the only control the human has for un-pausing. Report it against `Chat.razor`'s prerender path.
- The wake-nobody warning is absent before the click -> `ContinueWouldWakeNobody` is not seeing that the last Message is the teammate's own.
- Two `was extended to` lines appear for one click -> two budgets were granted from one intent.
- Any agent reply, draft row or tool activity appears -> a teammate was delivered its own Message.

**Inconclusive if**

The room is not paused, or its last stored Message is not Nova's own (a Human message was typed since APPTOOLS-19, which resets the Budget and ends the pause). Redo APPTOOLS-19 and run this right after it. Waking an agent from a paused room is not reachable manually when the last Message is the teammate's own; do not try to force it.

> [!NOTE]
> The F5 must come BEFORE the first click. Clicking Continue and then reloading tests nothing.

### APPTOOLS-22 — Editing a prompt changes model-facing text without restarting the session, and Next session prompts wait for a restart

**💰 Spends money** · about 15 min

*Proves the two prompt timings behave as badged, and — more important — that editing a prompt NEVER restarts a teammate and never costs it its conversation memory.*

**Before you start**

- `Nova` **Online**, Haiku/low, and has already had at least one exchange in its room so it has something to remember.
- APPTOOLS-01 and APPTOOLS-02 have passed, so you know the Settings page and Reset work.
- The app was started with `${env:Logging__LogLevel__Agency.Huddle} = "Trace"` (a Trace launch of `P-LAUNCH-PAID`): the oracle for what `get_help` returned is the Trace line `App tool server replied to tools/call:` (`AppToolServer.cs:250`), because the Adapter defers tool descriptions and the model cannot be asked what one says.
- COST: three or four short turns on Haiku at effort low (one before the edit, two or three after).

**Steps**

1. In the room named `Nova`, type exactly `Remember this word: pumpkin.` and press Enter. Wait for the reply.
2. Go to http://localhost:5100/settings, Prompts tab.
3. Find the **Help: budget** field (a LIVE field — it carries no **Next session** badge). Click into it, press Ctrl+A, and paste its existing text back with one change: add a new final line reading exactly `The word of the day is marmalade.`
4. Find the **list_agents description** field (a **Next session** field). Click into it, press End, and append exactly ` The word of the day is marmalade.`
5. Confirm both fields show an **Unsaved** badge, then click **Save**.
6. Confirm both fields now show **Modified** and neither shows **Unsaved**.
7. Open `<data>\prompts.json` and confirm it contains exactly two keys: `getHelp.budget` and `tool.listAgents.description`, and no others.
8. Read the app's console window. Confirm NO restart of the `Nova` runner is logged, and that the `Nova` tile on /teammates still reads **Online**.
9. Return to the `Nova` room and type exactly `What word did I ask you to remember?` then press Enter.
10. Confirm Nova answers `pumpkin` — its conversation memory survived the prompt save.
11. Now type exactly: `Call mcp__team__get_help and tell me the last line of its BUDGET section, word for word.` and press Enter.
12. Run `O-TOOLCALLS` for `Nova` and confirm a new line whose `tool` field contains `get_help`. In the console, find the newest `App tool server replied to tools/call:` line, decode its payload (newlines appear as `\n`, apostrophes as `'`) and confirm the BUDGET section ends with `The word of the day is marmalade.` — the LIVE prompt took effect on the very next tool call, with no restart. (Nova's own quote of it is a bonus, not the oracle.)
13. In that same decoded payload, find the TOOLS entry for `list_agents`. Confirm its description does NOT end with `marmalade`: that prompt is badged **Next session**, and `GetHelpTool` freezes the descriptions when the session is constructed, before your edit.
14. Go to /teammates, click the `Nova` tile, and click **Restart** on the card. Wait for the tile to return to **Online**.
15. Back in the room, type exactly `Call mcp__team__get_help again and list its TOOLS section.` and press Enter. Run `O-TOOLCALLS` and confirm another `get_help` line for this turn.
16. Decode the newest `App tool server replied to tools/call:` payload and confirm the `list_agents` entry in its TOOLS section NOW ends with `The word of the day is marmalade.`
17. Return to /settings, click **Reset** on both edited fields, and click **Save**. Confirm `prompts.json` returns to `{}`.

**Pass if — all of these**

- Saving the prompts does NOT restart the teammate: no restart in the log, tile stays **Online**, and Nova still remembers `pumpkin`.
- `prompts.json` holds exactly the two changed keys.
- The LIVE prompt (**Help: budget**) shows up in the decoded `get_help` payload on the very next call, with no restart.
- The **Next session** prompt (**list_agents description**) is absent from the TOOLS entry before the restart, and present after a **Restart**.
- Reset + Save returns `prompts.json` to `{}`.

**Fail if — any of these**

- Nova has forgotten `pumpkin` after the prompt save -> editing a Prompt restarted the session and destroyed its conversation memory. That is a defect: editing a Prompt must never restart a session. Editing a Persona, or changing its Model or Effort, is the only mechanism that legitimately does.
- The LIVE prompt change does NOT appear in `get_help`'s output even after several calls -> live prompts are not live; the store is caching the session's copy.
- The **Next session** prompt DOES appear in the `list_agents` TOOLS entry before the restart -> the badge is lying about the timing, which is the opposite error but equally misleading.
- It is STILL absent after the **Restart** -> the new session did not pick the edited description up.
- `prompts.json` contains keys you did not change -> the save is writing every field rather than just the edited ones.

**Inconclusive if**

A **Next session** prompt silently having no effect on a running teammate is EXPECTED and is exactly why those fields are badged — do not record it as a failure. If Nova cannot quote the last line of the BUDGET section verbatim, that is a model-following weakness rather than a prompt failure: the decoded Trace payload shows the literal text the tool returned, and you judge from that. If no `get_help` line appears in `O-TOOLCALLS` for a turn, Nova answered without the tool and that turn proves nothing: ask again naming the tool. If the app was not launched at Trace there is no payload to decode and steps 12, 13 and 16 are INCONCLUSIVE — relaunch at Trace, which clears nothing on disk but ends Nova's session, so repeat from step 1.

> [!NOTE]
> Always clean up with the final Reset + Save. A leftover prompt override silently changes the text every later test reads, and `prompts.json` is not obvious to the next tester.

### APPTOOLS-23 — follow_room wakes an agent that was not mentioned, and a non-follower in the same Room is the control

**💰 Spends money** · about 15 min

*The core proof of roadmap item 8: a following Agent takes a Turn on every Message in its Room, mentioned or not. A non-follower Member who is likewise not mentioned must stay silent in the same turn — that silence is the control that makes Nova's reply mean anything, rather than "everyone in this Room replies to everything."*

**Before you start**

- `Nova` and `Jarvis` both **Online**, Haiku/low. A bot `mybot` connected in a third terminal (`pwsh tools/echo-bot.ps1 -Name mybot`, `P-ECHO-BOT`) as the free control.
- A four-member room containing `Nova`, `Jarvis` and `mybot`: click **New chat**, tick all three, click **Start chat**.
- Neither Persona has restarted since this room was created — a restart clears every follow (see the Inconclusive note).
- COST: two short turns on Haiku at effort low — one for the `follow_room` call, one for the test message.

**Steps**

1. Open the four-member room and confirm the member-names line above the transcript (`Chat.razor:44`) lists the Human, `Nova`, `Jarvis` and `mybot`.
2. Write down the room id from the `/rooms/<id>` URL.
3. Click into the composer and type exactly: `@Nova use mcp__team__follow_room to follow this room, using the room id from your own [Room: ...] label. Tell me exactly what it said back.` then press Enter.
4. Wait for Nova's reply. Confirm it quotes `Now following room '<name>' (id <id>)` (`FollowRoomTool.cs:64`) with the id you wrote down.
5. Note how many message rows the room currently holds.
6. Click into the composer and type exactly: `@Jarvis, without calling any tool, reply with the single word "present".` then press Enter.
7. Wait for every streaming row to finish, then wait an extra ten seconds.
8. Confirm a message row from `Jarvis` appears containing `present`.
9. Confirm a SEPARATE message row from `Nova` ALSO appears, even though step 6's message did not name Nova. A follower takes a Turn on EVERY Message (`ReplyGate.cs:82`), including Jarvis's own `present`, so Nova may reply twice (once to your message, once to Jarvis's); that is expected.
10. Confirm NO message row from `mybot` appears anywhere in this room since step 6 — the control: `mybot` was not mentioned and holds no `follow_room` tool, so it must stay exactly as silent as it would have before roadmap item 8 shipped.
11. Open `<data>\rooms\<roomId>.jsonl` and confirm that since step 6 it gained the Human line, at least one new line with sender `Nova` (likely two) and one with sender `Jarvis`, no line from `mybot`, and at most four agent-authored lines in total (the Budget is 4).

**Pass if — all of these**

- Nova's `follow_room` call succeeds and quotes the room id back.
- `Jarvis` replies to being mentioned (expected, and not what this test is about).
- `Nova` ALSO replies to the same message, despite never being named in it.
- `mybot` — the control — stays silent throughout.
- The room's `.jsonl` file gained at least one `Nova` line and one `Jarvis` line, none from `mybot`, and no more than four agent lines.

**Fail if — any of these**

- `Nova` stays silent on the un-mentioning message -> `follow_room` is not reaching `ReplyGate.Decide`'s `following` argument, or `RoomFollows` never recorded the follow. Roadmap item 8 is not wired end to end, and this is the one finding this test exists to catch.
- `mybot` also replies -> the control itself is broken, or step 6's text accidentally mentioned it — re-read exactly what you sent. A failing control means this run proves nothing either way; do not record the Nova half as a pass.
- Nova's reply is a duplicate of an earlier answer, or otherwise reads as though it silently reused a cached reply rather than actually taking a fresh Turn -> report the reply text verbatim.
- The `.jsonl` file gains any line from `mybot` -> the same failure as the on-screen one, confirmed at the storage layer; report both facts together.

**Inconclusive if**

If Nova's `follow_room` call fails or Nova answers in prose without calling the tool, retry once naming the tool explicitly, the same escalation APPTOOLS-06 uses. If either Persona restarted for any reason between step 3 and step 6 — including an unrelated crash or an app restart — the follow was silently cleared (`RoomFollows.ClearAgent` runs on every runner's handshake, by design) and the result is meaningless; redo the `follow_room` call and rerun step 6 without restarting anything in between. If Nova's reply happens to answer as though it had been directly asked something, that is not this failure — judge only on whether Nova posted at all.

> [!NOTE]
> Keep this room and Nova's follow open for APPTOOLS-24, which is the direct continuation of this test. Do not restart the app or either Persona in between.

### APPTOOLS-24 — unfollow_room restores the ordinary mention-only rule, and the Room confirms the decline was not a silent drop

**💰 Spends money** · about 10 min

*The counterpart proof: after `unfollow_room`, an unaddressed Message produces silence from the Agent that used to be woken by it. As of issue #40 the Room also shows a quiet context-only note on the Human's own unaddressed Message, confirming delivery happened and every reachable Agent's Reply Gate declined — rather than the Message having gone nowhere.*

**Before you start**

- APPTOOLS-23 has just passed, in the same four-member room, with `Nova` still following and neither Persona restarted since.
- COST: two short turns on Haiku at effort low.

**Steps**

1. In the same room, type exactly: `@Nova use mcp__team__unfollow_room to stop following this room, using the room id from your own label. Tell me exactly what it said back.` then press Enter.
2. Wait for Nova's reply. Confirm it quotes `No longer following room` (`UnfollowRoomTool.cs:53`) and the room id.
3. Note how many message rows the room currently holds.
4. Click into the composer and type exactly: `Just checking in here — no action needed from anyone in particular.` (a message that names nobody at all) and press Enter.
5. Immediately look at the area between the transcript and the composer, without waiting for any reply.
6. Wait thirty seconds, watching the transcript continuously.
7. Confirm NO new message row appears from `Nova`, `Jarvis` or `mybot`.
8. In the console (`T-A`), find the Information line `Persona 'Nova' read a message in room <roomId> as context only: it was not mentioned and the room has <n> members.`
9. Open `<data>\rooms\<roomId>.jsonl` and confirm exactly ONE new line was appended since step 3: the Human's own message from step 4 (every Message is stored, `ChatService.cs:273`), with no new line from `Nova`, `Jarvis` or `mybot`.

**Pass if — all of these**

- `unfollow_room` succeeds and Nova's reply confirms it.
- Step 4's unaddressed message produces no reply from anybody, including `Nova`.
- The area below the transcript shows the quiet note `No teammate was @-mentioned — name one to ask for a reply.` immediately after step 4, with no reload — the issue #40 confirmation that the Message was delivered and every reachable Agent's Reply Gate declined, rather than nothing having happened at all.
- `T-A` carries the context-only Information line for `Nova` (`PersonaRunner.cs:634`).
- Exactly one new `.jsonl` line appears for step 4, sender the Human, and none from any agent.

**Fail if — any of these**

- `Nova` still replies to the unaddressed message -> `unfollow_room` did not clear the follow, or `ReplyGate.Decide` is not re-checking `following` on the very next Message; the ordinary mention-only rule has not been restored. This is the direct counterpart of APPTOOLS-23's core failure.
- The quiet note is absent after step 4 -> either the note regressed (report against `Chat.razor`'s `outlook` handling) or something DID reply and you missed it — re-check the transcript before concluding the note itself is broken.
- No context-only Information line for `Nova` appears in `T-A` -> the Message was never delivered to Nova's read loop at all, which is a worse and different failure than a following bug: a Member is not receiving the Room's ordinary traffic. Report it as a delivery defect, not a following defect.

**Inconclusive if**

If you cannot locate the quiet note, judge the reply-gate result (silence from every agent, plus `T-A`'s context-only line for Nova) on its own merits and record the UI-note half separately as inconclusive, rather than failing the whole test over a UI element you could not find. A Persona restart between APPTOOLS-23 and this test clears the follow and invalidates both; rerun APPTOOLS-23 first without restarting.

### APPTOOLS-25 — Following does not buy a Turn past the Budget

**💰 Spends money** · about 10 min

*The ordering test, and the one that matters most: `ReplyGate.Decide` checks the Budget before it checks `following`, so a follower is capped exactly like a Mentioned Agent. `ChatService` increments the count before it publishes, so the reply that spends the Budget is delivered to everyone else already carrying `Used == Granted`. Which agent's reply spends the single Budget is a race (see step 4), so only one half of the run is the oracle.*

**Before you start**

- APPTOOLS-23 has passed in this session. It is the control: it proves `Nova` DOES reply unmentioned while following, so the silence below can only be the cap.
- `P-STOP` the app. Repeat the `P-LAUNCH-PAID` launch block with `$env:Team__AgentMessageBudget = '1'` set before `dotnet run` (its Chief of Staff step is already done). A Budget of 1 keeps this test to two billed Turns.
- `Nova` and `Jarvis` both **Online**, Haiku/low, in a three-member Room together (`You`, `Nova`, `Jarvis`).
- COST: two short billed Turns — one for `Nova` to call the tool, one for `Jarvis` to say a single word. `Nova`'s decline costs nothing, which is the point.

**Steps**

1. Wait for both tiles to read **Online** under the new Budget.
2. Type exactly: `@Nova use mcp__team__follow_room to follow this room, using the room id from your own label.` and wait for the confirmation reply.
3. The Room will pause immediately — `Nova`'s own confirmation is one agent Message and the Budget is 1. That is expected. Do NOT click **Continue**.
4. Type exactly: `@Jarvis, without calling any tool, reply with the single word "go".` and press Enter. This Human Message resets the Budget to 0 of 1. Because Nova now follows the room, this one Message wakes BOTH Jarvis (mentioned) and Nova (following), and exactly one of their replies can land.
5. Wait for the first agent reply to land; the Budget is then spent at 1 of 1. If it is Jarvis's `go`, Nova's half of the test is live. If Nova's reply landed first, Jarvis is the one declined and Nova's half is INCONCLUSIVE: repeat step 4 (each Human Message resets the Budget), up to three times.
6. Watch the Room for sixty seconds. Type nothing once the Budget is spent — a Human Message would reset the counter and destroy the condition.
7. In `T-A`, search for `declined a turn`.
8. Click **Teammates** and read both tiles, then come back.

**Pass if — all of these**

- Exactly one agent Message lands after step 4 (Jarvis's `go`), `Nova` does not reply to it, and no further agent Message appears in the Room.
- `T-A` contains `Persona 'Nova' declined a turn in room <roomId>: the room has spent its budget of 1 agent messages.` naming **Nova specifically**. This is the oracle. `Nova` was following, was delivered the Message, and was stopped by the Budget rather than by the Mention rule — APPTOOLS-23 is what rules the Mention rule out.
- The amber pause panel reads `Agents have sent 1 replies since you last spoke, and are paused.`
- Neither tile has gone **Degraded**. A spent Budget is not a fault.

**Fail if — any of these**

- A SECOND agent Message lands after step 4 (for example `Nova` replying to `Jarvis`'s `go`) -> **following is buying Turns past the cap.** This is the most serious failure available in this area: two mutual followers could then run unattended with nothing to stop them, which is the exact hazard the Budget exists to prevent and the reason `rules.md` makes the Budget-before-everything ordering binding. Report it against `ReplyGate.Decide`'s parameter ordering.
- `T-A` shows the `declined a turn` line for `Jarvis` (Nova's reply landed first) and never for `Nova` -> that run is INCONCLUSIVE, not a failure; repeat step 4. If Jarvis's `go` landed and there is still no line for `Nova`, `Nova` was not following after all; re-check step 2's confirmation before filing anything.
- Either tile flips to **Degraded** -> a spent Budget is being reported as a fault, which it is not.

**Inconclusive if**

If `Nova`'s tile shows **Starting** or the Persona restarted at any point between steps 2 and 5, the follow was cleared — it is held in memory and dropped whenever that Agent's runner restarts — and `Nova`'s silence proves nothing. Check `T-A` for Persona start lines, then repeat from step 2. If `Jarvis` calls a tool instead of answering in one word, the Budget may be spent by something other than the reply under test; repeat with a plainer instruction. Unset `Team__AgentMessageBudget` before any later test.

### APPTOOLS-26 — create_room with seed posts the opening Message as part of creation

**💰 Spends money** · about 10 min

*`seed` closes the gap `invite-rooms.md` used to document as permanent: the new Room is never contextless, because the agents named in it learn why they are there in the same turn they are added.*

**Before you start**

- `Nova` and `Jarvis` both **Online**, Haiku/low.
- COST: one turn on Haiku at effort low.

**Steps**

1. Open the room named `Nova` (Nova's own direct room) and write down the sidebar room list.
2. Click into the composer and type exactly: `Use mcp__team__create_room to start a room with Jarvis, passing seed "Let's plan the launch." as the opening message. Then tell me the id it returned.` and press Enter.
3. Wait for the turn to finish.
4. Confirm exactly one new entry appears in the sidebar, named `Nova, Jarvis`.
5. Confirm Nova's reply quotes a result of the form `Created room '<name>' (id <id>) and posted the seed message into it.`
6. Click the new sidebar entry and confirm its `/rooms/<id>` URL matches the id Nova reported.
7. Confirm the new Room's Transcript already contains a message row whose sender is `Nova` and whose body is exactly `Let's plan the launch.` — present the moment the Room first opens, with no further messages needed to produce it.
8. Open `<data>\rooms\<newRoomId>.jsonl` and confirm its FIRST line is that seed message, sender `Nova`. There is no Human line in this new room; the seed is its opening line.

**Pass if — all of these**

- The new Room's Transcript shows the seed text as its first message, sender `Nova` — present as part of creation, not requiring a follow-up post.
- Nova's reply confirms the seed was posted, using the `...and posted the seed message into it.` wording.
- `.jsonl`'s first line is the seed message.

**Fail if — any of these**

- The new Room opens with NO messages despite `seed` having been passed -> unlike a Room created WITHOUT `seed` (which still legitimately arrives empty by design, see `invite-rooms.md`), this now IS a `create_room` defect: the seed post silently failed. Check Nova's exact reply text — `Created room '<name>' (id <id>), but could not post the seed message: <reason>` names the failure if the tool itself reported it; if Nova instead claims plain success with no such caveat while the Room is empty, the failure is happening below the tool's own error handling.
- The seed message's sender is anyone other than `Nova` -> the post is being attributed to the wrong Agent.
- The seed text is paraphrased or altered from what you asked Nova to pass -> the model did not pass your literal text as `seed`; note the exact string it used instead.

**Inconclusive if**

If Nova's reply reports `Created room '<name>' (id <id>), but could not post the seed message: <reason>`, that is the documented failure above, not a gap in this test — record it as a real `create_room` defect. If Nova omits `seed` entirely and creates the Room without an opening Message, that is not this test's failure mode either — that Room legitimately arrives empty by design; rerun with a prompt that names the `seed` argument more explicitly.

### APPTOOLS-27 — get_help carries a TASKS section while the Task tools are offered

**💰 Spends money** · about 6 min

*The progressive-discovery design promises that `get_help` names the tools the system prompt does not. The TASKS section is how a model learns that Tasks exist at all; it is added between BUDGET and the TOOLS section only when `create_task` is in the catalog, so it can vanish without any test noticing.*

**Before you start**

- `Nova` **Online**, Haiku/low, in a `P-LAUNCH-PAID` launch at Trace (`${env:Logging__LogLevel__Agency.Huddle} = "Trace"`), with `Team:Tasks:Enabled` at its default (true).
- COST: one short turn on Haiku at effort low.

**Steps**

1. In the room named `Nova`, type exactly `Call mcp__team__get_help and tell me which sections it has.` and press Enter. Wait for the reply.
2. Run `O-TOOLCALLS` for `Nova` and confirm a new line whose `tool` field contains `get_help`.
3. In the console, find the newest `App tool server replied to tools/call:` line and decode its payload (newlines appear as `\n`, apostrophes as `'`).
4. Confirm the decoded text contains a `TASKS` section beginning `TASKS\nTasks track work outside this conversation.` (`PromptCatalog.cs:1216-1222`), placed after the BUDGET section and before the TOOLS section (`GetHelpTool.cs:127-130`).

**Pass if — all of these**

- `O-TOOLCALLS` has a `get_help` line for this turn.
- The decoded payload carries the TASKS section between BUDGET and TOOLS.

**Fail if — any of these**

- The TASKS section is absent while Tasks are enabled -> the model is never told that Tasks exist, however many tools are offered.
- The TASKS section sits outside the order BUDGET, TASKS, TOOLS.

**Inconclusive if**

`Team:Tasks:Enabled` is false in this install: then the TASKS section must be ABSENT, and its absence is the pass for that configuration. If `O-TOOLCALLS` shows no `get_help` line for the turn, Nova answered from the system prompt; ask again naming the tool. Without Trace there is no payload to decode.

### APPTOOLS-28 — A prefix-less Adapter is told tool names bare (ADR-0014)

**Free** · about 6 min

*An Adapter whose profile sets `UsesToolNamePrefix=false` must never be told `mcp__team__` names, because it exposes tools bare and a prefixed name would send the model calling something that does not exist. The prefixed side for Claude is pinned by `ToolPrefixTests.cs:43-63`; only a real launch shows the bare side end to end.*

**Before you start**

- `P-LAUNCH-UAT`: the mock adapter is launched with `UsesToolNamePrefix=false` (`DotAcpAgentHostFactory.cs:234`). Ada is the Teammate; her room is the one named `Ada`.
- Free: the mock adapter bills nothing.

**Steps**

1. Open the room named `Ada` and send `hello`.
2. Read Ada's reply. The mock adapter replies with the whole prompt it received (`common.md`, Never set `TraceWire`).
3. In that echoed prompt, find where the system prompt names the help tool. Confirm it names it bare, as `get_help`.
4. Search the whole echoed prompt for `mcp__team__`. Confirm there is no occurrence.

**Pass if — all of these**

- The echoed prompt names the help tool as `get_help`.
- The echoed prompt contains no `mcp__team__` anywhere.

**Fail if — any of these**

- `mcp__team__` appears in the echoed prompt -> a prefix-less Adapter is being told prefixed names (ADR-0014 regression).

**Inconclusive if**

The echo does not carry the appended system prompt, only the Message text. That is not verified for the mock adapter: if the reply shows no system-prompt text at all, record the check as unobservable manually (the automated prefix tests remain the evidence) rather than passing it.

---

Back to [the manual test script](../manual-tests.md).
