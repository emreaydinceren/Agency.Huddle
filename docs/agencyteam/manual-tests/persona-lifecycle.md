# Persona lifecycle: supervisor, work dirs, health and restarts

Prove, in a real browser against a real `node` adapter, everything between "a Persona file exists on disk" and "an Agent is answering in a Room": that adding a Persona brings a teammate Online and mints a Room named after it, that the per-Persona Work Dir is keyed on the frontmatter Name, that the Offline / Starting / Online / Degraded badge tells the truth on every surface at once (tile, card, Room strip), that a failure carries a reason a human can act on, and that a Persona edit, a Model change, an Effort change and the Restart button each stop and restart exactly one session and no more. Almost none of this is reachable from the automated suite — the tests run through FakeAgentHostFactory — so every real-adapter failure (missing, unauthenticated, killed mid-session, a stale stored Model, a spent token Budget) and every timing-visible transition exists only here. The tests are ordered cheapest-first: PERSONALIFECYCLE-01 to -05 run with the adapter disabled, -06 to -25 start real adapter processes (free: starting a session spends nothing), and only -26 to -32 deliver Messages that cost money.

**32 tests** · 25 free, 7 paid 💰 · about 6.5 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB` / `O-WIRE`, the four resets,
`P-NEW-PERSONA`, `P-ECHO-BOT` and the standing conventions. This area adds:

1. Confirm the Adapter is installed: `Test-Path 'E:\Repos\Huddle\tools\acp\node_modules\@agentclientprotocol\claude-agent-acp\dist\index.js'` must print `True`. If `False`, run `pwsh E:\Repos\Huddle\tools\acp\install.ps1` and check again. Confirm `node --version` prints a version. Without both, every test from PERSONALIFECYCLE-06 onward is inconclusive.
2. Meet the Claude login requirement in [§0.1](../manual-tests.md#01-what-you-need) — the Adapter authenticates against that login, so nothing from PERSONALIFECYCLE-06 onward starts without it. If start failures later read "The Adapter needs authentication: …", stop and log in; do not report those as defects.
3. Two lanes. WITHOUT Adapters is `P-LAUNCH-FREE`. WITH Adapters is a plain `dotnet run --project src\Huddle.App --urls http://localhost:5100`, since the Development profile already sets `Team:Acp:Enabled=true`. The overrides this area uses are `Team__Acp__Enabled`, `Team__Acp__TokenBudget`, `Team__AgentMessageBudget`, `Team__Acp__AdapterPath` and `Team__Acp__WorkDir`.
4. In `T-B`, wrap `O-ADAPTERS-LIST` in a function so the tests below can just say `Adapters`: `function Adapters { Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like '*claude-agent-acp*' } | Select-Object ProcessId, CreationDate, CommandLine | Format-Table -Wrap }`. `Stop-Process -Id <pid>` on a row simulates a crashed Adapter.
5. `O-DB` is optional here and recorded as not-checked when absent — except PERSONALIFECYCLE-17, which cannot run at all without it.
6. `work\<Persona>\` under `App_Data` is each Persona's Work Dir, keyed on the frontmatter Name. Several tests read it directly.
7. Use the browser you will test in, with two tabs available. Never test through a private window for the multi-tab test.
8. MONEY. Starting a Persona's session is free — no prompt turn, no tokens. Only a Message that produces a reply costs. PERSONALIFECYCLE-01 to -25 are free; -26 to -32 are marked and each says how many Turns it costs.

## Tests

### PERSONALIFECYCLE-01 — With ACP disabled every teammate reads Offline with an empty tooltip and no Room raises an alert

**Free** · about 10 min

*Proves the stock configuration is silent: a Persona that has never connected is Offline with no reason, gets no Room, spawns no process, and puts no permanent red alert strip in any Room.*

**Before you start**

- The solution builds.
- No app is running on port 5100.
- `App_Data\Teams\` is empty, or you accept the Personas already in it.

**Steps**

1. In `T-A` run `$env:Team__Acp__Enabled='false'; dotnet run --project src\Huddle.App --urls http://localhost:5100` and wait for the line `Now listening on: http://localhost:5100`.
2. In `T-B` run `Adapters`. Note the row count (expected: none).
3. In the browser open `http://localhost:5100/teammates`.
4. Click **New teammate**.
5. In the **Name** field type `Nova`.
6. In the **Title** field type `Test teammate`.
7. In the **Alias** field type `nova`.
8. Leave **Teams** empty.
9. In the **Persona body** textarea type `You are Nova. Answer in one short sentence.`
10. In the **Model** select choose the entry whose label contains `Haiku`.
11. In the **Effort** select choose `low`.
12. Click **Add teammate**.
13. Read the card that appears, then click the **×** in its header to close it.
14. Hover the mouse over the status line under `Nova` in the tile list and hold it for three seconds.
15. Look at the sidebar Room list.
16. Click the Room named `echo` in the sidebar.
17. Click into the composer (placeholder `Message… (/invite @agent)`), type `hello there @echo` and press Enter.
18. In `T-B` run `Adapters` again.
19. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\work'`.

**Pass if — all of these**

- After **Add teammate**, the card header changes to `Teammate` and shows `Nova` with the status word **Offline**.
- The tile list contains a tile `Nova` whose status line reads **Offline**.
- Hovering that status line shows NO tooltip text at all (an empty tooltip, not a tooltip saying "Offline").
- The card for `Nova` shows no reason line under the status, and its action row has **Edit**, **Open**, **Restart** and **Remove** but NO **Message** link.
- The sidebar Room list contains `echo` and `alpha` and does NOT contain a Room named `Nova`.
- The `echo` Room shows NO red alert strip anywhere above the composer.
- Typing `hello there @echo` in `echo` produces a reply beginning `**echo:**` followed by the quoted text.
- `Adapters` lists no more `node.exe` rows than it did at step 2 (the picker probe processes from steps 10-11 are short-lived and may already be gone).
- `App_Data\work` contains no folder named `Nova` (a bare `work` folder with nothing in it is expected — the Model picker creates it).

**Fail if — any of these**

- A red alert strip appears in the `echo` or `alpha` Room listing not-running agents -> the Room health strip is rendering on unhealthy STATE rather than on a REASON, which puts a permanent alert in every Room of a stock install and trains users to ignore it.
- The `Nova` tile reads **Starting** -> "Starting" is being rendered without a pipe connection; PersonaStatusResolver's connected-first rule has been reversed and a never-connected Persona now looks like one that is launching.
- A Room named `Nova` appears in the sidebar -> a Room is being minted for a Persona that never connected, so the sidebar will fill with Rooms nobody can talk in.
- A **Message** link appears on `Nova`'s card -> the card is offering a Room that does not exist; clicking it will 404 or land on an empty page.
- Long-lived `node.exe` rows appear in `Adapters` -> an adapter was started with `Team:Acp:Enabled=false`, which spends the operator's money in a configuration they switched off.
- The status line tooltip reads a reason such as an exception type -> a diagnostic is leaking into a state that is not a failure.

**Inconclusive if**

If the Model select shows only `Use the agent's default` with the hint "This agent advertises no models, so it will use its own default.", the adapter could not be probed — record steps 10-11 as not-performed, continue the test, and run PERSONALIFECYCLE-04 to find out why. If `App_Data\Teams\` already contained Personas from earlier work, their tiles are expected too; judge only the `Nova` tile. If the browser shows `An unhandled error has occurred.` at any point, reload once and note it; if it recurs, the test is a FAIL on that step, not inconclusive.

> [!NOTE]
> Leave the app running — PERSONALIFECYCLE-02 through -05 reuse this same ACP-disabled run and the `Nova` Persona it creates.

### PERSONALIFECYCLE-02 — A Persona Name with a leading, trailing or doubled space is refused on the card and never written to disk

**Free** · about 8 min

*Proves name validation happens before any file is written, so two teammates a reader cannot tell apart can never exist.*

**Before you start**

- The ACP-disabled app from PERSONALIFECYCLE-01 is still running.
- You can list `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams`.

**Steps**

1. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams' -Recurse -Filter *.md | Select-Object Name` and write down the list.
2. In the browser go to `http://localhost:5100/teammates` and click **New teammate**.
3. In **Name** type ` Nova Two` (one leading space before the N).
4. In **Title** type `Test teammate`. In **Alias** type `novatwo`. In the **Persona body** textarea type `You are Nova Two.`
5. Click **Add teammate**.
6. Read the whole card, top to bottom, for an error message.
7. Clear the **Name** field and retype it as `Nova Two ` (one trailing space after the o).
8. Click **Add teammate** and read the card again.
9. Clear the **Name** field and retype it as `Nova  Two` (two spaces between the words).
10. Click **Add teammate** and read the card again.
11. Clear the **Name** field and retype it as `Nova Two` (exactly one interior space).
12. Click **Add teammate**.
13. In `T-B` re-run the `Get-ChildItem` command from step 1.

**Pass if — all of these**

- Each of the three bad Names keeps the card open and shows a red message on the card reading `' Nova Two' is not a valid Persona name.` (and likewise for the trailing-space and doubled-space forms).
- The typed Title, Alias and Persona body are still in their fields after each refusal — nothing the tester typed is thrown away.
- The single-interior-space Name `Nova Two` is ACCEPTED: the card switches to the `Teammate` view showing `Nova Two`, and a tile `Nova Two` appears in the list.
- After step 13 the Teams directory has gained exactly ONE new file, `Nova Two.md`, and no file whose name contains a leading, trailing or doubled space.

**Fail if — any of these**

- Any of the three bad Names is accepted -> the filename guard is gone; Windows strips a trailing space, so `Nova Two ` and `Nova Two` would resolve to one file while presenting as two teammates, and Mention resolution becomes ambiguous.
- A refusal writes a `.md` file anyway (step 13 shows extra files) -> validation runs after the write, leaving a file on disk that the UI claims was rejected.
- `Nova Two` with one interior space is refused -> the rule has been over-tightened; a display Name like `Chief of Staff` is legitimate and this would make PERSONALIFECYCLE-08 impossible.
- The refusal appears as a browser alert box or an `An unhandled error has occurred.` banner instead of an inline message on the card -> the error path is throwing rather than reporting.

**Inconclusive if**

If the browser or an input method silently trims the spaces you type (check by selecting the field text and looking at the selection), you cannot tell a refusal from a trim: switch to a different browser, or paste the value from a text editor, and re-run. If a file named `Nova Two.md` already existed before step 1, the error will instead read `Persona 'Nova Two' already exists.` — delete that file, restart the app and re-run.

> [!NOTE]
> Windows reserved device names (CON, NUL, COM1) pass this validation and then fail to become a real file, so such a teammate simply never appears. That is a pre-existing unguarded limit, recorded in the docs — do not test for it and do not file it.

### PERSONALIFECYCLE-03 — Browsing /teammates spawns no adapter; opening a card spawns exactly one, and reopening spawns none

**Free** · about 12 min

*Proves the Model/Effort probe runs on card-open only and is cached for the life of the app run, so merely looking at the page never costs a process.*

**Before you start**

- The ACP-disabled app from PERSONALIFECYCLE-01 is still running (the probe is deliberately NOT gated on `Team:Acp:Enabled`, so this test is free of adapters that stay alive).
- The adapter is installed (setup step 1).
- At least one Persona exists (`Nova` from PERSONALIFECYCLE-01).

**Steps**

1. In `T-B` run `Adapters` and write down the exact rows.
2. In the browser navigate to `http://localhost:5100/teammates`.
3. In `T-B` run `Adapters` immediately.
4. In the browser press F5 to reload `/teammates`, twice.
5. In `T-B` run `Adapters` immediately.
6. In the browser change the **Team** filter select from `All teams` to another option and back (skip this step if the select is not shown because no Persona has a Team).
7. In `T-B` run `Adapters` immediately.
8. In the browser click **New teammate**.
9. Within one second, in `T-B`, run `Adapters` repeatedly (about once a second for 25 seconds) and record the highest number of rows you see and when they disappear.
10. In the browser read the grey hint text under the **Model** select and under the **Effort** select while the card is loading, then again once it settles.
11. Click **Cancel** to close the card.
12. In the browser click **New teammate** again.
13. In `T-B` run `Adapters` repeatedly for 15 seconds and record the highest row count.

**Pass if — all of these**

- Steps 3, 5 and 7 show the SAME rows as step 1 — navigating, reloading and filtering spawn no `node.exe` at all.
- Step 9 shows AT MOST ONE extra `node.exe` row at any moment, and it disappears within 25 seconds.
- While the card is loading, the Model hint reads `Reading the models this agent offers…` and the Effort hint reads `Reading the effort levels this model offers…`; both are replaced within 20 seconds.
- Once settled, the Model select lists real model entries (one label contains `Haiku`) and the Effort select lists real levels (one is `low`).
- Step 13 shows NO new `node.exe` row — the second card-open is served from cache.

**Fail if — any of these**

- A `node.exe` appears on page load or reload (steps 3/5) -> the probe has moved to the page-load path; every visit to /teammates now costs a process, which was explicitly designed against.
- Two or more extra `node.exe` rows appear on a single card-open -> the Effort ladder is spawning its own session instead of reusing the model probe's; the process cost per card-open has doubled.
- A hint stays on `Reading the models this agent offers…` for more than 20 seconds -> the probe is hanging past its own timeout and the card will never become usable.
- Step 13 spawns a fresh process -> the successful catalog is not being cached, so every card-open pays for a probe.

**Inconclusive if**

If another program on the machine runs `node.exe` (a dev server, an editor extension), you cannot attribute rows: close it, or read the `CommandLine` column and count only rows containing `claude-agent-acp`. If the pickers come back empty with the adapter installed, this test cannot judge the spawn counts — record it inconclusive and run PERSONALIFECYCLE-04, then treat an empty picker against a REAL installed adapter as a separate suspect (the `initialize` handshake, not the adapter).

> [!NOTE]
> The probe's working directory is `App_Data\work\`, never the repository root, so the throwaway adapter cannot pick up this repository's own CLAUDE.md. `App_Data\work\` itself being created by this probe is expected and proves nothing about any Persona.

### PERSONALIFECYCLE-04 — A missing Adapter degrades the Model and Effort pickers with a plain-language hint, and restoring it works with no app restart

**Free** · about 12 min

*Proves the page survives a missing adapter, says so in words, still saves, and never caches the failure.*

**Before you start**

- The ACP-disabled app from PERSONALIFECYCLE-01 is still running.
- You can rename folders under `E:\Repos\Huddle\tools\acp\`.

**Steps**

1. In `T-B` run `Rename-Item 'E:\Repos\Huddle\tools\acp\node_modules' 'node_modules.off'`. If it fails with a file-in-use error, close any editor indexing that folder and retry.
2. In the browser reload `http://localhost:5100/teammates` with F5.
3. Read the whole page: the heading, the tile list, the **Team** filter and the `Files that didn't load` block if present.
4. Click **New teammate**.
5. Read the grey hint under the **Model** select and the grey hint under the **Effort** select, word for word.
6. Open the **Model** select and count its options.
7. In **Name** type `Orphan`, in **Title** type `No adapter`, in **Alias** type `orphan`, in the **Persona body** textarea type `You are Orphan.`
8. Click **Add teammate**.
9. Read the card that results, in particular its **Model** and **Effort** sections.
10. In `T-B` check the console in `T-A` for a line containing `No ACP adapter is installed; the model catalog is empty.`
11. In `T-B` run `Adapters` and confirm no probe process was spawned during steps 4-8.
12. In `T-B` run `Rename-Item 'E:\Repos\Huddle\tools\acp\node_modules.off' 'node_modules'`.
13. In the browser click the `Orphan` tile, then click **Edit**.
14. Read the **Model** select and its hint again.

**Pass if — all of these**

- With the adapter aside, `/teammates` renders completely: heading `Teammates`, the **New teammate** button, and every existing tile — no error page and no blank region.
- The Model select contains exactly one option, `Use the agent's default`, and its hint reads exactly `This agent advertises no models, so it will use its own default.`
- The Effort select contains exactly one option, `Use the agent's default`, and its hint reads exactly `This model offers no effort choice, so it will think as it normally does.`
- Saving still works: the card switches to `Teammate` showing `Orphan`, its **Model** section reads `Agent default` and its **Effort** section reads `Model default`.
- `T-A`'s console carries `No ACP adapter is installed; the model catalog is empty.` at Information level.
- No `node.exe` was spawned while the folder was renamed aside.
- After restoring the folder and reopening the card in step 13 — with NO app restart — the Model select lists real models again, including one labelled with `Haiku`.

**Fail if — any of these**

- The page shows `An unhandled error has occurred.` or a region that never finishes loading -> a missing adapter is crashing the page instead of degrading it.
- A hint stays on `Reading the models this agent offers…` indefinitely -> the 20-second probe timeout is not firing and the card hangs.
- After restoring the folder, the picker is still empty (step 14) -> a FAILED probe was cached, which means a user who installs the adapter must restart the whole app to get a model list.
- Saving is blocked or errors while the adapter is missing -> the picker's failure is being treated as a validation failure, so no Persona can be created without an adapter.
- The picker is empty even though the adapter IS installed and `node` runs -> this is a different fault: suspect the `initialize` handshake (the app does not advertise `clientCapabilities.session.configOptions`), not the adapter. Report it as such.

**Inconclusive if**

If the rename in step 1 fails because a file is locked, the test cannot start — close the locking process and retry rather than guessing. If step 12's restore fails, STOP and fix it before any later test: every test from PERSONALIFECYCLE-06 onward assumes the adapter is present at its real path.

> [!NOTE]
> In the picker a failed probe, a cancelled probe, a missing adapter and an unauthenticated adapter all read identically. That ambiguity is a recorded limit — only a Persona that FAILED TO START distinguishes them (PERSONALIFECYCLE-09 and -25).

### PERSONALIFECYCLE-05 — Restart with Team:Acp:Enabled=false — record whether it starts a real adapter anyway

**Free** · about 8 min

*Establishes, as a measurement rather than a guess, whether the Restart button is the one path that ignores the money flag the operator switched off.*

**Before you start**

- The ACP-disabled app from PERSONALIFECYCLE-01 is still running and the adapter folder has been restored (PERSONALIFECYCLE-04 step 12).
- At least one Persona exists and reads Offline.

**Steps**

1. In `T-B` run `Adapters` and write down the rows.
2. In the browser go to `http://localhost:5100/teammates` and click the `Nova` tile.
3. Confirm the card's status line reads **Offline** and that a **Restart** button is present in the action row.
4. Click **Restart** ONCE. Do not click it again.
5. Watch the button's own label for the next five seconds and write down every label you see.
6. Watch the card's status line for 30 seconds and write down every status word it shows, in order.
7. In `T-B` run `Adapters` every two seconds for 30 seconds and write down the highest row count and whether any new row persists.
8. Read `T-A`'s console for new lines mentioning `Nova`.
9. Whatever happened, write it down verbatim — this test reports behaviour, it does not assume one.
10. If an adapter did start, click the `Nova` tile again and check whether a **Message** link is now on the card and whether a Room named `Nova` has appeared in the sidebar.
11. Stop the app in `T-A` with Ctrl+C.
12. In `T-B` run `Adapters` one final time.

**Pass if — all of these**

- Exactly one of these two outcomes is observed, recorded verbatim, and reported as the build's behaviour: (A) clicking Restart starts a real adapter — the button reads `Restarting…` while disabled, a new `node.exe` row appears and persists, and the status goes Starting then Online; or (B) the Restart is a no-op that reports something to the human on the card and no `node.exe` appears.
- In BOTH outcomes: clicking Restart once produces AT MOST ONE new persistent `node.exe` row.
- In BOTH outcomes: no `An unhandled error has occurred.` banner, and the button ends the test re-enabled (or gone, if the teammate came Online).
- After Ctrl+C in step 11, `Adapters` shows no rows containing `claude-agent-acp` — nothing was orphaned.

**Fail if — any of these**

- Two or more persistent `node.exe` rows appear from one click -> a duplicate start slipped past the supervisor's starting/stopping guard and two adapter processes now share one agent id.
- The button stays stuck on `Restarting…` and never re-enables -> the busy flag is never cleared and the only recovery is a page reload.
- The card shows `An unhandled error has occurred.` -> the restart path is throwing into the circuit instead of reporting on the card.
- An adapter survives Ctrl+C in step 12 -> shutdown is orphaning processes; those keep a session open and can keep spending.
- The tile reads **Online** while `Adapters` shows no process for it -> a badge that lies, the exact failure PersonaStatusResolver exists to prevent.

**Inconclusive if**

If the tile was already Online (because a previous test left ACP enabled in this window), the Restart button will not be offered — check `T-A`'s startup output for `Team:Acp:Enabled` and re-run in a fresh window with `$env:Team__Acp__Enabled='false'`. If outcome (A) occurs, note in the report that the teammate is now LIVE in a configuration the operator switched off, so the next Message typed in its Room would spend real money — this is the finding, not a bug to fix on the spot.

> [!NOTE]
> Either outcome is defensible; the tester's job is to state which one this build does. `Team:Acp:Enabled` is consulted only where the supervisor starts everything at app startup, so the manual restart path plausibly never sees it.

### PERSONALIFECYCLE-06 — A Persona added while the app is running comes Online and gets a Room named after it, with no page refresh

**Free** · about 12 min

*Proves the whole happy path end to end: file composed, agent registered, Room minted under the frontmatter Name, badge walking Offline then Starting then Online, exactly one adapter process.*

**Before you start**

- No app is running.
- The adapter is installed and the machine is logged in to Claude.
- You have a fresh PowerShell `T-A` with no `Team__` environment variables set (open a new one to be sure).

**Steps**

1. In `T-A` run `dotnet run --project src\Huddle.App --urls http://localhost:5100` and wait for `Now listening on: http://localhost:5100`.
2. In `T-B` run `Adapters` and write down the row count.
3. In the browser open `http://localhost:5100/teammates`.
4. Click **New teammate**.
5. In **Name** type `Nova`. In **Title** type `Test teammate`. In **Alias** type `nova`. Leave **Teams** empty.
6. In the **Persona body** textarea type `You are Nova. Answer in one short sentence.`
7. In the **Model** select choose the entry whose label contains `Haiku`. In the **Effort** select choose `low`.
8. Click **Add teammate** and immediately start watching the status line on the card. Write down every status word it shows, in order, with rough timings.
9. Without reloading the page, watch the sidebar Room list for 30 seconds.
10. In `T-B` run `Adapters`.
11. In `T-B` run `Get-Content 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova.md'`.
12. In `T-A`, read the console for a line containing `Created direct room` and for any line containing `failed to start`.
13. OPTIONAL (needs sqlite3): in `T-B` run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "select id,name,kind from users; select id,name from rooms; select * from persona_models;"`

**Pass if — all of these**

- The card switches to the `Teammate` view showing `Nova` immediately after **Add teammate**.
- The status line shows **Offline** briefly, then **Starting**, then **Online** with a green dot, reaching Online within about 30 seconds.
- A tile `Nova` is in the list and reads **Online**.
- Within a few seconds and with NO page refresh, the sidebar gains a Room named exactly `Nova`.
- `Adapters` has gained exactly ONE persistent `node.exe` row whose CommandLine contains `claude-agent-acp`.
- `Nova.md` begins with `---`, then `name: 'Nova'`, `title: 'Test teammate'`, `alias: 'nova'`, then `---`, then the body text verbatim.
- `T-A`'s console contains `Created direct room` naming `Nova`, and contains NO `Persona 'Nova' failed to start.` warning.
- If sqlite3 was available: `users` has a row named `Nova`, `rooms` has a row named `Nova`, and `persona_models` holds a model ID string for `Nova` (an id such as `claude-haiku-…`, not a display label like `Haiku 4.5`).

**Fail if — any of these**

- The Room never appears in the sidebar even after the tile reads Online -> the rooms-changed event is not reaching the sidebar, or the Room is never being ensured; the teammate is Online but unreachable.
- The Room is named after the FILE rather than the frontmatter `name:` -> a path-derived key has crept back in; renaming a file would then rename a teammate.
- TWO tiles or TWO Rooms appear from one save -> the duplicate-start race fired and a second adapter process was spawned under one agent id.
- The status never shows **Starting**, jumping Offline to Online -> either the health report is not reaching the page, or Starting is being skipped; you lose the only visible signal that a slow adapter is still coming up.
- The tile stays **Starting** for more than about 30 seconds -> the adapter is hanging, most likely in `session/new`.
- The tile reads Offline with a reason -> read the reason: `No ACP adapter is installed…` means run PERSONALIFECYCLE-09 instead; `The Adapter needs authentication…` means log in and re-run.
- `persona_models` holds a display label rather than an id -> the picker stored the wrong field and every start will log `not in the agent's advertised model catalog`.

**Inconclusive if**

If the Model select is empty, you cannot honour the Haiku convention — record it, run PERSONALIFECYCLE-04 to find out why, and re-run this test afterwards. If sqlite3 is not installed, record the database checks as not-checked; the UI conditions alone still decide pass or fail. If the tile flips straight to Offline with an authentication reason, this test is inconclusive (not a FAIL) — fix the login first.

> [!NOTE]
> The Starting badge is only reachable once the pipe has connected, so seeing the word Starting on screen proves BOTH that the pipe registered and that health reported Starting. Keep this app run and the `Nova` Persona: tests -07 through -25 build on them.

### PERSONALIFECYCLE-07 — The Work Dir is named from the frontmatter Name and does not follow a renamed file

**Free** · about 10 min

*Catches a path-derived key creeping back in — the regression the rules doc calls out — by renaming the .md file on disk and proving nothing about the teammate changes.*

**Before you start**

- The app from PERSONALIFECYCLE-06 is running with `Nova` Online.

**Steps**

1. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\work' | Select-Object Name`.
2. Confirm there is a folder named exactly `Nova`.
3. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\work\Nova'` and note whether it is empty.
4. In `T-B` run `Rename-Item 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova.md' 'nova-renamed.md'`.
5. Wait 10 seconds and watch `T-A`'s console and the browser's `/teammates` page without reloading it.
6. Read the tile list.
7. Click the `Nova` tile and read the **Persona file** section at the bottom of the card.
8. Close the card and look at the sidebar Room list.
9. In `T-B` re-run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\work' | Select-Object Name`.
10. In `T-B` run `Get-Content 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\nova-renamed.md' | Select-Object -First 3` and confirm `name: 'Nova'` is unchanged.
11. In `T-B` run `Rename-Item 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\nova-renamed.md' 'Nova.md'` to restore.

**Pass if — all of these**

- Before the rename, `App_Data\work` contains a folder named exactly `Nova`, and it is empty.
- After renaming the file, the tile is still named `Nova` — the Name comes from the frontmatter, not the filename.
- The card's **Persona file** section shows the NEW path ending in `nova-renamed.md`.
- The Room named `Nova` is still in the sidebar.
- `App_Data\work` still has exactly one folder for this teammate and it is still named `Nova` — no folder named `nova-renamed` appears, before or after the restart the rename triggers.

**Fail if — any of these**

- A work folder named `nova-renamed` appears -> the Work Dir key is derived from the file path; renaming a file would silently strand everything the previous session wrote.
- The tile is renamed to `nova-renamed` -> identity has moved from the frontmatter to the filename, contradicting the Teams-are-a-field decision.
- The tile disappears entirely and does not come back -> the recursive watcher missed the rename, and this Persona is now invisible with no error anywhere (a silent failure).
- No work folder exists at all for `Nova` -> the host factory never ran; check whether the teammate is really Online.
- The work folder is created at the repository root or beside the built binary rather than under `App_Data` -> the Work Dir is escaping DataDir.

**Inconclusive if**

If the rename in step 4 is blocked because an editor holds the file open, close it and retry — do not conclude anything from a failed rename. If the app was started from a different working directory, `App_Data` may not be under `src\Huddle.App`; find it with `Get-ChildItem -Path E:\Repos\Huddle -Recurse -Filter team.db -Depth 4` and use that path throughout.

> [!NOTE]
> `App_Data\work\` itself (with no Persona subfolder) is created by the Model/Effort picker probe, so the parent folder existing proves nothing on its own — judge only the named subfolder. The Work Dir is not a jail: agent-side Bash and Write reach the real disk, which is a recorded limit, not a defect.

### PERSONALIFECYCLE-08 — A Name containing spaces works for the monogram, the Room and the Work Dir

**Free** · about 8 min

*Proves nothing in the pipeline truncates a display Name at its first space.*

**Before you start**

- The app from PERSONALIFECYCLE-06 is running with adapters enabled.

**Steps**

1. In the browser go to `http://localhost:5100/teammates` and click **New teammate**.
2. In **Name** type `Chief of Staff`. In **Title** type `Keeps the team honest`. In **Alias** type `cos`. Leave **Teams** empty.
3. In the **Persona body** textarea type `You are the Chief of Staff. Answer in one short sentence.`
4. In the **Model** select choose the entry labelled with `Haiku`. In the **Effort** select choose `low`.
5. Click **Add teammate**.
6. Read the round avatar badge (the monogram) on the card and on the new tile.
7. Wait until the tile reads **Online**, then look at the sidebar Room list.
8. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\work' | Select-Object Name`.
9. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams' | Select-Object Name`.
10. OPTIONAL (sqlite3): run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "select name from rooms;"`

**Pass if — all of these**

- The monogram reads `CS` — the first letter of the first word and of the LAST word, not the first two words.
- A tile reads `Chief of Staff` in full and reaches **Online**.
- The sidebar gains a Room named exactly `Chief of Staff`, spaces intact.
- `App_Data\work` gains a folder named exactly `Chief of Staff`.
- `App_Data\Teams` gains a file named exactly `Chief of Staff.md`.
- If sqlite3 was available, the `rooms` table holds the full name `Chief of Staff`.

**Fail if — any of these**

- The Room, the folder or the file is named `Chief` -> something in the chain truncates at the first space; that teammate's work would land in the wrong place and its Room would be unfindable.
- The monogram reads `CO` -> the monogram takes the first two words, giving the connective `of` a letter it does not deserve (cosmetic, but report it).
- The Name is refused as invalid -> single interior spaces have been over-restricted, which contradicts PERSONALIFECYCLE-02.
- The tile appears but never reaches Online while `Nova` is fine -> suspect the spaces in the Work Dir path being passed unquoted to the adapter process.

**Inconclusive if**

If the teammate reaches only **Starting** and stalls, wait a full 60 seconds before judging — a second adapter starting while others are running is slower. If it is still Starting, check `T-A` for a start failure and treat an authentication or missing-adapter reason as inconclusive for THIS test.

> [!NOTE]
> Whether a real model can write `@Chief of Staff` out in full to reach it in a group Room is a separate, money-spending concern and is deliberately not tested here.

### PERSONALIFECYCLE-09 — A missing Adapter reports the same actionable reason on the tile, the card and the Room strip, and harms no other teammate

**Free** · about 15 min

*Proves a start failure is visible in words on all three surfaces at once, that the Work Dir is still created, and that one failure is contained.*

**Before you start**

- The app from PERSONALIFECYCLE-06 is running with `Nova` and `Chief of Staff` Online.
- You can rename folders under `E:\Repos\Huddle\tools\acp\`.

**Steps**

1. In `T-B` run `Rename-Item 'E:\Repos\Huddle\tools\acp\node_modules' 'node_modules.off'`.
2. In the browser go to `http://localhost:5100/teammates` and click the `Nova` tile, then click **Restart**.
3. Wait 15 seconds, then read the card's status line and the line directly under it.
4. Close the card and hover the mouse over the `Nova` tile's status line for three seconds; read the tooltip.
5. Read the `Chief of Staff` tile's status line.
6. In the sidebar click the Room named `Nova` and read the area directly above the composer.
7. In the sidebar click the Room named `Chief of Staff` and read the area directly above the composer.
8. In `T-A` read the console for a warning containing `Persona 'Nova' failed to start.`
9. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\work' | Select-Object Name`.
10. In `T-B` run `Adapters` and count the rows containing `claude-agent-acp`.
11. OPTIONAL (sqlite3): run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "select name from users;"` and confirm `Nova` is still listed.
12. Leave the adapter folder renamed aside — PERSONALIFECYCLE-10 restores it.

**Pass if — all of these**

- The `Nova` card reads **Offline** and shows, as a full line under the status, exactly: `No ACP adapter is installed for Persona 'Nova'. Run tools/acp/install.ps1 (or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.`
- The `Nova` tile reads **Offline** and its tooltip carries that same reason text.
- The `Nova` Room shows a red alert strip above the composer reading `Nova is Offline: No ACP adapter is installed for Persona 'Nova'. …`.
- The `Chief of Staff` tile still reads **Online**, its Room shows NO alert strip, and its `node.exe` row is still in `Adapters`.
- `App_Data\work\Nova` still exists — the Work Dir is created before the adapter is located, so its presence proves the start was entered and the adapter lookup is what failed.
- `T-A`'s console carries a Warning `Persona 'Nova' failed to start.` naming one Persona only.
- If sqlite3 was available, `Nova` is still a row in `users` — registration happened; only the adapter launch did not.

**Fail if — any of these**

- Any of the three surfaces disagrees (for example the tile says Offline but the Room strip is absent, or the card shows a different state) -> the surfaces are deriving state independently instead of through one resolver, and users will get contradictory answers.
- The reason is blank, or names only an exception type such as `InvalidOperationException` -> the human is told something broke but not what to do about it.
- `Chief of Staff` also goes Offline -> one Persona's failure is taking down others; a shared failure path or an aborting start loop.
- An alert strip appears in a Room that `Nova` is not a member of -> the strip is listing agents by something other than membership.
- `An unhandled error has occurred.` appears -> a start failure is escaping into the circuit rather than being reported as health.
- `App_Data\work\Nova` is absent -> the directory is no longer created before the adapter lookup, so a tester can no longer tell "the adapter is missing" from "the Persona never started".

**Inconclusive if**

If the rename in step 1 fails (file in use), the test cannot start. If `Nova` was already Offline for a different reason before step 1, restart it first and confirm it reaches Online, or you cannot attribute the failure. If the app was started BEFORE the rename and `Nova`'s adapter is still running, the Restart in step 2 is what makes the failure happen — do not skip it.

> [!NOTE]
> `Nova` has a Room and an agent row at all only because the runner connects over the pipe and says hello BEFORE the adapter is ever launched. That ordering is what makes all three surfaces available to fail on.

### PERSONALIFECYCLE-10 — Restart on the Teammate card recovers a failed Persona with no app restart, and one click starts one adapter

**Free** · about 10 min

*Proves the human has a working recovery action for a Persona with no host running, and that it cannot be double-fired.*

**Before you start**

- PERSONALIFECYCLE-09 has been run: `Nova` is Offline with the missing-adapter reason and `tools\acp\node_modules` is renamed to `node_modules.off`.

**Steps**

1. In the browser open the `Nova` card and click **Restart** once, while the adapter is still missing.
2. Read the card for 10 seconds: the button label, the status line and the reason line.
3. In `T-B` run `Rename-Item 'E:\Repos\Huddle\tools\acp\node_modules.off' 'node_modules'`.
4. In `T-B` run `Adapters` and write down the rows.
5. In the browser, on the `Nova` card, click **Restart** ONCE. Do not click again.
6. Watch the button label for the first two seconds and write down what it reads.
7. Watch the status line for 40 seconds and write down every word it shows, in order.
8. Watch the reason line under the status.
9. In the sidebar click the Room named `Nova` and look above the composer.
10. In `T-B` run `Adapters` and compare with step 4.
11. In `T-A` read the console for `failed to start` warnings written after step 5.

**Pass if — all of these**

- Clicking Restart while the adapter is still missing leaves the card on **Offline** with the SAME missing-adapter reason, shows no error banner, and re-enables the button.
- On the successful attempt the button reads `Restarting…` and is disabled while the request is in flight.
- The status line goes **Starting** and then **Online** within about 40 seconds.
- The reason line disappears from the card once Online.
- The `Nova` Room's red alert strip is gone with NO navigation and NO page reload.
- `Adapters` has gained exactly ONE new `node.exe` row with a fresh CreationDate.
- No new `Persona 'Nova' failed to start.` warning appears after the successful click.

**Fail if — any of these**

- Clicking Restart does nothing at all for a Persona with no host running -> the recovery path is only removing a host and never starting one, so a Persona that failed at startup can only be fixed by restarting the whole app.
- Two new `node.exe` rows appear from one click -> the restart guard is not serialising, and two adapters now share one agent id.
- The button never leaves `Restarting…` -> the busy flag is never cleared and the card is stuck.
- The status stays **Offline** even though a fresh `node.exe` is running -> the health report is not reaching the page; the badge now lies about a working teammate.
- Clicking Restart with the adapter missing throws `An unhandled error has occurred.` -> the failure path is not being caught on the card.

**Inconclusive if**

If step 3's rename fails, stop and fix it — everything after depends on the adapter being back at its real path. If the status reaches Starting and stalls past 60 seconds, check `T-A` for an authentication failure and treat that as inconclusive, not a fail. If you accidentally double-click, restart the app and re-run; a double-click result cannot be attributed.

> [!NOTE]
> A Degraded or Offline teammate is never restarted automatically by design — the human clicking this button is the whole recovery policy.

### PERSONALIFECYCLE-11 — Restart is offered only for an unhealthy Teammate, and is accompanied by the memory-loss warning

**Free** · about 6 min

*Proves a healthy teammate cannot be restarted for nothing, and that a human is warned before throwing away conversation memory.*

**Before you start**

- The app is running with `Nova` Online (after PERSONALIFECYCLE-10) and at least one other teammate present.

**Steps**

1. In the browser go to `http://localhost:5100/teammates`.
2. Click the `Nova` tile, which should read **Online**.
3. Read every button in the card's action row, left to right, and write them down.
4. Look immediately under the action row for any hint line.
5. Close the card.
6. In `T-B` find `Nova`'s adapter with `Adapters` (match the CommandLine containing `claude-agent-acp` and the newest CreationDate, or cross-check by stopping the app and starting it with only `Nova` present if you cannot attribute the row).
7. In `T-B` run `Stop-Process -Id <pid>` for that adapter.
8. In the browser, without reloading, wait up to 10 seconds and click the `Nova` tile again.
9. Read every button in the action row again, and read the hint line under it, word for word.

**Pass if — all of these**

- For the Online teammate the action row is exactly: **Message**, **Edit**, **Open**, **Remove** — with NO **Restart** button and NO hint line under the row.
- After the adapter is killed, the same card shows **Message**, **Edit**, **Open**, **Restart**, **Remove**, with **Restart** between **Open** and **Remove**.
- With Restart present, the line under the action row reads exactly: `Like saving an edit, this restarts the teammate, which clears what it remembers.`

**Fail if — any of these**

- A **Restart** button is offered on a healthy teammate -> a human can now throw away a working session's memory for no reason, with no signal that there was nothing wrong.
- The hint line is missing when Restart is shown -> the human is not warned that recovery costs the teammate's memory.
- A **Message** link is shown for a teammate that has never connected -> the card is offering a Room that does not exist.
- The Restart button appears but the status still reads **Online** -> the gate and the badge are reading different state, so one of them is wrong.

**Inconclusive if**

If you cannot attribute a `node.exe` row to `Nova` specifically, do not kill any process: instead reach an unhealthy state the safe way — rename `tools\acp\node_modules` aside and click Restart (PERSONALIFECYCLE-09) — then run steps 8-9. Record which route you used.

> [!NOTE]
> The Starting dot (yellow) and the Degraded dot (orange) are nearly the same colour throughout this suite: always judge by the text label, never by the dot.

### PERSONALIFECYCLE-12 — Killing the adapter process flips the badge to Offline with a loop reason, even though the pipe stays open

**Free** · about 12 min

*Hunts the area's headline silent failure: a runner whose loops have died leaves the named pipe open, so a naive liveness check would keep reporting Online for an Agent that is deaf.*

**Before you start**

- The app is running with `Nova` Online.
- You can identify `Nova`'s adapter process (start the app with ONLY `Nova` in `App_Data\Teams\` if attribution is otherwise impossible — move other .md files out and back).
- `Nova`'s Room exists in the sidebar.

**Steps**

1. Arrange the browser so `/teammates` is visible in one tab and the `Nova` Room in another.
2. In `T-B` run `Adapters` and identify `Nova`'s `node.exe` row by CreationDate (it was created when `Nova` came Online).
3. In `T-B` run `Stop-Process -Id <pid>` for that row.
4. Do NOT touch the browser. Watch the `/teammates` tab for 15 seconds and write down every status word the `Nova` tile shows and roughly when it changes.
5. Hover the `Nova` tile's status line and read the tooltip.
6. Click the `Nova` tile and read the reason line on the card, word for word.
7. Switch to the `Nova` Room tab and read the area above the composer.
8. In `T-A` read the console for a Warning naming a loop (`read loop`, `consumer loop` or `event reader`) and for `The agent process disconnected.`
9. Check the other teammate's tile and Room: status and alert strip.
10. In the `Nova` Room, type `are you there` and press Enter, then watch the Room for 60 seconds.
11. Open the `Nova` card and click **Restart**; watch the status line until it settles.
12. In `T-B` run `Adapters` and confirm a fresh row exists.

**Pass if — all of these**

- Within a second or two of the kill and with NO interaction, the `Nova` tile changes to **Offline**.
- The reason names which loop died — text of the shape `Its event reader ended unexpectedly.` or `Its read loop ended unexpectedly: …` or `The pipe 'team' broke: …` — on both the tooltip and the card.
- The `Nova` Room grows a red alert strip reading `Nova is Offline: Its … ended unexpectedly…`.
- `T-A`'s console carries `The agent process disconnected.` and a warning naming the loop.
- The other teammate stays **Online** with no alert strip in its Room.
- Typing into the dead `Nova` Room does NOT hang the page: either the Message posts with no reply, or an error is reported — the browser stays responsive and no draft spins forever.
- Clicking **Restart** brings `Nova` back to **Online** and a fresh `node.exe` row appears.
- The Room and its Transcript survive the whole test.

**Fail if — any of these**

- The tile stays **Online** after the kill -> THE silent failure this area exists to prevent: the pipe is still open so a liveness check alone reports true, and the human is told a deaf Agent is healthy.
- The Room hangs forever after step 10 with a draft that never resolves -> an in-flight Turn's completion was never settled when the loop died; this exact bug has shipped once before.
- No reason text at all, only the word Offline -> the human cannot tell a killed adapter from a never-started one.
- Other teammates go Offline too -> the loop-death path is shared rather than per-Persona.
- The badge only corrects after a page reload -> the health event is not repainting open surfaces, so every badge in the app is as stale as the last navigation.

**Inconclusive if**

If you cannot attribute a `node.exe` row to `Nova`, do not kill anything — re-run with only `Nova` present. If the browser tab had been backgrounded by the OS, bring it to the front and allow 5 extra seconds before judging. If `Stop-Process` is denied by permissions, run `T-B` as the same user that started the app.

> [!NOTE]
> Do not file the fact that the Room and Transcript survive as a bug — no-cascade is deliberate.

### PERSONALIFECYCLE-13 — One Persona failing leaves every other Persona Online and messageable

**Free** · about 12 min

*Proves failures are contained per Persona: the supervisor does not abort its start loop or share a failure path.*

**Before you start**

- The adapter is installed and working.
- You can create three Personas.

**Steps**

1. In the browser at `/teammates` ensure three teammates exist and all read **Online**: `Nova`, `Chief of Staff`, and a third created via **New teammate** with Name `Ada`, Title `Third teammate`, Alias `ada`, body `You are Ada. Answer in one short sentence.`, Model containing `Haiku`, Effort `low`.
2. In `T-B` run `Adapters` and confirm three `claude-agent-acp` rows.
3. In `T-B` identify `Ada`'s row (newest CreationDate) and run `Stop-Process -Id <pid>` on it.
4. Wait 10 seconds without touching the browser.
5. Read all three tiles' status lines.
6. Open the `Nova` Room and the `Chief of Staff` Room and look above each composer.
7. In `T-A` count how many distinct Persona names appear in new warning lines.
8. In `T-B` run `Adapters` and count rows.
9. Now provoke a second, different failure: in `T-B` run `Rename-Item 'E:\Repos\Huddle\tools\acp\node_modules' 'node_modules.off'`, then in the browser click **New teammate** and add Name `Ghost`, Title `Fourth`, Alias `ghost`, body `You are Ghost.` and click **Add teammate**.
10. Read all four tiles' status lines and the two healthy Rooms again.
11. In `T-B` run `Rename-Item 'E:\Repos\Huddle\tools\acp\node_modules.off' 'node_modules'` to restore.

**Pass if — all of these**

- After `Ada`'s adapter is killed, only the `Ada` tile changes state; `Nova` and `Chief of Staff` still read **Online**.
- `Nova`'s and `Chief of Staff`'s Rooms show no alert strip.
- `T-A`'s warnings name exactly ONE Persona (`Ada`).
- `Adapters` keeps exactly two `claude-agent-acp` rows.
- After `Ghost` fails to start with the adapter missing, `Nova` and `Chief of Staff` are STILL Online with no alert strips, and the app keeps responding.
- Restoring the adapter folder leaves the healthy teammates untouched (no forced restart of anyone).

**Fail if — any of these**

- Every tile goes Offline together -> a shared failure path, or the supervisor aborting its start loop at the first exception; one bad Persona then takes down the whole team.
- An alert strip appears in an unrelated Room -> the strip is not scoped to Room membership.
- Adding a failing Persona blocks the page or leaves the app unresponsive -> a failed start is being awaited on a path that must not block.
- The healthy teammates restart when the adapter folder is renamed back -> something is restarting everyone on an unrelated filesystem event, destroying memory across the board.

**Inconclusive if**

If you cannot attribute adapter rows to Personas, skip step 3 and use only the adapter-missing half (steps 9-10); record which half you ran. If the machine is slow and the third teammate never reaches Online within 60 seconds, reduce to two teammates and note the reduction.

> [!NOTE]
> Sluggishness with four or more Personas is a recorded cost, not a defect — report it as an observation if you see it.

### PERSONALIFECYCLE-14 — An external edit to a Persona nested in a Team sub-folder reloads and restarts it, once per save

**Free** · about 15 min

*Hunts the highest-value silent failure in this area: a Persona in a sub-folder that is read once at startup and then never watched again — no error, no log, the file simply stops mattering.*

**Before you start**

- The app is running with adapters enabled and at least `Nova` Online.
- You have a plain text editor (Notepad works) that can save a file without locking it.

**Steps**

1. In `T-B` run `New-Item -ItemType Directory 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Business'`.
2. In `T-B` run `Move-Item 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova.md' 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Business\Nova.md'`.
3. In the browser at `/teammates`, WITHOUT reloading, wait 10 seconds and confirm the `Nova` tile is still present and still named `Nova`.
4. Click the `Nova` tile and read its **Persona file** section; close the card.
5. In `T-B` run `Adapters` and write down `Nova`'s row (CreationDate).
6. Open `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Business\Nova.md` in a text editor.
7. Append a new line at the end of the body: `Always mention that you are Nova.` and save the file ONCE.
8. Watch the `/teammates` page (no reload) for 30 seconds and write down every status word the `Nova` tile shows.
9. In `T-B` run `Adapters` and compare `Nova`'s row with step 5.
10. In the browser click the `Nova` tile and read the **Persona** section of the card.
11. Now test the debounce: in the editor add a space at the end of the file, save, and within one second remove it and save again.
12. Watch the tile for 30 seconds and count how many separate Offline/Starting/Online cycles occur, and count how many new `node.exe` rows appear in `Adapters`.
13. In `T-B` run `Set-Content 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\notes.txt' 'not a persona'` and watch every tile for 15 seconds.
14. In `T-B` run `Remove-Item 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\notes.txt'`.

**Pass if — all of these**

- Moving the file into `Teams\Business\` changes nothing visible: the tile is still `Nova`, still Online, the Room is still in the sidebar, and the card's **Persona file** path now ends in `Business\Nova.md`.
- Saving an edit to the nested file restarts `Nova` within about a second: the tile flickers through Offline/Starting and returns to **Online** with no page reload.
- `Adapters` shows `Nova`'s old `node.exe` replaced by one with a newer CreationDate.
- The card's **Persona** section shows the appended line `Always mention that you are Nova.`
- Two saves within one second produce exactly ONE restart cycle and ONE new adapter process.
- Creating and deleting `notes.txt` in the Teams directory restarts nobody — every tile stays **Online** with no flicker.

**Fail if — any of these**

- The nested file's edit produces NO restart and NO change on the card -> the watcher is not recursive; a Persona in a sub-folder is read once at startup and then silently stops mattering, with no error anywhere. This is the single highest-value defect in this area.
- Two quick saves produce two or more restarts -> the debounce is gone, so an editor that writes twice on save destroys the teammate's memory twice.
- Touching a non-.md file restarts Personas -> the watcher filter is gone and unrelated files now cost every teammate its memory.
- The tile vanishes after the move and never returns -> the recursive scan is not finding sub-folders at all.
- A burst of file changes leaves the page permanently stale (tiles never update again) -> the watcher's event buffer overflowed and the error-driven rebuild did not happen; look for a console line about a dropped-event buffer overflow.

**Inconclusive if**

Some editors write a temp file and rename, some write twice; if you cannot tell a debounce failure from your editor saving twice, repeat step 11 using `Add-Content` from `T-B`, which writes exactly once. If the app was started with `Team:Acp:Enabled=false`, you will see the card text reload but no restart — that half of the test is still valid; record that you ran the reload half only.

> [!NOTE]
> Leave `Nova.md` in `Teams\Business\` — PERSONALIFECYCLE-15 needs it there.

### PERSONALIFECYCLE-15 — Renaming or moving a Team sub-folder keeps its teammates reachable, or takes them offline cleanly

**Free** · about 12 min

*Hunts the second silent watcher failure: a filename filter of `*.md` never sees a DIRECTORY rename, because a directory name has no extension.*

**Before you start**

- PERSONALIFECYCLE-14 has been run and `Nova.md` sits in `App_Data\Teams\Business\`.
- The app is running with `Nova` Online.

**Steps**

1. In the browser at `/teammates`, confirm `Nova` reads **Online** and note the sidebar has a Room named `Nova`.
2. In `T-B` run `Rename-Item 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Business' 'BusinessOps'`.
3. Wait 10 seconds. WITHOUT reloading the browser, read the `Nova` tile: its Name, its Title/Alias line and its status.
4. Click the `Nova` tile and read the **Persona file** section, then the **Model** section.
5. Close the card and check the sidebar for the `Nova` Room.
6. Now prove the cached text is not stale: open `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\BusinessOps\Nova.md`, append the line `Renamed folder marker.` and save.
7. Wait 10 seconds, open the `Nova` card and check whether the **Persona** section shows `Renamed folder marker.`
8. In `T-B` run `Move-Item 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\BusinessOps' 'E:\Repos\Huddle\src\Huddle.App\App_Data\BusinessOps'` (out of the Teams directory entirely).
9. Wait 10 seconds and read the tile list and the sidebar.
10. In `T-B` run `Adapters`.
11. In `T-B` run `Move-Item 'E:\Repos\Huddle\src\Huddle.App\App_Data\BusinessOps\Nova.md' 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova.md'` and `Remove-Item 'E:\Repos\Huddle\src\Huddle.App\App_Data\BusinessOps' -Recurse` to restore.

**Pass if — all of these**

- After the folder rename, `Nova` is still listed, still named `Nova`, keeps its Title/Alias, its Team grouping and its Model, and its Room is still in the sidebar.
- The card's **Persona file** section shows the NEW path containing `BusinessOps`.
- An edit made AFTER the folder rename reaches the card (step 7 shows `Renamed folder marker.`) — the watcher is still following the file at its new path.
- After moving the folder out of `Teams\` entirely, the `Nova` tile disappears from the list, its adapter row leaves `Adapters`, and the Room named `Nova` REMAINS in the sidebar.
- Opening that orphaned `Nova` Room shows NO red alert strip.

**Fail if — any of these**

- The card's **Persona file** path still shows `Business` after the rename, and step 7's edit does not reach the card -> the watcher never saw a directory rename, so every Persona under the old path is now served from stale cached prompt text, forever, with no error. A silent failure.
- The teammate keeps its tile but loses its Model -> the stored Model is being keyed off the path rather than the Name.
- Moving the folder out leaves the tile behind and its adapter running -> a Persona whose file is gone is still live and still able to spend money.
- The orphaned `Nova` Room shows a permanent alert strip -> a teammate that is merely not running is being reported as a failure; every removed teammate would leave a permanent alert behind.
- The app shows `An unhandled error has occurred.` after a directory event -> a directory name (which has no extension) is not being accepted by the change handler.

**Inconclusive if**

If the rename is blocked because a file in the folder is open in an editor, close it and retry. If the app was started before `Teams\Business\` existed AND the tile never appeared at all, run PERSONALIFECYCLE-14 first — this test assumes the nested Persona was visible to begin with.

> [!NOTE]
> The Room and Transcript surviving the folder move is deliberate no-cascade behaviour, not a defect.

### PERSONALIFECYCLE-16 — A Persona file that becomes malformed is named in "Files that didn't load", and an Alias collision names BOTH files

**Free** · about 15 min

*Proves a file that fails to become a teammate is impossible to miss, and that a collision never picks a silent winner.*

**Before you start**

- The app is running.
- At least two Personas exist on disk (`Nova` and `Chief of Staff`).
- `Nova` is Online if adapters are enabled.

**Steps**

1. In the browser go to `http://localhost:5100/teammates` and note the tile list and whether a `Files that didn't load` block is present.
2. In `T-B` open `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova.md` in a text editor.
3. Delete the whole `title: 'Test teammate'` line and save the file.
4. Wait 10 seconds. WITHOUT reloading, read the top of the `/teammates` page and the tile list.
5. Read the entry in the `Files that didn't load` block: its path and its reason.
6. Check the sidebar for the Room named `Nova`, open it and look above the composer.
7. In `T-B` run `Adapters` and check whether `Nova`'s adapter row is gone.
8. Put the `title: 'Test teammate'` line back into the file exactly as it was and save.
9. Wait 15 seconds and confirm the tile returns and (with adapters enabled) reaches **Online** again.
10. Now the collision: edit `Chief of Staff.md` and change its `alias:` line to `alias: 'nova'` so it matches `Nova`'s alias exactly. Save.
11. Wait 10 seconds and read the tile list and the `Files that didn't load` block.
12. Restore `Chief of Staff.md`'s alias to `alias: 'cos'` and save; confirm both tiles return.

**Pass if — all of these**

- Removing `title:` makes the `Nova` tile disappear from the list within about a second, with no page reload.
- A `Files that didn't load` block appears at the TOP of the page listing the file's path and the reason `Persona frontmatter is missing required field 'Title'.`
- The Room named `Nova` stays in the sidebar, and opening it shows NO red alert strip.
- With adapters enabled, `Nova`'s `node.exe` row disappears — the rejected Persona's adapter is stopped.
- Restoring `title:` brings the tile back and (with adapters) restarts it to **Online**.
- With a duplicated alias, BOTH `Nova` and `Chief of Staff` vanish from the tile list and BOTH are listed in `Files that didn't load`, each reason naming the other file's path.
- Restoring the alias brings both back.

**Fail if — any of these**

- A malformed file simply vanishes with nothing on screen explaining why -> a user edits a file, the teammate disappears, and nothing anywhere says what went wrong.
- Only ONE of two colliding files is rejected -> a winner is being picked by enumeration order, so editing the loser silently does nothing forever, with no feedback anywhere.
- The adapter for a rejected Persona keeps running -> a teammate that the app says did not load is still live and can still spend money.
- The orphaned Room grows a permanent red alert strip -> an unhealthy state with no reason is being alerted on, putting a permanent alert in the Room.
- The reason text names an exception type or a line number rather than the missing field -> the diagnostic is not fit for the human reading it.

**Inconclusive if**

If your editor rewrites line endings or reorders the file on save, the reason text may differ; check the file contents in `T-B` with `Get-Content` before concluding. If the `Files that didn't load` block already listed entries at step 1, judge only the entries that appear during the test.

> [!NOTE]
> Alias-versus-Name collisions are also rejected on both sides; you can extend step 10 by setting `Chief of Staff`'s alias to `Nova` (matching the other's Name) and expecting the same both-rejected result.

### PERSONALIFECYCLE-17 — A stored Model the Adapter no longer advertises is Degraded with a reason, never a failed start

**Free** · about 12 min

*Proves a stale Model cannot brick a teammate: it starts on the adapter's default, still answers, and says so.*

**Before you start**

- `O-DB` is available. Without it this test CANNOT run.
- A Persona `Nova` exists with a stored Model (created with Haiku in PERSONALIFECYCLE-06).
- The adapter is installed and authenticated.

**Steps**

1. In `T-A` press Ctrl+C to stop the app and wait for the prompt.
2. In `T-B` run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "select * from persona_models;"` and write down the current row for `Nova`.
3. In `T-B` run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "update persona_models set model='claude-does-not-exist' where persona_name='Nova';"`.
4. Re-run the select from step 2 and confirm the value changed.
5. In `T-A` run `dotnet run --project src\Huddle.App --urls http://localhost:5100`.
6. In the browser open `http://localhost:5100/teammates` and watch the `Nova` tile until it settles (up to 60 seconds). Write down every status word.
7. Hover the tile's status line and read the tooltip.
8. Click the tile and read the reason line on the card, word for word.
9. Open the Room named `Nova` and read the area above the composer.
10. In `T-A` read the console for a Warning containing `not in the agent's advertised model catalog`.
11. In `T-B` run `Adapters` and confirm `Nova` has a live adapter row.
12. In `T-A` press Ctrl+C. In `T-B` run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "update persona_models set model=NULL where persona_name='Nova';"` to clear the stale row, then restart the app and confirm `Nova` returns to **Online** with no reason.

**Pass if — all of these**

- `Nova` reaches a settled state of **Degraded** — NOT Offline — with a live `node.exe` adapter row.
- The card's reason line reads exactly: `The Model 'claude-does-not-exist' is not in the Adapter's catalog; running on its default.`
- The tile's tooltip carries the same reason, and the `Nova` Room shows the strip `Nova is Degraded: The Model 'claude-does-not-exist' is not in the Adapter's catalog; running on its default.`
- `T-A`'s console carries the Warning `Requested model 'claude-does-not-exist' is not in the agent's advertised model catalog; continuing on the agent's default.`
- After clearing the row and restarting, `Nova` is **Online** with no reason line anywhere.

**Fail if — any of these**

- `Nova` fails to start (Offline with a start-failure reason) -> a stale stored Model bricks a teammate; any adapter upgrade that renames a model would take every teammate down.
- The tile reads **Online** with no badge at all -> the mismatch reached only the log, so a teammate is silently running on a model nobody chose.
- The tile reads **Offline** while the adapter is alive and answering -> the classification is wrong; Offline means unusable, and this teammate is usable.
- The Degraded reason also appears when the adapter's model catalog is legitimately EMPTY -> an empty catalog means "the agent never told us", not "your model is wrong", and must produce no report at all.

**Inconclusive if**

If `sqlite3` is unavailable, record this test as NOT RUN — there is no UI path to store a non-advertised model, and inventing one would not test the same thing. If the adapter cannot be authenticated, the catalog comes back empty and the Degraded report is correctly suppressed: record inconclusive, not fail. If you forget step 12, every later test runs against a Degraded `Nova` — always restore the row.

> [!NOTE]
> The card shows the STORED Model, not the session's live one, so it will still read the bogus id while the session actually runs on the adapter's default. That is a recorded limit.

### PERSONALIFECYCLE-18 — Editing a Hook at /settings does not restart a running Teammate

**Free** · about 8 min

*Proves rewording a system-prompt Hook does not throw away every teammate's conversation memory.*

**Before you start**

- The app is running with at least one teammate **Online**.
- `App_Data\hooks.json` may or may not exist — either is fine.

**Steps**

1. In the browser go to `http://localhost:5100/teammates` and confirm at least one tile reads **Online**.
2. In `T-B` run `Adapters` and write down every row's ProcessId and CreationDate.
3. In the sidebar click **Settings**.
4. Confirm the **Hooks** tab is selected (it is the default).
5. Find the first hook field. Note whether it carries a `Next session` badge.
6. Click into that field's textarea and append the text ` (edited)` at the end.
7. Confirm a `Unsaved` badge appears next to that field's label.
8. Click **Save** at the bottom of the form.
9. Switch back to `/teammates` and watch every tile for 30 seconds.
10. In `T-B` run `Adapters` and compare every ProcessId and CreationDate with step 2.
11. In `T-B` run `Test-Path 'E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json'`.
12. Return to **Settings**, click the **Reset** button on the field you edited, and click **Save** to restore the shipped wording.
13. In `T-B` run `Adapters` once more.

**Pass if — all of these**

- After saving the hook, every tile stays **Online** with no flicker through Offline or Starting.
- `Adapters` shows exactly the same ProcessIds and CreationDates as before the save — nothing restarted.
- A `Modified` badge appears on the edited field after saving.
- `App_Data\hooks.json` exists after the first save (it is absent before the first save, and that is expected).
- Resetting and saving again also restarts nothing.

**Fail if — any of these**

- Any teammate restarts on a hook save -> every reworded sentence in Settings now destroys every teammate's conversation memory.
- All teammates go Offline and stay there -> the hook save is breaking the running sessions rather than being inert on them.
- The Save button is disabled even after an edit -> the unsaved-change tracking is broken and hooks cannot be saved at all.
- `An unhandled error has occurred.` on save -> the hook write path is throwing into the circuit.

**Inconclusive if**

If no teammate is Online (adapters disabled, or all failed), this test cannot distinguish "did not restart" from "was never running" — get one Online first. If a hook field shows an error under it after your edit (a broken placeholder), undo the edit and pick a different field: you want a valid edit, not a rejected one.

> [!NOTE]
> A reworded system-prompt Hook being inert on an already-running teammate is the documented trade, not a bug — do not file it. The `Next session` badge is the app telling you exactly that.

### PERSONALIFECYCLE-19 — Removing a Persona takes it offline but leaves its Agent, Room and Transcript, and raises no alert

**Free** · about 12 min

*Proves removal is a deliberate no-cascade: history survives, the process does not, and the orphaned Room stays quiet.*

**Before you start**

- The app is running.
- A teammate exists that has connected at least once and has a Room (for example `Chief of Staff`).
- Optional: its Transcript has content — you can create content for free by posting a Message in the demo `echo` Room instead, but for this test a Room with even one Human Message is enough (typing a Message to a teammate that will then reply costs money; to stay free, type a Message and then immediately remove the teammate, or accept an empty Transcript).

**Steps**

1. In `T-B` run `Adapters` and identify the target teammate's `node.exe` row if you can.
2. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\rooms'` and note the files.
3. OPTIONAL (sqlite3): run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "select name from users; select id,name from rooms; select * from persona_models; select * from persona_efforts;"` and save the output.
4. In the browser at `/teammates`, click the `Chief of Staff` tile.
5. Click **Remove**.
6. Observe what happens to the action row — do NOT click anything yet.
7. Click **Cancel** and confirm the row returns to normal.
8. Click **Remove** again, then click **Confirm**.
9. Read the page: the card, the tile list, and the sidebar.
10. Click the Room named `Chief of Staff` in the sidebar and read the whole Room, especially above the composer.
11. In `T-B` run `Adapters` and count rows.
12. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams' | Select-Object Name` and `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\work' | Select-Object Name`.
13. OPTIONAL (sqlite3): re-run the query from step 3 and compare.

**Pass if — all of these**

- Clicking **Remove** swaps the button row in place for **Confirm** and **Cancel** — no browser dialog appears.
- **Cancel** returns the row to the normal actions with nothing removed.
- **Confirm** closes the card and removes the tile from `/teammates`.
- The Room named `Chief of Staff` STAYS in the sidebar with its full history and its member list intact.
- That Room shows NO red alert strip.
- `App_Data\Teams\Chief of Staff.md` is gone.
- The removed teammate's `node.exe` row is gone from `Adapters` (row count drops by one).
- `App_Data\work\Chief of Staff` is LEFT BEHIND — nothing deletes it (expected).
- `App_Data\rooms\*.jsonl` files are unchanged.
- If sqlite3 was available: the `users` and `rooms` rows still exist, and the `persona_models` and `persona_efforts` rows for that Name are GONE.

**Fail if — any of these**

- The Room or the Transcript disappears -> removal is cascading, and a user who removes a teammate loses the conversation history they wanted to keep.
- A permanent red alert strip appears in the orphaned Room -> a teammate that is merely not running is being reported as a failure.
- The tile lingers after Confirm -> the page is not reloading the Persona list on removal.
- The adapter process survives the removal -> a removed teammate is still live and can still spend money.
- A browser `confirm()` dialog appears instead of the inline Confirm/Cancel -> the destructive-action pattern has regressed to a native dialog.
- The `persona_models` / `persona_efforts` rows survive -> re-creating the same Name later would silently resurrect an old Model or Effort (see PERSONALIFECYCLE-20).

**Inconclusive if**

If sqlite3 is not installed, run PERSONALIFECYCLE-20 immediately after this test — it checks the same fact through the UI. If you could not attribute a `node.exe` row to the removed teammate, judge by the total row count dropping by exactly one.

> [!NOTE]
> Do NOT file the leftover Agent, Room, Transcript or Work Dir as bugs; all four are deliberate. What IS a bug is a leftover Model or Effort row.

### PERSONALIFECYCLE-20 — Re-creating a removed Persona under the same Name does not resurrect its old Model or Effort

**Free** · about 8 min

*Proves the stored Model and Effort really go with a removal, so a recreated teammate starts from the agent's defaults.*

**Before you start**

- PERSONALIFECYCLE-19 has just been run: a teammate that had Model = Haiku and Effort = low was removed.
- The app is still running.

**Steps**

1. In the browser at `/teammates` click **New teammate**.
2. In **Name** type exactly the Name you removed: `Chief of Staff`.
3. In **Title** type `Keeps the team honest`. In **Alias** type `cos`. In the **Persona body** textarea type `You are the Chief of Staff. Answer in one short sentence.`
4. Do NOT touch the **Model** or **Effort** selects — leave both showing `Use the agent's default`.
5. Click **Add teammate**.
6. On the resulting `Teammate` card read the **Model** section and the **Effort** section.
7. Close the card, click the tile again and read both sections once more.
8. OPTIONAL (sqlite3): in `T-B` run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "select * from persona_models where persona_name='Chief of Staff'; select * from persona_efforts where persona_name='Chief of Staff';"`

**Pass if — all of these**

- The card's **Model** section reads exactly `Agent default`.
- The card's **Effort** section reads exactly `Model default`.
- Reopening the card shows the same two values — nothing appears after a refresh either.
- If sqlite3 was available: either no row exists for that Name, or the row's model/effort value is NULL.

**Fail if — any of these**

- The card shows `Haiku` or `low` -> a removed setting was silently resurrected; the user gets a model they did not choose and, worse, will not think to look.
- The Model section is blank rather than `Agent default` -> the default sentinel is being rendered as an empty string, so the user cannot tell "no choice" from "a choice that failed to display".
- An empty string was written to the database (a row exists with an empty model) -> that row never resolves and will warn on every restart.

**Inconclusive if**

If PERSONALIFECYCLE-19 was not run immediately before this, the Name may never have had a stored Model, in which case this test proves nothing — remove and re-create deliberately, with Haiku/low set the first time. If the Model select was empty when you created the original (no adapter catalog), the same applies.

> [!NOTE]
> The mirror-image rule also matters and is covered by PERSONALIFECYCLE-21: a RENAME must MOVE these rows rather than drop them.

### PERSONALIFECYCLE-21 — Editing the frontmatter name: renames the Teammate and moves its Model and Effort, leaving a documented ghost

**Free** · about 12 min

*Proves the rename path carries the stored Model and Effort across, does not rename the file on disk, and restarts the teammate under its new identity — while confirming the leftover Room and Agent are the documented no-cascade limit.*

**Before you start**

- The app is running with adapters enabled.
- A teammate `Nova` exists, is **Online**, has Model = Haiku and Effort = low, and has a Room named `Nova`.

**Steps**

1. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams' -Recurse -Filter *.md | Select-Object FullName` and write down the exact filenames.
2. OPTIONAL (sqlite3): run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "select * from persona_models; select * from persona_efforts; select name from users;"` and save the output.
3. In the browser at `/teammates` click the `Nova` tile, read its **Model** and **Effort** sections, then click **Edit**.
4. In the **Persona text** textarea, find the line `name: 'Nova'` and change it to `name: 'Nova Prime'`. Change nothing else.
5. Click **Save**.
6. Read the resulting card: its title, its Name, its **Model** section and its **Effort** section.
7. Close the card and read the tile list.
8. Watch the sidebar Room list for 30 seconds.
9. In `T-B` re-run the `Get-ChildItem` from step 1.
10. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\work' | Select-Object Name`.
11. OPTIONAL (sqlite3): re-run the query from step 2 and compare.
12. In `T-B` run `Adapters` and confirm a fresh adapter row exists.

**Pass if — all of these**

- The tile `Nova` is replaced by a tile named `Nova Prime`, which reaches **Online**.
- The card for `Nova Prime` shows the SAME Model (Haiku) and the SAME Effort (low) the old Name had.
- A NEW Room named `Nova Prime` appears in the sidebar, and a NEW `App_Data\work\Nova Prime\` folder is created.
- The `.md` file on disk KEEPS its original filename (`Nova.md`) — identity moved, storage did not.
- A fresh `node.exe` row exists — the teammate restarted under its new identity.
- If sqlite3 was available: `persona_models` and `persona_efforts` are now keyed on `Nova Prime` with NO row left under `Nova`; `users` lists BOTH `Nova` and `Nova Prime`.
- The OLD Room named `Nova` stays in the sidebar with its whole history, and opening it shows no red alert strip.

**Fail if — any of these**

- The Model or Effort is LOST by the rename (the card reads `Agent default` / `Model default`) -> those rows are being dropped rather than moved; a user renaming a teammate silently loses its model choice.
- Rows exist under BOTH Names -> the old row was not removed, and removing/recreating `Nova` later would resurrect a setting.
- The `.md` file is renamed on disk -> storage is being driven by identity; combined with PERSONALIFECYCLE-07 this means the two have been conflated.
- The teammate does not restart under the new identity (no fresh process) -> the new system prompt never takes effect, because prompt, Model and Effort are all fixed when a session is created.
- `An unhandled error has occurred.` on save -> the rename path is throwing.

**Inconclusive if**

If the save is refused with a message on the card, read it: a collision with an existing Name or Alias is a legitimate refusal, so pick an unused Name and retry. If sqlite3 is unavailable, judge the Model/Effort carry-over from the card alone and record the ghost checks as not-checked.

> [!NOTE]
> The leftover Room named `Nova`, the leftover `users` row and the leftover Transcript are the DOCUMENTED no-cascade limit — do not file them. Rename `Nova Prime` back to `Nova` afterwards if later tests expect that Name.

### PERSONALIFECYCLE-22 — Health and presence repaint every open surface live, in every browser tab

**Free** · about 10 min

*Proves both halves of the badge — health reports and pipe liveness — drive a repaint of every open page, so no tab is ever left with a stale badge.*

**Before you start**

- The app is running with at least one teammate **Online** and a Room for it.
- You can open three browser tabs against `http://localhost:5100`.

**Steps**

1. Open tab 1 at `http://localhost:5100/teammates`.
2. Open tab 2 at `http://localhost:5100/teammates`.
3. Open tab 3 at the Room for that teammate (`/rooms/<its room>` — click it in the sidebar).
4. Arrange the windows so you can see at least tab 1 and tab 3 at once, and keep tab 2 reachable with one click.
5. In `T-B` run `Adapters` and identify the teammate's adapter process.
6. Run `Stop-Process -Id <pid>` on it.
7. Without touching any tab, watch tab 1 and tab 3 for 10 seconds and write down when each changes.
8. Click to tab 2 and read the tile's status WITHOUT reloading.
9. In tab 1 open the teammate's card and click **Restart**.
10. While the restart runs, watch tab 2's tile and tab 3's Room strip without touching them.
11. Once the teammate is Online again, navigate tab 3 away (click **Teammates**) and back to the Room, twice.
12. Watch for any `An unhandled error has occurred.` banner at the bottom of any tab for 30 seconds.

**Pass if — all of these**

- Within about a second of the kill, tab 1's tile reads **Offline** with a reason, and tab 3's Room grows the red alert strip — both without any reload or navigation.
- Tab 2, brought forward without reloading, also reads **Offline** with the same reason.
- During the restart from tab 1, tab 2's tile and tab 3's strip follow the same state changes (Starting, then Online with the strip gone) without being touched.
- Navigating tab 3 away and back twice produces no error banner in any tab.

**Fail if — any of these**

- One tab updates and another does not -> the health/presence events are reaching only the tab that acted, so any other open window shows a badge that is silently wrong.
- A badge only corrects after F5 -> live repainting is broken; every surface is as stale as its last navigation.
- The tile updates but the Room strip does not (or vice versa) -> only one of the two facts is driving the repaint, so the badge lags one of them.
- `An unhandled error has occurred.` appears after navigating away and back -> a page failed to unsubscribe from the shared event hub when it was disposed, and that error will recur forever until the circuit is reloaded.

**Inconclusive if**

If the browser throttles background tabs (some do aggressively), bring each tab to the foreground before judging it and allow 5 seconds; only conclude "did not update" for a tab you have looked at in the foreground without reloading. If you cannot attribute an adapter process, use the adapter-missing route (rename `tools\acp\node_modules` aside, click Restart) to change state instead.

> [!NOTE]
> This is the cheapest test that catches a component failing to unsubscribe from a singleton event on dispose — a failure that never dies once it starts.

### PERSONALIFECYCLE-23 — An app restart brings every Persona back, orphans no process, and deliberately loses only in-memory state

**Free** · about 12 min

*Proves shutdown is clean and startup is complete, and separates the deliberate in-memory losses from real data loss.*

**Before you start**

- The app is running with two or more teammates **Online**, each with a Room.
- At least one Room has some Transcript content (even a single Human Message).

**Steps**

1. In `T-B` run `Adapters` and write down every row.
2. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\rooms' | Select-Object Name, Length` and write the output down.
3. In the browser open one Room and note the last three visible Messages.
4. In `T-A` press Ctrl+C and wait for the prompt to return.
5. In `T-B` run `Adapters` immediately, then again 10 seconds later.
6. In `T-A` run `dotnet run --project src\Huddle.App --urls http://localhost:5100`.
7. In the browser open `http://localhost:5100/teammates` and watch every tile until they settle (up to 90 seconds). Write down the status sequence for each.
8. Check the sidebar Room list against what it held before the restart.
9. Open the Room from step 3 and compare the last three Messages.
10. In `T-B` re-run the commands from steps 1 and 2 and compare.
11. In `T-A` read the console for any `failed to start` warnings.

**Pass if — all of these**

- After Ctrl+C, `Adapters` shows NO rows containing `claude-agent-acp` — every adapter exited with the app.
- On restart every Persona starts automatically: each tile goes Offline, then Starting, then **Online**, with no human action.
- The sidebar holds exactly the same Rooms as before, with no duplicates.
- The Room's last three Messages are identical to before the restart.
- `App_Data\rooms\*.jsonl` file sizes are unchanged across the restart.
- No `failed to start` warnings appear for teammates that were healthy before.

**Fail if — any of these**

- A `node.exe` adapter survives Ctrl+C -> shutdown orphans processes; those hold sessions open and can keep spending after the app is gone.
- A Persona does not come back at all -> startup is not starting everything it started last time; the user has to notice and click Restart.
- A Room is duplicated after the restart -> Rooms are being minted again rather than found.
- The Transcript lost Messages, or a `.jsonl` file shrank -> real data loss, not the deliberate in-memory kind.
- Tiles stay Starting forever after a restart -> the parallel start is deadlocking.

**Inconclusive if**

If Ctrl+C does not stop the app within 30 seconds, note it and use Ctrl+C again rather than killing the process — a forced kill cannot tell you whether shutdown is clean. If the machine is slow, allow up to 90 seconds for all tiles to settle before judging.

> [!NOTE]
> A paused Budget prompt, a streaming Draft and the per-Room Budget counter are all per-process and in memory, so they are GONE after a restart by design. Do not file those. If you have a paused Room from PERSONALIFECYCLE-31 or -32, confirm the prompt is gone and the Room shows a fresh Budget — that is the expected result.

### PERSONALIFECYCLE-24 — Four Personas cost four adapter processes, all reach Online, and all exit on shutdown

**Free** · about 15 min

*Measures the per-Persona process cost and proves the parallel start does not deadlock or leak.*

**Before you start**

- The adapter is installed and authenticated.
- You are willing to create four Personas.
- No app is running (start fresh so the startup path is what is measured).

**Steps**

1. In `T-B` confirm `App_Data\Teams\` holds exactly four `.md` files. If not, create the missing Personas through **New teammate** (Names `Nova`, `Ada`, `Bram`, `Cleo`; Title `Test teammate`; Aliases `nova`, `ada`, `bram`, `cleo`; body `You are <Name>. Answer in one short sentence.`; Model containing `Haiku`; Effort `low`), then stop the app with Ctrl+C.
2. In `T-A` run `dotnet run --project src\Huddle.App --urls http://localhost:5100` and note the time.
3. In the browser open `http://localhost:5100/teammates` and watch all four tiles until they settle, up to 120 seconds. Write down how long the last one takes.
4. In `T-B` run `Adapters` and count rows containing `claude-agent-acp`.
5. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\work' | Select-Object Name`.
6. Click between two Rooms and back to `/teammates` and note whether the UI stays responsive (any click taking more than about two seconds to paint is worth recording).
7. In `T-A` press Ctrl+C and wait for the prompt.
8. In `T-B` run `Adapters` immediately and again 10 seconds later.

**Pass if — all of these**

- All four tiles reach **Online**.
- `Adapters` shows exactly FOUR `claude-agent-acp` rows — one per Online Persona, no more.
- `App_Data\work` has exactly four Persona subfolders, named after the four frontmatter Names.
- The UI stays usable throughout; navigation paints without visible stalls.
- After Ctrl+C, `Adapters` shows ZERO `claude-agent-acp` rows.

**Fail if — any of these**

- More adapter rows than Online Personas -> processes are leaking per start or restart; each one holds a session.
- A tile stuck on **Starting** while the others are Online -> the parallel start is deadlocking or one adapter is hanging.
- Adapters survive shutdown -> orphaned processes that keep sessions open.
- A start failure mentioning a port or a bind error -> each Persona gets its own loopback tool server, and exhausting ports would cap how many teammates can run at all; report the exact message.
- Fewer work subfolders than Personas -> a host factory never ran for one of them.

**Inconclusive if**

If authentication fails for all four, this measures nothing — fix the login and re-run. If the machine has other `node.exe` processes, count only rows whose CommandLine contains `claude-agent-acp`. If the last tile takes longer than 120 seconds, record the timing as an observation and mark the test inconclusive rather than failed, then retry once.

> [!NOTE]
> The design is documented as worth revisiting past roughly four Personas: slowness at five or six is a known cost, not a defect. Report timings as observations.

### PERSONALIFECYCLE-25 — An Adapter that needs authentication reports that as its own distinct reason

**Free** · about 10 min

*Proves an unauthenticated adapter is narrowed apart from a missing one on the tile, rather than presenting identically.*

**Before you start**

- A machine where the adapter is INSTALLED but the Claude login is absent or expired. If you cannot produce that state safely, this test is NOT RUN — see inconclusive.

**Steps**

1. Reach the unauthenticated state without damaging the tester's own credentials: preferably run the app as a different OS user, or on a machine that has never logged in to Claude. Do not delete or move another user's credential files.
2. In `T-A` start the app with adapters enabled: `dotnet run --project src\Huddle.App --urls http://localhost:5100`.
3. In the browser open `http://localhost:5100/teammates` and wait for the tiles to settle.
4. Read the tile status and hover for the tooltip.
5. Click the tile and read the reason line, word for word.
6. Open that teammate's Room and read the strip above the composer.
7. In `T-A` read the console for a Warning containing `failed to start` and note the exception type it carries.
8. In `T-A` also look for a line containing `Model catalog probe skipped: the adapter needs authentication.`
9. Click **New teammate** and read the Model select's hint.

**Pass if — all of these**

- The tile reads **Offline** and the card's reason line reads either `The Adapter needs authentication: <method names>.` or, when the adapter names no methods, exactly `The Adapter needs authentication.`
- That reason is NOT the missing-adapter text, and is NOT a bare exception type name.
- The tooltip and the Room strip carry the same reason as the card.
- `T-A`'s console carries a Warning naming an authentication-required exception.
- The Model picker shows `This agent advertises no models, so it will use its own default.` — the picker being ambiguous here is EXPECTED and recorded, and the console line about a skipped probe is what disambiguates it.

**Fail if — any of these**

- The reason is the missing-adapter text (`No ACP adapter is installed…`) while the adapter file is present -> the two failure classes are being collapsed, and a user will run an install script that fixes nothing.
- The reason is a bare exception type such as `AgentAuthenticationRequiredException:` with no explanation -> the human is told the class name of a problem, not the problem.
- No reason at all, just Offline -> the classification was lost entirely.

**Inconclusive if**

If you cannot safely produce an unauthenticated adapter, record this test as NOT TESTED — explicitly, with the reason — rather than marking it passed. Never log the tester out of their own Claude session to provoke it, and never move or delete credential files. If the adapter is missing as well as unauthenticated, you are testing the wrong thing: restore the adapter first.

> [!NOTE]
> That the model PICKER cannot tell an unauthenticated adapter from a missing one is a recorded limit — only a Persona that failed to START says which. Do not file the picker ambiguity.

### PERSONALIFECYCLE-26 — MONEY: saving the Edit card with nothing changed does NOT restart the Teammate

**💰 Spends money** · about 12 min

*Proves a no-op save is free: the session, and therefore the teammate's conversation memory, survives opening and closing the Edit form.*

**Before you start**

- The app is running with `Nova` **Online** at Model = Haiku, Effort = low.
- `Nova` has a Room in the sidebar.
- COST: two one-sentence Haiku Turns (a few cents at most).

**Steps**

1. In `T-B` run `Adapters` and write down `Nova`'s ProcessId and CreationDate.
2. In the browser open the Room named `Nova`.
3. In the composer type `Remember the word ZEBRA. Reply with just OK.` and press Enter. Wait for the reply.
4. Go to `/teammates` and click the `Nova` tile.
5. Click **Edit**.
6. Change NOTHING. Do not click into the textarea, do not touch the Model or Effort selects.
7. Click **Save**.
8. Watch the tile's status line for 30 seconds and write down every word it shows.
9. In `T-B` run `Adapters` and compare `Nova`'s ProcessId and CreationDate with step 1.
10. Go back to the Room named `Nova`.
11. In the composer type `What word did I ask you to remember?` and press Enter. Wait for the reply.
12. Read the reply.

**Pass if — all of these**

- After **Save** the tile stays **Online** for the whole 30 seconds — no flicker through Offline or Starting.
- `Nova`'s `node.exe` has the SAME ProcessId and the SAME CreationDate before and after the save.
- The reply to step 11 contains `ZEBRA` — the session and its memory survived the save.

**Fail if — any of these**

- The tile flickers and a new ProcessId appears -> a no-op save restarts the teammate, so every time anyone opens and closes the Edit form the teammate forgets everything, and a new session is paid for.
- The reply does not know `ZEBRA` while the ProcessId is unchanged -> something other than a restart cleared the session's memory; report the exact reply.
- A real change (make one deliberately and re-run) is ALSO treated as a no-op -> the opposite failure: edits silently never take effect. Test this by running PERSONALIFECYCLE-27 immediately after.
- `An unhandled error has occurred.` on Save -> the save path throws when nothing changed.

**Inconclusive if**

If the first reply does not acknowledge the word at all (the model answered something else), re-word the prompt as `Please remember this word: ZEBRA. Reply with only: OK.` and start again — you cannot judge memory you never established. If `Nova` was Degraded or Offline when you started, fix that first. If the adapter times out mid-Turn, mark inconclusive and retry once; do not retry more than twice (each retry costs money).

> [!NOTE]
> The comparison is by value over the whole Persona — text, Model and Effort — so a no-op save must compare equal and do nothing. This is also what a debounced watcher event after a no-op file write must not cause.

### PERSONALIFECYCLE-27 — MONEY: editing the Persona body restarts the session and the new instruction takes effect

**💰 Spends money** · about 12 min

*Proves a prompt edit reaches the running teammate through a restart, with no app restart, and that the whole file (frontmatter included) round-trips.*

**Before you start**

- The app is running with `Nova` **Online**.
- COST: two one-sentence Haiku Turns.

**Steps**

1. In `T-B` run `Adapters` and write down `Nova`'s ProcessId.
2. In the browser open the Room named `Nova`, type `Say hello in one sentence.` and press Enter. Read the reply and note whether it starts with the word `PLUM`.
3. Go to `/teammates`, click the `Nova` tile, and click **Edit**.
4. Read the textarea's label and the grey hint under it, word for word.
5. Confirm the textarea contains the WHOLE file, starting with `---` and the `name:`, `title:` and `alias:` lines.
6. Click at the very end of the textarea text and type a new line: `Always begin every reply with the word PLUM.`
7. Click **Save**.
8. Watch the tile's status line for 40 seconds and write down every word.
9. In `T-B` run `Adapters` and compare `Nova`'s ProcessId with step 1.
10. In `T-B` run `Get-Content 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova.md'` and check that the frontmatter block is intact AND the new line is present.
11. Once the tile reads **Online**, return to the Room, type `Say hello in one sentence.` and press Enter.
12. Read the reply.

**Pass if — all of these**

- The Edit textarea is labelled **Persona text** and its hint reads exactly: `Markdown. This is the whole Persona file, front matter included, and becomes the teammate's system prompt. Saving restarts it, which clears what it remembers.`
- The textarea holds the whole file including the `---` frontmatter block.
- After Save, the tile goes through Starting and back to **Online**, and `Nova` has a NEW `node.exe` ProcessId.
- `Nova.md` on disk still has its complete frontmatter (`---`, `name:`, `title:`, `alias:`, `---`) and now ends with the appended instruction.
- The reply after the edit begins with `PLUM`, and the reply before it did not.

**Fail if — any of these**

- The reply does not obey the new instruction and no new ProcessId appeared -> the edit does not take effect until the app is restarted; a prompt change is silently inert.
- The saved file has lost its frontmatter -> the card is saving only the body, which turns the Persona into a rejected file on the next scan.
- A save that breaks the frontmatter is accepted and written -> the file on disk must be left untouched and the card must refuse inline with the parser's reason; test this by deleting the `title:` line in the textarea and clicking Save.
- Two restarts occur from one Save (two new ProcessIds) -> a save is firing the restart path twice, doubling the cost and the memory loss.
- The reply obeys the instruction but no restart was observed -> impossible unless something else changed; re-check the process list, because a system prompt is fixed when the session is created.

**Inconclusive if**

If the model ignores the PLUM instruction even after a confirmed restart (models sometimes do), re-run with a stronger instruction such as `Your reply must start with the single word PLUM followed by a colon.` — a model declining to follow an instruction is not a lifecycle defect. If the restart is confirmed by a new ProcessId and the file on disk is correct, the restart half of this test has PASSED even if the model's obedience is inconclusive; report the two halves separately.

> [!NOTE]
> Restarting clears conversation memory every time, by design: a system prompt is fixed when the session is created, so a stop-and-restart is the only way an edit can take effect.

### PERSONALIFECYCLE-28 — MONEY: changing the Model from Haiku to Sonnet restarts the session and clears what the Teammate remembers

**💰 Spends money** · about 18 min

*Proves a Model change is applied by a new session, that the stored value is the model ID (not a display label), and that memory loss is the visible consequence.*

**Before you start**

- The app is running with `Nova` **Online** at Model = Haiku, Effort = low.
- The Model picker lists a Sonnet entry.
- COST: about four one-sentence Turns — two on Haiku, two on Sonnet. Never select Opus.

**Steps**

1. In `T-B` run `Adapters` and write down `Nova`'s ProcessId.
2. In the browser open the Room named `Nova`. Type `Remember the word ZEBRA. Reply with just OK.` and press Enter; wait for the reply.
3. Type `Which model are you? Answer in one short sentence.` and press Enter; write the answer down verbatim.
4. Go to `/teammates`, click the `Nova` tile, and click **Edit**.
5. In the **Model** select, change the value from the Haiku entry to the entry whose label contains `Sonnet`. Do NOT pick Opus.
6. Observe what happens to the **Effort** select immediately after the model change.
7. Click **Save**.
8. Watch the tile's status for 40 seconds and write down every word.
9. In `T-B` run `Adapters` and compare `Nova`'s ProcessId with step 1.
10. Click the `Nova` tile and read its **Model** section.
11. Click **Edit** again and confirm the Model select still shows the Sonnet entry (not `Use the agent's default`), then click **Cancel**.
12. Return to the Room and type `What word did I ask you to remember?`; press Enter and read the reply.
13. Type `Which model are you? Answer in one short sentence.`; press Enter and compare with step 3.
14. In `T-A` search the console for any line containing `not in the agent's advertised model catalog`.
15. OPTIONAL (sqlite3): in `T-B` run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "select * from persona_models;"`.
16. **Restore.** Open the Nova card, click **Edit**, set **Model** back to the Haiku entry, click **Save**, and wait for the tile to read Online. Every later test states Haiku as a precondition.

**Pass if — all of these**

- After Save the tile flickers through Offline/Starting and returns to **Online**, and `Nova` has exactly ONE new `node.exe` ProcessId (the old one is gone).
- The card's **Model** section now names the Sonnet entry, and reopening Edit still shows Sonnet selected.
- The reply to step 12 does NOT know `ZEBRA` — the session was genuinely replaced.
- The answer to step 13 differs from step 3 — a different model is answering.
- `T-A`'s console contains NO `not in the agent's advertised model catalog` warning.
- If sqlite3 was available: `persona_models` holds a model ID string (for example one beginning `claude-sonnet-`), not a human display label.

**Fail if — any of these**

- No restart at all (same ProcessId) -> the Model change never reached the restart decision; the teammate keeps thinking with the old model while the card claims otherwise.
- TWO restarts from one Save -> the change is firing the restart path twice, paying for two sessions.
- The teammate still remembers `ZEBRA` -> the session was not replaced, so the Model cannot have changed either (a Model is fixed when a session is created).
- A `not in the agent's advertised model catalog` warning appears after picking from the picker itself -> a display NAME was stored where an ID belongs; every start will now fall back to the adapter's default.
- Reopening Edit shows `Use the agent's default` -> the stored choice is not being read back, so the next save would silently wipe it.

**Inconclusive if**

If the model's answer to "Which model are you?" is vague or identical both times, that alone does not decide the test — judge on the ProcessId change, the ZEBRA memory loss and the absence of a catalog warning, and record the model's self-report as inconclusive. If the Sonnet entry is missing from the picker, do NOT substitute Opus: record the test as not-run. If a Turn errors mid-flight, retry once only.

> [!NOTE]
> Both a system prompt and a Model are fixed when the session is created, so "the change took effect" can only ever mean "a new session started". The restore step is mandatory: tests from -29 onward assume Nova is back on Haiku at low effort, and their cost estimates depend on it.

### PERSONALIFECYCLE-29 — MONEY: changing the Effort from low to medium restarts the session, and a Model change clears the Effort selection

**💰 Spends money** · about 15 min

*Proves an Effort change is applied only by restart (there is no live mid-session switching), and that the effort ladder is re-read per model rather than carried across. The Edit card is a real `MudDialog` and its Model/Effort dropdowns are `MudSelect` (Stages 3-4 of the MudBlazor migration); neither changes what this test is checking.*

**Before you start**

- The app is running with `Nova` **Online**, Model = Haiku, Effort = low.
- COST: about two to three one-sentence Haiku Turns. Never select high, xhigh or max.

**Steps**

1. In `T-B` run `Adapters` and write down `Nova`'s ProcessId.
2. In the browser open the Room named `Nova`, type `Remember the word QUINCE. Reply with just OK.` and press Enter; wait for the reply.
3. Go to `/teammates`, click the `Nova` tile, click **Edit**.
4. Confirm the **Effort** select currently shows `low`.
5. In the **Effort** select choose `medium`. Leave the **Model** select alone.
6. Click **Save**.
7. Watch the tile's status for 40 seconds and write down every word.
8. In `T-B` run `Adapters` and compare `Nova`'s ProcessId with step 1.
9. Click the `Nova` tile and read its **Effort** section.
10. Return to the Room, type `What word did I ask you to remember?`, press Enter and read the reply.
11. Now test the model-change interaction: open the card, click **Edit**, and note the current Effort value.
12. In the **Model** select change the model (Haiku to Sonnet, or Sonnet back to Haiku — never Opus).
13. Immediately read the **Effort** select, its hint, and the note that appears above it, and watch all three for 20 seconds.
14. Click **Cancel** (do not save).
15. In `T-A` search the console for any line containing `not in the agent's advertised effort catalog`.
16. OPTIONAL (sqlite3): run `sqlite3 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' "select * from persona_efforts;"`.
17. **Restore.** Open the Nova card, click **Edit**, set **Effort** back to `low`, click **Save**, and wait for Online.

**Pass if — all of these**

- After Save the tile flickers and returns to **Online** with exactly ONE new `node.exe` ProcessId.
- The card's **Effort** section reads `medium`.
- The reply to step 10 does NOT know `QUINCE`.
- When the Model is changed in the Edit card, the Effort select resets to `Use the agent's default` and its hint shows `Reading the effort levels this model offers…` while the new ladder is read.
- That reset is **announced**: a note appears between the Model and Effort selects saying the Effort was reset and that each model advertises its own effort levels. Added for #45 — the reset was always correct, the silence was the defect. It must NOT appear on a Model change made while the Effort is already `Use the agent's default`, because nothing was discarded.
- `T-A`'s console contains NO `not in the agent's advertised effort catalog` warning.
- If sqlite3 was available: `persona_efforts` holds an effort ID, not a display label.
- The Effort select does NOT offer the adapter's own `default` entry alongside the blank `Use the agent's default` option — there is exactly one way to say "default".

**Fail if — any of these**

- An Effort change produces no restart (same ProcessId) -> the change silently does nothing; there is no live mid-session effort switching, so a restart is the only mechanism there is.
- The Effort selection survives a Model change -> the user is left with a level they never chose for the new model, and the adapter will clamp it silently.
- The Effort resets with no note saying so -> #45 is back. The reset is right; losing a deliberate choice without being told is not.
- A `not in the agent's advertised effort catalog` warning appears after choosing from the picker -> a display label was stored where an ID belongs.
- The effort list does not change at all when the Model changes -> the ladder is being cached per app run instead of per model.
- A slow probe for the ABANDONED model lands on the new choice (switch models twice quickly to test) -> a superseded probe is overwriting the current selection.
- Two options in the Effort select mean "default" -> the picker offers the same thing twice.

**Inconclusive if**

If the Model has no effort ladder at all, the select will show only `Use the agent's default` with the hint `This model offers no effort choice, so it will think as it normally does.` — that is a valid outcome for THAT model; switch to a model that does advertise levels, or record the test as not-applicable for this adapter. If the tile does not restart because the Effort value you picked was the one already stored, re-check step 4.

> [!NOTE]
> The card always shows the STORED Effort, not the session's live value. If the adapter clamps a requested level back to its own default, the card will not say so — that is a recorded limit, not a defect. The restore step is mandatory, for the same reason as PERSONALIFECYCLE-28.

### PERSONALIFECYCLE-30 — MONEY: context bleeds between Rooms — confirm the known limit, and that replies still land in the right Room

**💰 Spends money** · about 12 min

*Confirms the documented one-session-per-Persona limit is what you see, while proving the genuine defect it could mask (a reply landing in the wrong Room) does not happen.*

**Before you start**

- The app is running with `Nova` **Online** and a two-member Room named `Nova`.
- The demo agent `echo` exists (default configuration).
- COST: two one-sentence Haiku Turns.

**Steps**

1. In the sidebar click **New chat**.
2. In the panel tick the checkbox next to `Nova` AND the checkbox next to `echo`.
3. Click **Start chat** and note the new Room that appears in the sidebar; open it.
4. Go back to the two-member Room named `Nova`.
5. In its composer type `My favourite fruit is a quince. Reply with just OK.` and press Enter; wait for the reply.
6. Switch to the group Room from step 3.
7. In its composer type `@Nova what is my favourite fruit?` and press Enter; wait for the reply.
8. Read the reply and note whether it says `quince`.
9. Check WHICH Room the reply appeared in.
10. Go back to the two-member `Nova` Room and confirm no stray reply appeared there.
11. In `T-B` run `Adapters` and count rows for `Nova`.
12. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\rooms'` and confirm each Room has its own `.jsonl` file.

**Pass if — all of these**

- `Nova` very likely answers `quince` in the group Room — recorded and reported as the KNOWN LIMIT, not as a defect.
- The reply appears in the group Room where the question was asked, and NOT in the two-member Room.
- No reply appears in any Room `Nova` is not a member of.
- `Adapters` shows exactly ONE `node.exe` for `Nova` regardless of how many Rooms it is in.
- Each Room has its own `.jsonl` Transcript and each reply is filed in the right one.

**Fail if — any of these**

- The reply appears in a DIFFERENT Room from the one asked in -> a real defect: Room routing is broken, and conversations will cross.
- A reply appears in a Room the Agent is not a Member of -> membership is not gating delivery.
- Two `node.exe` processes appear for one Persona in two Rooms -> the one-session-per-Persona design has been broken and the cost per teammate now scales with Rooms.
- A Message posts into the wrong `.jsonl` file -> the Transcript and the UI disagree about where a Message lives.

**Inconclusive if**

If `Nova` says it does not know the fruit, that is also acceptable — the memory is shared but the model may not use it, and the `[Room: …]` prefix on every prompt is a convention it may follow. Record what happened; only the routing conditions decide pass or fail. If **New chat** shows `No agents have connected yet`, the demo agent is disabled or nothing has connected — enable it or use **Add teammate** in the `Nova` Room header instead.

> [!NOTE]
> DO NOT FILE THE BLEED AS A BUG. One ACP session spans every Room its Agent is in; a session per (Persona, Room) was rejected on cost. In the same family: **Stop** stops that Agent in every Room at once, because there is nothing narrower to stop.

### PERSONALIFECYCLE-31 — MONEY: the per-Persona token Budget shows as Degraded with its reason, and any Human Message clears it

**💰 Spends money** · about 25 min

*Proves a spent token cap is surfaced as a badge with a reason on every surface, rather than an Agent that silently goes quiet, and that a Human speaking resets it.*

**Before you start**

- Two Personas exist (`Nova` and `Ada`), both Model = Haiku, Effort = low.
- No app is running (this test needs specific environment overrides at startup).
- COST: roughly four to eight short Haiku Turns. The `Team__AgentMessageBudget='4'` override is what caps the spend — do not omit it.

**Steps**

1. Open a FRESH `T-A` (so no stale `Team__` variables remain).
2. In `T-A` run `$env:Team__Acp__TokenBudget='1'; $env:Team__AgentMessageBudget='4'; dotnet run --project src\Huddle.App --urls http://localhost:5100`.
3. In the browser open `http://localhost:5100/teammates` and wait until both `Nova` and `Ada` read **Online**.
4. In the sidebar click **New chat**, tick `Nova` and `Ada`, and click **Start chat**.
5. Open the new Room.
6. In the composer type `@Nova please ask @Ada a question, then keep the conversation going.` and press Enter.
7. Watch the Room. As soon as the first agent-to-agent exchange happens, switch to the `/teammates` tab and watch both tiles.
8. When a tile flips to **Degraded**, hover its status line and read the tooltip.
9. Click that tile and read the reason line on the card, word for word.
10. Go back to the group Room and read the area above the composer.
11. In `T-A` find the Warning line about a spent token budget and copy it.
12. Now clear it: in ANY Room that the Degraded Persona is a Member of, type `hello` and press Enter.
13. Watch that Persona's tile for 15 seconds.
14. Read the card's reason line again.
15. When finished, press Ctrl+C in `T-A` and close that window so the overrides do not leak into later tests.

**Pass if — all of these**

- One of the two tiles reaches **Degraded** (NOT Offline) with the reason, on the card, reading exactly: `The per-Persona token Budget of 1 is spent; no more Turns until a Human speaks.`
- The same reason appears in the tile's tooltip and in the Room as a strip reading `<Name> is Degraded: The per-Persona token Budget of 1 is spent; no more Turns until a Human speaks.`
- `T-A`'s console carries the Warning `Persona '<Name>' has spent its token budget of 1 and is taking no more turns until a human speaks to it.`
- After a Human Message in any Room that Persona is a Member of, the tile returns to **Online** and the reason line disappears.
- The exchange also stops at the per-Room Budget with the prompt `Agents have sent 4 replies since you last spoke, and are paused.` plus **Continue** and **Leave paused** buttons — and that pause leaves every member HEALTHY (no Degraded badge from the Room budget).

**Fail if — any of these**

- The Agent goes silent with NO badge and no reason -> a cap that stops an Agent silently is indistinguishable from a broken Agent; this is the entire point of surfacing it.
- The state reads **Offline** rather than Degraded -> the session and the pipe are both fine, so Offline overstates the failure and invites a pointless Restart.
- The counter does not reset when the Human speaks -> the cap is permanent rather than per unattended run, and the teammate is dead until the app restarts.
- The Degraded badge appears for a spent ROOM Budget -> that pause belongs to the Continue prompt and must leave every member healthy; conflating them makes the badge meaningless.
- The reason names a number other than the configured `1` -> the override is not being read; check that you used double underscores.

**Inconclusive if**

IMPORTANT: this Degraded state is UNREACHABLE in a plain two-member Human-to-Persona Room, because a Human Message resets the counter before the Turn is processed. If you tried it that way and saw nothing, you have not found a bug — redo it with two Personas in one Room. If the two agents refuse to talk to each other at all, re-word step 6 as `@Nova ask @Ada what its favourite colour is. @Ada, answer and then ask @Nova a question back.` If neither tile ever goes Degraded but the Room budget pauses first, raise `Team__AgentMessageBudget` to `6` and retry once — no more than once, because each retry spends.

> [!NOTE]
> Never run this test without the `Team__AgentMessageBudget` cap: the per-Room Budget is what stops the exchange from running away.

### PERSONALIFECYCLE-32 — MONEY: a token Budget of zero disables the per-Persona cap, and the per-Room Budget still stops the exchange

**💰 Spends money** · about 20 min

*Proves the disable switch actually disables the token cap and that the Room-level pause remains the backstop.*

**Before you start**

- PERSONALIFECYCLE-31 has been run, so you know what the Degraded token-budget badge looks like.
- Two Personas (`Nova`, `Ada`) at Haiku/low.
- No app is running.
- COST: about four short Haiku Turns, capped by `Team__AgentMessageBudget='4'`.

**Steps**

1. Open a FRESH `T-A`.
2. In `T-A` run `$env:Team__Acp__TokenBudget='0'; $env:Team__AgentMessageBudget='4'; dotnet run --project src\Huddle.App --urls http://localhost:5100`.
3. In the browser wait until `Nova` and `Ada` both read **Online** on `/teammates`.
4. Open (or create with **New chat**) a Room containing both `Nova` and `Ada`.
5. In the composer type `@Nova please ask @Ada a question, then keep the conversation going.` and press Enter.
6. Watch the Room until the exchange stops. Count the agent replies.
7. Read the area above the composer.
8. Switch to `/teammates` and read both tiles' statuses and tooltips.
9. In `T-A` search the whole console for the text `has spent its token budget`.
10. Back in the Room, click **Continue** and confirm the exchange resumes without you typing a Message.
11. Let it pause again, then click **Leave paused** and confirm the prompt is replaced by the quieter paused note.
12. Press Ctrl+C in `T-A` and close that window so the overrides do not leak.

**Pass if — all of these**

- The exchange runs on with NO Degraded badge on either tile at any point.
- It stops at the per-Room Budget: the Room shows `Agents have sent 4 replies since you last spoke, and are paused.` with **Continue** and **Leave paused** buttons.
- Both tiles stay **Online** throughout, with empty tooltips.
- `T-A`'s console contains NO line matching `has spent its token budget`.
- **Continue** resumes the same exchange with no Human Message typed, and it halts again one Budget later.
- **Leave paused** replaces the prompt with the note `Paused — N of M agent replies since you last spoke.`

**Fail if — any of these**

- A Degraded token-budget badge appears while the cap is disabled -> the zero-disables rule is broken and the operator cannot turn the cap off.
- NOTHING stops the exchange -> both caps are gone and an agent-to-agent loop will run until someone notices; this is real, unbounded spend.
- A tile goes Offline during the exchange -> a normal pause is being reported as a fault.
- The pause prompt vanishes on page reload -> the prompt is only reaching the live event path and not the initial render, so a refresh hides the fact that the Room is paused.
- **Continue** does nothing, or the exchange resumes and never halts again -> the granted Budget is not being re-applied.

**Inconclusive if**

If the two agents will not talk to each other, re-word the prompt as in PERSONALIFECYCLE-31's inconclusive note. If the Room pauses after fewer replies than the configured budget, check whether a Turn failed (read `T-A`) before concluding. If you cannot get four replies out of the pair within a few minutes, stop the test rather than spending more — record it inconclusive.

> [!NOTE]
> A Turn a Human stopped is not a failure and a spent per-Room Budget is not either: neither may ever show as Degraded or Offline. The Budget pause belongs to the Continue prompt above the composer, and that is the only place it should appear.

---

Back to [the manual test script](../manual-tests.md).
