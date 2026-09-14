# Model and Effort pickers, catalog probe and runner restart

Prove, in a real browser, that the Model and Effort `<select>` pair on the Teammate card at `/teammates` is filled by a real throwaway adapter probe (not a hardcode), that the chosen values are stored as wire ids in `persona_models` / `persona_efforts`, that changing either one restarts the teammate while changing nothing does not, and that every documented silent failure in this area (a display name stored instead of an id, a superseded probe landing on the wrong model, a stored choice wiped just by opening Edit, a removed teammate's setting resurrecting, a probe running in the repo root) is caught. Almost everything here is free: the probe never starts a prompt turn, and starting a session is not a turn either. Only MODELEFFORT-27 spends money.

**27 tests** · 26 free, 1 paid 💰 · about 3.6 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB` / `O-WIRE`, the four resets,
`P-NEW-PERSONA`, `P-ECHO-BOT` and the standing conventions. This area adds:

1. Confirm the prerequisites: `dotnet --version` prints an SDK, `node --version` prints **v22.5 or newer** (the database helper below uses Node's built-in `node:sqlite`), and `Test-Path 'E:\Repos\Huddle\tools\acp\node_modules\@agentclientprotocol\claude-agent-acp\dist\index.js'` prints True. If False, run `pwsh E:\Repos\Huddle\tools\acp\install.ps1` once and re-check.
2. This area uses three terminals: `T-A` (the app), `T-B` (the Adapter-spawn watcher) and `T-C` (database and file checks).
3. In `T-C`, paste these two helpers once — they stand in for `O-DB` on a machine with no `sqlite3`. Query: `function Get-TeamDb($sql) { node --no-warnings -e "const {DatabaseSync}=require('node:sqlite');const db=new DatabaseSync('E:/Repos/Huddle/src/Huddle.App/App_Data/team.db',{readOnly:true});console.log(JSON.stringify(db.prepare(process.argv[1]).all(),null,1));" $sql }`. Write: `function Set-TeamDb($sql) { node --no-warnings -e "const {DatabaseSync}=require('node:sqlite');const db=new DatabaseSync('E:/Repos/Huddle/src/Huddle.App/App_Data/team.db');db.exec(process.argv[1]);console.log('ok');" $sql }`. Verify with `Get-TeamDb "SELECT name FROM sqlite_master WHERE type='table'"` — it must list `persona_models` and `persona_efforts`.
4. In `T-B`, leave this spawn watcher running. It is `O-ADAPTERS` sampled continuously: `while ($true) { '{0}  {1}' -f (Get-Date -Format HH:mm:ss.fff), (Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like '*claude-agent-acp*' } | Measure-Object).Count; Start-Sleep -Milliseconds 200 }`. Its resting value must be 0 before any test begins.
5. **FREE LANE** (MODELEFFORT-01 to -21) is `P-LAUNCH-FREE`. The catalog probe deliberately still spawns an Adapter in this lane — that is documented behaviour, not the money switch leaking. No teammate runner starts, so `T-B`'s resting count stays 0, which is what makes spawn counting readable.
6. **RESTART LANE** (MODELEFFORT-22 to -27): `Remove-Item Env:Team__Acp__Enabled -ErrorAction SilentlyContinue` then the same `dotnet run` line. Teammate runners now start, so `T-B`'s resting count equals the number of Online teammates. Starting a session is not a prompt turn, so this lane still spends nothing as long as you never type a Message into a Room.
7. "Restart the app" always means `P-STOP` then the `dotnet run` line for the lane the test names. Both catalog caches are per app run, so several tests below are only valid on the FIRST card open after a restart — each says so in its preconditions.
8. Run MODELEFFORT-02 before any test mentioning LADDER-MODEL or NO-LADDER-MODEL. Whether Haiku advertises an effort ladder is a runtime fact of the installed Adapter, not a defect either way, and MODELEFFORT-02 is the walk that records it.

## Tests

### MODELEFFORT-01 — First card open: the Model list comes from the adapter, and one adapter spawn fills both lists

**Free** · about 10 min

*Proves the Model select is populated by a real probe of the installed adapter rather than a hardcoded list, and that one card open costs exactly one adapter process - never two, and never one per select.*

**Before you start**

- Free lane. The app has just been restarted, so no card has been opened in this app run.
- `T-B` is running and resting at 0.
- The Adapter is installed (setup step 1).

**Steps**

1. In `T-C` run `Remove-Item -Recurse -Force 'E:\Repos\Huddle\src\Huddle.App\App_Data\work' -ErrorAction SilentlyContinue` and then `Test-Path 'E:\Repos\Huddle\src\Huddle.App\App_Data\work'` - it must print False.
2. In `T-A`, note the last line currently in the app log so you can tell new lines from old.
3. Open a browser at `http://localhost:5100/teammates`. Confirm the page heading reads **Teammates** and the sidebar link reads **Teammates**.
4. Click the **New teammate** button in the page header (top right of the heading row).
5. Immediately look at `T-B` and keep watching for 30 seconds. Write down the highest number it ever shows and how many separate times it rises from 0 and falls back to 0.
6. In the card titled **New teammate**, read the hint directly under the **Model** select. Immediately after the click it should read `Reading the models this agent offers…`; wait until it stops.
7. Open the **Model** select and write down, in order, every option's visible text. Also note the first option's text exactly.
8. Read the hint under the **Model** select again now that the list has arrived.
9. Open the **Effort** select and confirm it is populated too (it contains at least the blank first option, and the hint under it is no longer `Reading the effort levels this model offers…`).
10. In `T-C` run `Test-Path 'E:\Repos\Huddle\src\Huddle.App\App_Data\work'`.
11. In `T-A`, read every new log line since step 2.
12. Click **Cancel** at the bottom of the card. Then click **New teammate** again and watch `T-B` for 15 seconds.
13. Confirm both selects are populated on this second open, and note how quickly.

**Pass if — all of these**

- The first option of the **Model** select reads exactly `Use the agent's default`.
- Below it the select lists at least one real model entry, and the entries read as human labels (for example `Sonnet 4.5`, `Haiku 4.5`) rather than raw ids of the shape `claude-sonnet-4-5`.
- During the first card open `T-B` rises to exactly 1 and returns to 0 - one spawn, not two, and not two spawns in sequence.
- After the list arrives the Model hint reads exactly `Which model this teammate thinks with. Changing it restarts the teammate, which clears what it remembers.`
- `App_Data\work` exists again after the card open (step 10 prints True).
- The app log contains NO line reading `No ACP adapter is installed; the model catalog is empty.` and no line beginning `Model catalog probe `.
- On the second card open `T-B` stays at 0 for the whole 15 seconds, and both selects are populated immediately.

**Fail if — any of these**

- Both selects populate but `T-B` shows a peak of 2 -> the model probe and the effort probe are no longer sharing the single semaphore, and every card open costs two adapter processes.
- `T-B` rises to 1, falls to 0, then rises to 1 again during ONE card open -> the model probe stopped seeding the default-model effort list from its own session; functionally invisible, exactly twice the cost.
- `T-B` rises above 0 on the SECOND card open -> the successful catalog is no longer cached for the app run, so browsing the card now costs a process every time.
- The Model select contains only `Use the agent's default` while the log shows no `Model catalog probe ...` warning and no `No ACP adapter is installed` line -> the probe succeeded and returned nothing: suspect a grouped `options` catalog being read only on the flat branch, which silently reads as 'this agent advertises none'.
- Every model entry reads as a raw id (`claude-haiku-4-5`) instead of a label (`Haiku 4.5`) -> the label is being read from the wrong field (`displayName` instead of `name`), which silently degrades every label.
- `App_Data\work` does not exist after the card open -> no probe ran at all, so whatever is in the select is not coming from the adapter.

**Inconclusive if**

If the Model select is empty AND the log carries one of `No ACP adapter is installed; the model catalog is empty.`, `Model catalog probe failed to start or talk to the adapter.`, `Model catalog probe skipped: the adapter needs authentication.` or `Model catalog probe timed out after 00:00:20.`, this test is INCONCLUSIVE, not a fail: the environment, not the code, is the cause. Fix the named cause (re-run install.ps1, authenticate the Claude CLI, check `node` on PATH) and re-run from a restarted app. If `T-B` never leaves 0 but `App_Data\work` was recreated and the list is populated, the 200 ms poll simply missed a short spawn - record INCONCLUSIVE on the count half and re-run that half only.

> [!NOTE]
> This is the one test that must be the first card open of its app run: a successful catalog is cached for the whole run, so a second attempt without a restart will show zero spawns and prove nothing about the first half.

### MODELEFFORT-02 — Fixture walk: record every model's effort ladder, and one spawn per newly seen model

**Free** · about 10 min

*Produces the fixture table every later test refers to - which model advertises an effort ladder and which does not - and proves the effort catalog is probed per model rather than read once.*

**Before you start**

- Free lane, app running. MODELEFFORT-01 has been run in this app run (or the card has been opened at least once, so the model list is cached).
- `T-B` running.

**Steps**

1. On `http://localhost:5100/teammates`, click **New teammate**.
2. Prepare a table with four columns: model label, model id, effort options offered, effort hint text.
3. In the **Model** select, choose the first real model (the first entry below `Use the agent's default`). Wait until the Effort hint stops reading `Reading the effort levels this model offers…`.
4. Write down, for that model: every option in the **Effort** select in order, and the exact hint text under the Effort select.
5. Note whether `T-B` rose to 1 and back to 0 during that model change.
6. Repeat steps 3-5 for every remaining entry in the **Model** select, one at a time, waiting for each to settle before moving on.
7. Now re-select a model you have already walked. Watch `T-B` for 10 seconds.
8. Mark in your table which model you will call HAIKU (the option whose label contains `Haiku`) and which SONNET (the option whose label contains `Sonnet`).
9. Mark one model as LADDER-MODEL: a model whose Effort select offers real levels including both `Low` and `Medium`. Mark one as NO-LADDER-MODEL: a model whose Effort hint reads `This model offers no effort choice, so it will think as it normally does.` If no model offers a ladder, or none lacks one, say so explicitly in your notes.
10. Click **Cancel**.

**Pass if — all of these**

- Every model in the list yields either a set of real effort levels or the hint `This model offers no effort choice, so it will think as it normally does.` - no model leaves the hint stuck on `Reading the effort levels this model offers…`.
- `T-B` rises to exactly 1 and returns to 0 once per model id you had not selected before in this app run.
- `T-B` stays at 0 when you re-select a model already walked (step 7).
- Where an Effort select is populated, its first option reads `Use the agent's default` and the remaining options are real levels (`Low`, `Medium`, and possibly `High`, `Xhigh`, `Max`).
- No Effort select anywhere contains an option labelled `Default`.
- The table is complete: HAIKU, SONNET, LADDER-MODEL and NO-LADDER-MODEL are each named, or explicitly recorded as absent.

**Fail if — any of these**

- An Effort select contains an option labelled `Default` -> the adapter's own `default` sentinel is no longer filtered out; two options now carry one meaning, and the literal string `default` can reach `persona_efforts`, where it is sent on the wire as a real named selection that CLEARS the adapter's flag layer - a different behaviour wearing the same label.
- Re-selecting an already-walked model spawns an adapter again (step 7 shows a rise) -> the per-model effort cache is not being written, so every model switch costs a process forever.
- Two different models produce byte-identical effort option lists AND `T-B` never rose between them -> the effort catalog is not actually per-model; the first model's ladder is being reused.
- An Effort hint stays on `Reading the effort levels this model offers…` for more than 25 seconds -> a probe is hanging past its own 20-second timeout.

**Inconclusive if**

If every model in the list advertises an effort ladder, NO-LADDER-MODEL does not exist in this environment: mark MODELEFFORT-10 as INCONCLUSIVE - not failed - and note the adapter version. If NO model advertises a ladder, mark MODELEFFORT-07, MODELEFFORT-23 and MODELEFFORT-27's effort half INCONCLUSIVE the same way. If Haiku turns out to be NO-LADDER-MODEL, that is expected and not a defect: run every low/medium test on SONNET instead and say so in the result.

> [!NOTE]
> This test is a recorder as much as a check. Every later test that names LADDER-MODEL or NO-LADDER-MODEL reads its answer from this table, so do not skip it. Opus is excluded from the walk by the Model and Effort convention in section 0.3.

### MODELEFFORT-03 — Saving a new teammate stores wire ids, never display names

**Free** · about 8 min

*Catches the highest-frequency silent failure in the area: a display name stored in the database never resolves against the adapter and logs a warning on every restart forever, while the session quietly runs on the default.*

**Before you start**

- Free lane, app running.
- MODELEFFORT-02 has been run, so LADDER-MODEL is known.

**Steps**

1. In `T-C` run `Get-TeamDb "SELECT * FROM persona_models"` and `Get-TeamDb "SELECT * FROM persona_efforts"` and note what is already there.
2. On `/teammates`, click **New teammate**.
3. In **Name** type `Probe One`.
4. In **Title** type `Tester`.
5. In **Alias** type `p1`.
6. Leave **Teams** empty.
7. In the **Persona body** textarea type `You are a tester.`
8. In the **Model** select choose HAIKU (or LADDER-MODEL if Haiku is not in the list). Wait for the Effort select to settle.
9. In the **Effort** select choose `Low`. If HAIKU offers no ladder, first switch the Model to LADDER-MODEL, wait, then choose `Low`.
10. Click **Add teammate**.
11. Read the card that is now showing: its heading, its **Model** section and its **Effort** section.
12. In `T-C` run `Get-TeamDb "SELECT persona_name, model FROM persona_models"` and `Get-TeamDb "SELECT persona_name, effort FROM persona_efforts"`.
13. In `T-C` run `Get-Content -Raw 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Probe One.md'` and then `[bool]((Get-Content -Raw 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Probe One.md') -match "\r\n")`.

**Pass if — all of these**

- The card does not close on save: it shows the saved teammate, with a **Persona** section, a **Model** section and an **Effort** section.
- `persona_models` has exactly one row with `persona_name` = `Probe One` and `model` a lowercase hyphenated wire id (for example `claude-haiku-4-5`), NOT the label shown in the select.
- `persona_efforts` has exactly one row with `persona_name` = `Probe One` and `effort` exactly `low` (lowercase), NOT `Low`.
- Neither stored value is an empty string.
- `App_Data\Teams\Probe One.md` exists and begins with a `---` line followed by `name: 'Probe One'`, `title: 'Tester'`, `alias: 'p1'`, a closing `---`, then `You are a tester.`
- The CRLF check in step 13 prints **False** (the file is LF-only).

**Fail if — any of these**

- `persona_models.model` holds a display label such as `Haiku 4.5`, or `persona_efforts.effort` holds `Low` -> a display name is being stored instead of an id; it will never resolve against the adapter's option value, will log a catalog warning on every single restart forever, and the session will silently run on the default.
- Either column holds an empty string -> a row that exists but never resolves; the blank-means-default path has been bypassed.
- No row appears in one or both tables even though a non-blank choice was made -> the choice is being dropped on save and the teammate silently runs on the default.
- The file contains CRLF line endings (step 13 prints True) -> the composed frontmatter is no longer joined with LF, which will make MODELEFFORT-24's no-op save restart the teammate spuriously.
- A `teams:` line appears in the file even though Teams was left empty -> an empty optional field is being written where it should be omitted.

**Inconclusive if**

If **Add teammate** shows a red error line above the fields (for example a name collision), the save never happened - delete or rename the colliding Persona file under `App_Data\Teams` and re-run. If the database helper errors with `Cannot find module 'node:sqlite'`, the installed Node is too old: use any SQLite GUI against `App_Data\team.db` instead and record the same queries.

> [!NOTE]
> `Probe One` is the fixture for MODELEFFORT-11, MODELEFFORT-12, MODELEFFORT-15 and the restart-lane tests. Do not delete it until those are done.

### MODELEFFORT-04 — Two different 'default' choices in the Model select, and none in the Effort select

**Free** · about 10 min

*Proves the blank option stores nothing at all while the adapter's own `default` model entry stores the literal string, because those are genuinely different wire behaviours - and that the effort sentinel is filtered out so the same ambiguity cannot exist there.*

**Before you start**

- Free lane, app running.
- MODELEFFORT-02 has been run; you know whether the Model select contains an entry that is the adapter's own default (its label or description names the model it resolves to).

**Steps**

1. On `/teammates`, click **New teammate**.
2. Open the **Model** select and confirm the blank first option reads exactly `Use the agent's default`.
3. Look for a SECOND default-ish entry among the real model entries - the adapter's own `default` model. If the list has none, stop and record this test as partially inconclusive (see below), then jump to step 10.
4. Select that adapter `default` entry.
5. Type **Name** `Probe Default`, **Title** `Tester`, **Alias** `pd`, leave **Teams** empty, and in **Persona body** type `You are a tester.`
6. Leave the **Effort** select on `Use the agent's default`.
7. Click **Add teammate**.
8. In `T-C` run `Get-TeamDb "SELECT persona_name, model FROM persona_models WHERE persona_name='Probe Default'"`.
9. In `T-C` run `Get-TeamDb "SELECT * FROM persona_efforts WHERE persona_name='Probe Default'"`.
10. Close the card (the × button, whose tooltip reads `Close`). Click **New teammate** again.
11. Type **Name** `Probe Blank`, **Title** `Tester`, **Alias** `pb`, **Persona body** `You are a tester.` and leave BOTH selects on `Use the agent's default`.
12. Click **Add teammate**.
13. In `T-C` run `Get-TeamDb "SELECT * FROM persona_models WHERE persona_name='Probe Blank'"` and `Get-TeamDb "SELECT * FROM persona_efforts WHERE persona_name='Probe Blank'"`.
14. Read the **Model** and **Effort** sections of the card now showing for `Probe Blank`.
15. Clean up: open each of `Probe Default` and `Probe Blank` from its tile, click **Remove**, then click **Confirm**.

**Pass if — all of these**

- `Probe Default` has exactly one row in `persona_models` whose `model` is exactly the literal string `default`.
- `Probe Default` has ZERO rows in `persona_efforts`.
- `Probe Blank` has ZERO rows in `persona_models` and ZERO rows in `persona_efforts`.
- `Probe Blank`'s View card **Model** section reads exactly `Agent default` and its **Effort** section reads exactly `Model default`.
- No Effort select seen during this test offered an option labelled `Default`.

**Fail if — any of these**

- `Probe Blank` has a row in either table with an empty-string value -> the blank option is writing a row that exists but never resolves; every restart of that teammate will log a catalog warning forever.
- `Probe Default` has no row in `persona_models`, or its row holds something other than `default` -> the adapter's own default entry is being folded into the blank option, hiding a genuinely different wire behaviour (sending `set_config_option` versus sending nothing) behind one label.
- The Effort select offers an option labelled `Default` -> the sentinel filter is gone; the literal string `default` can now be stored and sent as a real named selection, which explicitly clears the adapter's flag layer.
- `Probe Blank`'s card shows a model name rather than `Agent default` -> the file-to-database join is returning a value for a Persona that has none.

**Inconclusive if**

If the installed adapter's model catalog contains no entry of its own named `default`, steps 3-9 cannot be run: record them INCONCLUSIVE with the adapter version, and report only the `Probe Blank` half plus the Effort-sentinel check. Do not fabricate the missing half.

> [!NOTE]
> The wording difference between the Effort select's blank option (`Use the agent's default`) and the View card's Effort section (`Model default`) is a known copy inconsistency - report it as an observation if you notice it, not as a defect.

### MODELEFFORT-05 — No card field shows a literal parameter name (the missing-@ binding trap)

**Free** · about 3 min

*Catches a defect class that compiles clean, no automated page or component test can see, and has shipped once: a string parameter bound without a leading @ renders the field name as literal text.*

**Before you start**

- Free lane, app running.
- At least one teammate exists (`Probe One` from MODELEFFORT-03).

**Steps**

1. On `/teammates`, click the tile for `Probe One` to open its card in View mode.
2. Read the card heading, the name line, the `Alias:` line, the **Persona** section and the **Model** and **Effort** sections.
3. Click **Edit**.
4. Read the **Persona text** textarea, the **Model** select's selected option and the **Effort** select's selected option.
5. Use the browser's find-on-page (Ctrl+F) and search the page for the text `this.card`.
6. Click **Cancel**.

**Pass if — all of these**

- Nothing anywhere on the card renders text of the form `this.cardName`, `this.cardModel`, `this.cardEffort`, `this.cardTitle`, `this.cardAlias`, `this.cardTeams`, `this.cardText`, `this.cardRoomId`, `this.cardFilePath` or `this.cardError`.
- Ctrl+F for `this.card` finds zero matches.
- The heading shows the Persona's real name (`Probe One` in View, `Edit Probe One` in Edit mode), the Alias line shows `p1`, and the Persona text is the real file text.

**Fail if — any of these**

- Any field renders a literal like `this.cardName` -> the corresponding `[Parameter]` lost its leading `@` in `Teammates.razor` and is being passed as a plain string literal; it compiles clean and warning-free and only misbehaves here, which is exactly why a human at the browser is the second line of defence.

**Inconclusive if**

If the card will not open at all, this test cannot run - report the card-open failure as its own defect and mark this INCONCLUSIVE.

> [!NOTE]
> Takes two minutes and is the cheapest test in the set; run it any time a card is already open.

### MODELEFFORT-06 — Neither catalog is probed on a plain page load or a tile click

**Free** · about 6 min

*Proves browsing to /teammates and opening a teammate for viewing never costs a real adapter process - only Edit and New teammate may spawn one.*

**Before you start**

- Free lane.
- At least one teammate exists (`Probe One`).
- `T-B` running and resting at 0.

**Steps**

1. Restart the app (Ctrl+C in `T-A`, then the free-lane `dotnet run` line).
2. In `T-C` run `Remove-Item -Recurse -Force 'E:\Repos\Huddle\src\Huddle.App\App_Data\work' -ErrorAction SilentlyContinue`, then `Test-Path 'E:\Repos\Huddle\src\Huddle.App\App_Data\work'` - it must print False.
3. In the browser, navigate to `http://localhost:5100/teammates` and let the page finish rendering (heading, the teammate tiles, and the **Team** filter select if more than one team exists).
4. Do not click anything. Watch `T-B` for 20 seconds.
5. In `T-C` run `Test-Path 'E:\Repos\Huddle\src\Huddle.App\App_Data\work'`.
6. Now click the tile for `Probe One` (the whole tile is the button). The card opens in View mode.
7. Watch `T-B` for another 20 seconds.
8. In `T-C` run `Test-Path 'E:\Repos\Huddle\src\Huddle.App\App_Data\work'` again.
9. If more than one team exists, change the **Team** filter select to a specific team and watch `T-B` for 10 seconds.
10. Click **Edit** on the open card and watch `T-B`.

**Pass if — all of these**

- `T-B` stays at 0 for the whole page load (step 4) and `App_Data\work` still does not exist (step 5 prints False).
- `T-B` stays at 0 for the whole tile-click card open (step 7) and `App_Data\work` still does not exist (step 8 prints False).
- Changing the **Team** filter spawns nothing (step 9 stays at 0).
- Clicking **Edit** DOES spawn: `T-B` rises to 1 and `App_Data\work` is created.

**Fail if — any of these**

- `T-B` rises, or `App_Data\work` appears, on the plain page load -> browsing to /teammates now launches a real process every time; silent, the page looks identical, it is just slower and dirtier.
- `T-B` rises, or `App_Data\work` appears, on the tile click -> the tile-open path stopped being synchronous and now probes; same silent cost on every teammate you merely look at.
- Changing the **Team** filter spawns a process -> display narrowing is reaching the probe.
- Clicking **Edit** spawns nothing AND the Model select is empty -> the probe is no longer reachable at all from the Edit path.

**Inconclusive if**

If the app was not restarted first, the catalogs may already be cached and the Edit half will show zero spawns for a legitimate reason - restart and re-run. If `App_Data\work` cannot be deleted because a file is locked, stop the app first, delete, then restart.

> [!NOTE]
> The two automated counterparts of the page-load half exist in the suite; the tile-click half and the Edit half are browser-only.

### MODELEFFORT-07 — Changing the Model re-probes: the Effort list changes and the chosen level resets

**Free** · about 8 min

*The highest-value test in the area. Proves the effort ladder belongs to the model that is actually selected, and that a level chosen for one model is never carried over to another - the adapter silently clamps an unsupported level back to default with no error anywhere.*

**Before you start**

- Free lane, app running.
- MODELEFFORT-02 has been run and recorded HAIKU and SONNET, and recorded each one's effort ladder.
- `T-B` running.

**Steps**

1. On `/teammates`, click **New teammate**.
2. In the **Model** select choose HAIKU. Wait until the Effort hint stops reading `Reading the effort levels this model offers…`.
3. Write down every option in the **Effort** select, in order, and the exact Effort hint text.
4. If HAIKU offers a ladder, select `Low` in the **Effort** select. Confirm the select now shows `Low`.
5. Now change the **Model** select to SONNET.
6. IMMEDIATELY - within the first second - read three things and write them down: what the **Effort** select shows as selected, what the Effort hint reads, and whether the Effort select is still showing HAIKU's option list.
7. Wait until the Effort hint settles. Write down every option in the **Effort** select now, in order, and the hint text.
8. Check `T-B`: note how many times it rose to 1 and fell back to 0 across steps 2 and 5.
9. Change the **Model** select back to HAIKU, wait for it to settle, and compare the Effort list with what you wrote in step 3.
10. Check `T-B` again for the switch in step 9.
11. Click **Cancel**.

**Pass if — all of these**

- Immediately after the model change, the **Effort** select shows `Use the agent's default` as selected - the `Low` chosen for HAIKU is cleared, not carried over.
- During the re-probe the Effort hint reads exactly `Reading the effort levels this model offers…`.
- When the new list arrives it is SONNET's ladder and it DIFFERS from HAIKU's - a different set of entries, or one model showing `This model offers no effort choice, so it will think as it normally does.` where the other showed levels.
- `T-B` shows exactly one spawn per model id not yet probed in this app run, and zero spawns when switching back to a model already probed (step 9).
- Switching back to HAIKU restores exactly the list recorded in step 3.

**Fail if — any of these**

- The **Effort** select still shows `Low` after the model change -> a level chosen for HAIKU will be sent for SONNET; the adapter will silently clamp it back to default with no error, and the user's visible choice becomes a lie.
- The Effort list is byte-identical for HAIKU and SONNET and `T-B` showed no second spawn -> the per-model probe is not re-running; the first model's ladder is being shown for every model.
- The Effort hint never shows `Reading the effort levels this model offers…` during the switch -> the loading state is not being rendered, which means the select is showing a stale list as if it were current.
- Switching back to an already-probed model spawns another adapter -> the per-model cache is broken (cost only, but it also means this test's 'zero spawns' oracle is gone).

**Inconclusive if**

If MODELEFFORT-02 recorded that HAIKU and SONNET advertise the SAME ladder, the 'must differ' condition cannot be judged: record it INCONCLUSIVE and judge only the reset-to-default half and the spawn counts. If neither model advertises a ladder, the whole test is INCONCLUSIVE - say so with the adapter version rather than passing it vacuously.

> [!NOTE]
> This is manual checklist step 15 and the suite structurally cannot reach it: the change handler only runs off a real `<select>` change event in a live circuit.

### MODELEFFORT-08 — A superseded effort probe never lands on the model the user ended up with

**Free** · about 6 min

*Catches a silent, intermittent defect: an abandoned model's answer arriving late and painting its ladder onto the model actually selected, letting the user save an effort the chosen model does not support.*

**Before you start**

- Free lane, app running.
- MODELEFFORT-02 and MODELEFFORT-07 have been run, so HAIKU's and SONNET's ladders are recorded and known to differ.

**Steps**

1. Restart the app (so per-model effort caches are cold), then open `http://localhost:5100/teammates`.
2. Click **New teammate**. Wait for the Model list to arrive.
3. Change the **Model** select to HAIKU, then about one second later to SONNET, then about one second later back to HAIKU - each change made before the previous Effort hint has stopped reading `Reading the effort levels this model offers…`.
4. Stop touching the card. Wait 30 seconds for everything to settle.
5. Write down every option now in the **Effort** select and the Effort hint text.
6. Compare that list against HAIKU's ladder as recorded in MODELEFFORT-07 step 3.
7. Click **Cancel**, restart the app again, click **New teammate**, and repeat steps 3-5 but with SONNET as the LAST selection.
8. Compare that settled list against SONNET's recorded ladder.

**Pass if — all of these**

- After the HAIKU-last run, the settled Effort list matches HAIKU's ladder exactly, and the Model select still shows HAIKU.
- After the SONNET-last run, the settled Effort list matches SONNET's ladder exactly, and the Model select still shows SONNET.
- In both runs the Effort hint has stopped reading `Reading the effort levels this model offers…` by the end of the 30-second wait.

**Fail if — any of these**

- The settled Effort list belongs to a model that is not the one selected -> a superseded probe landed; the user is looking at a ladder for a model they are not saving and can pick a level the chosen model does not support, which the adapter will later clamp back to default, also silently.
- The Effort hint is still stuck on `Reading the effort levels this model offers…` after 30 seconds -> the loading flag is being cleared by the wrong probe generation, or not at all.
- The Model select shows a different model than the last one you clicked -> the model change handler is racing itself.

**Inconclusive if**

If the probes complete faster than you can click (the hint never shows `Reading the effort levels this model offers…` between clicks), the overlap this test needs never happened - record INCONCLUSIVE and retry with faster switching, or accept that this environment's adapter starts too quickly to force the race in a browser.

> [!NOTE]
> There is no log or filesystem oracle for this one - the only evidence is the on-screen list compared against a slow, deliberate selection of the same model.

### MODELEFFORT-09 — Closing the card mid-probe leaves no stuck state

**Free** · about 5 min

*Proves an in-flight probe whose card has closed cannot paint state nobody is looking at, and cannot leave the next card stuck on a loading hint or showing the previous card's list.*

**Before you start**

- Free lane.
- MODELEFFORT-02 has been run, so you know a model whose ladder is not yet probed in a fresh app run.

**Steps**

1. Restart the app, then open `http://localhost:5100/teammates`.
2. Click **New teammate** and wait for the Model list to arrive.
3. Change the **Model** select to SONNET.
4. While the Effort hint still reads `Reading the effort levels this model offers…`, click **Cancel**.
5. Confirm the card has closed and the page behind it is normal.
6. Wait 30 seconds, watching the page for any flicker or repaint.
7. Click **New teammate** again.
8. Read: the **Model** select's populated state and selected option, the **Effort** select's selected option, and the Effort hint text.
9. Repeat steps 2-8 twice more, once closing with the × button (tooltip `Close`) and once by clicking the dark backdrop outside the card.

**Pass if — all of these**

- The card closes immediately on each of the three close gestures.
- Nothing repaints or flickers on the page during the 30-second wait after the close.
- On re-open, the **Model** select is populated instantly from cache and shows `Use the agent's default` selected.
- On re-open, the **Effort** select shows `Use the agent's default` selected.
- On re-open, the Effort hint is NOT `Reading the effort levels this model offers…` - it is either the normal hint or `This model offers no effort choice, so it will think as it normally does.`

**Fail if — any of these**

- The re-opened card's Effort hint is stuck on `Reading the effort levels this model offers…` -> the loading flag was left set by a probe belonging to a closed card; every subsequent card open looks permanently busy.
- The re-opened Create card's Effort select shows SONNET's ladder from the previous card -> state survived a close that is supposed to clear everything.
- The page visibly repaints after the card is closed -> a superseded probe is still writing into component state.
- The re-opened Model select is empty and a fresh spawn occurs -> closing a card mid-probe destroyed the cached model catalog.

**Inconclusive if**

If the probe completes before you can click Cancel (the hint never lingers), the mid-flight close never happened - record INCONCLUSIVE and try a model that has not been probed yet in this app run, since a cached one answers instantly.

> [!NOTE]
> UI-only; there is no log line for a discarded probe.

### MODELEFFORT-10 — A model with no effort support says so, and the log proves it was not a failed probe

**Free** · about 7 min

*Proves 'this model offers no effort choice' is a real answer rather than the identical sentence a broken probe also produces - the log is the only thing that tells the two apart.*

**Before you start**

- Free lane, app running.
- MODELEFFORT-02 recorded a NO-LADDER-MODEL. If it did not, see INCONCLUSIVE.

**Steps**

1. In `T-A`, note the last log line so you can separate new lines from old.
2. On `/teammates`, click **New teammate**.
3. In the **Model** select choose NO-LADDER-MODEL. Wait until the Effort hint stops reading `Reading the effort levels this model offers…`.
4. Open the **Effort** select and write down every option it contains.
5. Write down the exact Effort hint text.
6. In `T-A`, read every new log line since step 1.
7. Type **Name** `Probe NoEffort`, **Title** `Tester`, **Alias** `pne`, **Persona body** `You are a tester.`
8. Click **Add teammate**.
9. In `T-C` run `Get-TeamDb "SELECT * FROM persona_efforts WHERE persona_name='Probe NoEffort'"`.
10. Read the **Effort** section of the card now showing.
11. Clean up: click **Remove** on that card, then **Confirm**.

**Pass if — all of these**

- The **Effort** select contains ONLY the option `Use the agent's default` - no other entries.
- The Effort hint reads exactly `This model offers no effort choice, so it will think as it normally does.`
- The app log contains NONE of: `Model catalog probe failed to start or talk to the adapter.`, `Model catalog probe timed out after 00:00:20.`, `Model catalog probe skipped: the adapter needs authentication.`, `Model catalog probe failed talking to the adapter process.`, `No ACP adapter is installed; the model catalog is empty.`
- `persona_efforts` has ZERO rows for `Probe NoEffort`.
- The saved card's **Effort** section reads exactly `Model default`.

**Fail if — any of these**

- The picker says the model offers no effort choice AND the log carries one of the four probe-failure lines -> the sentence is a lie produced by a broken probe, not a real answer about the model; the picker is the ambiguous surface and the log is the disambiguator.
- A row appears in `persona_efforts` for `Probe NoEffort` -> something is being stored for a model that advertises no levels; whatever it is will never resolve and will warn on every restart.
- The Effort select offers real levels for a model MODELEFFORT-02 recorded as offering none -> the two walks disagree; re-run MODELEFFORT-02 before filing anything.

**Inconclusive if**

If MODELEFFORT-02 found no NO-LADDER-MODEL in this adapter's catalog, this test cannot be run against a genuine fixture: record INCONCLUSIVE with the adapter version. Do not substitute a broken adapter to produce the sentence - that tests the opposite thing.

> [!NOTE]
> This is manual checklist step 16. An absent effort entry is deliberately NOT the same as the model catalog's 'unknown': for models, empty means 'the agent never told us'; for effort, empty means 'this model offers no effort choice', which is a real answer.

### MODELEFFORT-11 — Edit preselects the stored Model and Effort, and keeps them when the catalog lands late

**Free** · about 7 min

*Catches the highest-cost silent failure on the card: if the select falls back to its first option when the real catalog replaces the option list, the next Save writes null and wipes the user's stored choice without a word.*

**Before you start**

- Free lane.
- `Probe One` exists with a stored Model and a stored Effort (from MODELEFFORT-03).
- Both caches must be cold, so the app must be restarted immediately before this test.

**Steps**

1. In `T-C` record the current values: `Get-TeamDb "SELECT * FROM persona_models WHERE persona_name='Probe One'"` and `Get-TeamDb "SELECT * FROM persona_efforts WHERE persona_name='Probe One'"`.
2. Restart the app.
3. Open `http://localhost:5100/teammates` and click the `Probe One` tile.
4. Click **Edit**, and watch the **Model** select from the instant the card paints.
5. While the Model hint still reads `Reading the models this agent offers…`, write down what the **Model** select shows as selected.
6. Do the same for the **Effort** select while its hint reads `Reading the effort levels this model offers…`.
7. Wait until both hints have settled and the real option lists have arrived.
8. Write down what each select shows as selected NOW.
9. Without touching any field, click **Save**.
10. In `T-C` re-run both queries from step 1 and compare.

**Pass if — all of these**

- While the Model hint reads `Reading the models this agent offers…`, the **Model** select already shows the stored value selected (rendered as the raw id, since no label is known yet) - not `Use the agent's default`.
- After the catalog arrives and the option list is replaced, the **Model** select STILL shows the stored model (now with its friendly label).
- The same holds for the **Effort** select: the stored level is shown before and after the real ladder arrives.
- After the no-op Save, `persona_models` and `persona_efforts` hold exactly the same values as in step 1.

**Fail if — any of these**

- Either select snaps back to `Use the agent's default` when the real catalog replaces the option list -> merely opening Edit and pressing Save now destroys the user's stored choice with no message anywhere; this is the worst silent failure on this card.
- The stored value is absent from the select during the loading phase (it shows the blank option) -> the synthesised entry for a stored-but-not-yet-known value is gone, and any Save made before the catalog lands writes null.
- After the no-op Save, either database row is missing or changed -> the save path is writing the select's fallback rather than the stored value.

**Inconclusive if**

If the catalogs answer so fast that the loading hints never appear, the 'late catalog' half cannot be observed: record it INCONCLUSIVE and judge only the after-settle selection plus the unchanged database rows. If `Probe One` has no stored Model or Effort, re-run MODELEFFORT-03 first.

> [!NOTE]
> The app must be restarted immediately before this test - a warm cache makes the catalog land instantly and the whole point of the test disappears.

### MODELEFFORT-12 — A stored value the adapter no longer advertises is still offered and still saved

**Free** · about 8 min

*Proves a stale stored choice survives being looked at: the select must offer it, and Save must not silently reset the teammate to the agent default.*

**Before you start**

- Free lane.
- `Probe One` exists.
- The app must be STOPPED while the database is edited.

**Steps**

1. Stop the app (Ctrl+C in `T-A`) and wait for the prompt to return.
2. In `T-C` run `Set-TeamDb "UPDATE persona_models SET model='claude-nonexistent-9' WHERE persona_name='Probe One';"`.
3. In `T-C` run `Set-TeamDb "INSERT INTO persona_efforts(persona_name, effort) VALUES('Probe One','vintage') ON CONFLICT(persona_name) DO UPDATE SET effort='vintage';"`.
4. In `T-C` verify with `Get-TeamDb "SELECT * FROM persona_models WHERE persona_name='Probe One'"` and `Get-TeamDb "SELECT * FROM persona_efforts WHERE persona_name='Probe One'"`.
5. Start the app in the free lane and open `http://localhost:5100/teammates`.
6. Click the `Probe One` tile, then click **Edit**. Wait for both selects to settle.
7. Open the **Model** select and write down every option, noting which is selected.
8. Open the **Effort** select and write down every option, noting which is selected.
9. Click **Save** without changing anything.
10. In `T-C` re-run both queries from step 4.

**Pass if — all of these**

- The **Model** select shows `claude-nonexistent-9` as its own selected option, listed alongside the real catalog entries.
- The **Effort** select shows `vintage` as its own selected option, listed alongside the real levels.
- After Save, `persona_models.model` is still exactly `claude-nonexistent-9` and `persona_efforts.effort` is still exactly `vintage`.
- The card returns to View mode and its **Model** / **Effort** sections show `claude-nonexistent-9` and `vintage`.

**Fail if — any of these**

- Either select omits the stored value and shows `Use the agent's default` selected instead -> opening Edit and saving destroys the user's stored choice merely by looking at the card.
- After Save, either row is gone or reset -> the same destruction, confirmed in the database.
- The card refuses to open or throws an error for an unknown stored value -> an unrecognised stored value is being treated as fatal rather than as something to keep and warn about later.

**Inconclusive if**

If `Set-TeamDb` errors with 'database is locked', the app was not fully stopped - stop it and retry. If the tables do not exist yet, create a teammate through the UI first (MODELEFFORT-03).

> [!NOTE]
> Leave `Probe One` in this stale state if you are going straight to MODELEFFORT-25, which is the restart-lane half of the same fixture. Otherwise restore it by re-running MODELEFFORT-03's model and effort selection through the UI.

### MODELEFFORT-13 — Remove a teammate and recreate it with the same Name - nothing resurrects

**Free** · about 10 min

*Proves removing a Persona deletes its Model and Effort rows, so a later teammate of the same name cannot silently inherit settings its creator never chose - including through the case-insensitive key.*

**Before you start**

- Free lane, app running.
- MODELEFFORT-02 has been run (LADDER-MODEL known).

**Steps**

1. On `/teammates`, click **New teammate**. Type **Name** `Probe Two`, **Title** `Tester`, **Alias** `p2`, **Persona body** `You are a tester.`
2. Set **Model** to LADDER-MODEL and, once the Effort select settles, set **Effort** to `Low`.
3. Click **Add teammate**.
4. In `T-C` run `Get-TeamDb "SELECT * FROM persona_models WHERE persona_name='Probe Two'"` and `Get-TeamDb "SELECT * FROM persona_efforts WHERE persona_name='Probe Two'"`. Both must return one row. Write the values down.
5. On the card now showing, click **Remove**. Two buttons replace it: **Confirm** and **Cancel**.
6. Click **Confirm**. The card closes and the `Probe Two` tile disappears from the list.
7. In `T-C` run both queries from step 4 again.
8. Click **New teammate**. Type **Name** `Probe Two`, **Title** `Tester`, **Alias** `p2`, **Persona body** `You are a tester.` and leave BOTH selects on `Use the agent's default`.
9. Click **Add teammate**.
10. Read the **Model** and **Effort** sections of the card now showing.
11. In `T-C` run both queries from step 4 once more.
12. Now test the case-insensitive key: click **Remove** then **Confirm** on `Probe Two`. Click **New teammate** and create `probe two` (all lowercase) with **Title** `Tester`, **Alias** `p2`, **Persona body** `You are a tester.`, both selects left on `Use the agent's default`.
13. In `T-C` run `Get-TeamDb "SELECT * FROM persona_models"` and `Get-TeamDb "SELECT * FROM persona_efforts"` and look for any row keyed `Probe Two` or `probe two`.
14. Clean up: remove `probe two` (**Remove**, then **Confirm**).

**Pass if — all of these**

- Between the remove and the recreate (step 7), BOTH queries return zero rows for `Probe Two`.
- After recreating with both selects on default (step 11), both queries still return zero rows for `Probe Two`.
- The recreated teammate's card shows **Model** = `Agent default` and **Effort** = `Model default`.
- After the lowercase recreate (step 13), there is no row keyed `Probe Two` or `probe two` in either table.

**Fail if — any of these**

- A row survives the Remove (step 7 returns a row) -> a later teammate with the same Name silently runs on a Model or Effort its creator never chose, and the only evidence is a stale database row.
- The recreated teammate's card shows a model name or a level instead of the two defaults -> the old setting resurrected through the UI, which is the same failure seen from the front.
- The lowercase recreate inherits the old row -> the case-insensitive key is being bypassed by a case-sensitive delete, so `Probe Two` and `probe two` diverge where the schema says they must not.

**Inconclusive if**

If **Confirm** produces a red error line on the card, the removal never happened - read the message, resolve it (usually a file lock) and re-run. Seeing the removed teammate's old Agent still listed in a Room or in the sidebar is NOT a failure of this test: Agents, Rooms and Transcripts deliberately do not cascade, only the Model and Effort rows do.

> [!NOTE]
> This is manual checklist step 19.

### MODELEFFORT-14 — Editing the frontmatter name moves the Model and Effort rows, and only those

**Free** · about 8 min

*Proves a rename carries the teammate's Model and Effort with it and leaves no row behind under the old name - a drop is silent, and a leftover row silently arms a future teammate of the old name.*

**Before you start**

- Free lane, app running.
- MODELEFFORT-02 has been run (LADDER-MODEL known).

**Steps**

1. On `/teammates`, click **New teammate**. Type **Name** `Probe Three`, **Title** `Tester`, **Alias** `p3`, **Persona body** `You are a tester.`
2. Set **Model** to LADDER-MODEL, wait for the Effort select to settle, set **Effort** to `Low`, then click **Add teammate**.
3. In `T-C` run `Get-TeamDb "SELECT * FROM persona_models"` and `Get-TeamDb "SELECT * FROM persona_efforts"`. Record the exact values of the `Probe Three` rows.
4. On the card now showing, click **Edit**. The **Persona text** textarea now contains the WHOLE file including front matter - the hint under it reads `Markdown. This is the whole Persona file, front matter included, and becomes the teammate's system prompt. Saving restarts it, which clears what it remembers.`
5. In the textarea, change the line `name: 'Probe Three'` to `name: 'Probe Four'`. Change nothing else - leave the Model and Effort selects alone.
6. Click **Save**.
7. Look at the teammate list behind the card: it must now show a tile named `Probe Four`.
8. Read the **Model** and **Effort** sections of the card now showing.
9. In `T-C` run both queries from step 3 again and look for rows keyed `Probe Three` and `Probe Four`.
10. In `T-C` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams'` and note the filename.
11. Clean up: open `Probe Four`, click **Remove**, then **Confirm**.

**Pass if — all of these**

- The tile list shows `Probe Four` and no longer shows `Probe Three`.
- `persona_models` contains exactly one row keyed `Probe Four` holding the same model id recorded in step 3, and NO row keyed `Probe Three`.
- `persona_efforts` contains exactly one row keyed `Probe Four` holding `low`, and NO row keyed `Probe Three`.
- The card's **Model** and **Effort** sections show the LADDER-MODEL choice and `Low` - not `Agent default` / `Model default`.

**Fail if — any of these**

- The rows do not move (the `Probe Four` rows are absent or default) -> editing `name:` silently drops the teammate's Model and Effort; the teammate then runs on the agent default with the card showing nothing wrong.
- A row keyed `Probe Three` is left behind -> a future Persona named `Probe Three` will silently inherit it, which is exactly the resurrection failure MODELEFFORT-13 guards.
- Both a `Probe Three` and a `Probe Four` row exist -> the rename copied instead of moving, same resurrection risk.

**Inconclusive if**

If **Save** shows a red error line (a malformed edit, or a Name/Alias collision with another Persona), the rename never happened - fix the text and re-run. Seeing the OLD name `Probe Three` still listed in a Room, in the sidebar or in a Team directory is NOT a failure: only the Model and Effort follow a rename; the Agent, its Rooms and its Transcripts deliberately stay behind under the old Name.

> [!NOTE]
> The filename does not change on a rename - the Persona's identity is its frontmatter, not its filename, so `Probe Three.md` still holding `Probe Four` is correct.

### MODELEFFORT-15 — The View card's Model and Effort sections, and the label-versus-id inconsistency

**Free** · about 6 min

*Proves the View card never invents a value, and records the documented (and confusing) difference between what the card shows right after a Save and what it shows when opened from a tile.*

**Before you start**

- Free lane, app running.
- `Probe One` exists with a stored Model and Effort in wire-id form (re-run MODELEFFORT-03 if MODELEFFORT-12 left it stale).

**Steps**

1. On `/teammates`, click the `Probe One` tile, then click **Edit**.
2. Without changing anything, click **Save**.
3. IMMEDIATELY read the **Model** section and the **Effort** section of the View card now showing, and write both down.
4. Close the card with the × button (tooltip `Close`).
5. Click the `Probe One` tile again.
6. Read the **Model** and **Effort** sections again and write both down.
7. In `T-C` run `Get-TeamDb "SELECT persona_name, model FROM persona_models"` and `Get-TeamDb "SELECT persona_name, effort FROM persona_efforts"`.
8. Compare each of the four values you wrote down against the stored values.

**Pass if — all of these**

- Right after **Save**, the **Model** section shows the friendly label (for example `Haiku 4.5`) and the **Effort** section shows the friendly label (for example `Low`).
- When the same card is opened from the tile, the sections show the raw stored ids instead (for example `claude-haiku-4-5` and `low`).
- Every one of the four values is either the exact stored id or that id's catalog label - never a third string, and never `Agent default` / `Model default` for a teammate that has stored values.

**Fail if — any of these**

- A teammate WITH a stored model shows `Agent default`, or one with a stored effort shows `Model default` -> the file-to-database join that attaches stored values to a Persona has broken, and the card is reporting a default that is not what will actually be used.
- Either section shows a value that is neither the stored id nor its catalog label -> the display is deriving a value rather than reading one.

**Inconclusive if**

If both views show the same thing (labels in both, or ids in both), that is not automatically a failure - it depends on whether the catalogs happened to be populated. Record what you saw and check it against the stored values; only a value that matches neither the id nor its label is a defect.

> [!NOTE]
> The label-then-id switch is a documented consequence of the rule that opening from a tile never probes, so the catalogs are empty on that path. Report it as a copy observation if it confuses you, not as a functional defect.

### MODELEFFORT-16 — The probe runs in App_Data\work, never in the repository root

**Free** · about 5 min

*Proves the throwaway adapter is launched with its own working directory, so it cannot auto-load this repository's own CLAUDE.md and .claude/settings.json into a process that is only being asked what models it offers.*

**Before you start**

- Free lane.
- `T-B` running.

**Steps**

1. Restart the app.
2. In `T-C` run `Remove-Item -Recurse -Force 'E:\Repos\Huddle\src\Huddle.App\App_Data\work' -ErrorAction SilentlyContinue`.
3. In `T-C` run `git -C E:\Repos\Huddle status --porcelain` and save the output.
4. In `T-C` run `Get-ChildItem -Force 'E:\Repos\Huddle' | Select-Object -ExpandProperty Name` and save the list.
5. Open `http://localhost:5100/teammates` and click **New teammate**. Wait for both selects to settle.
6. In `T-C` run `Test-Path 'E:\Repos\Huddle\src\Huddle.App\App_Data\work'`.
7. In `T-C` re-run `git -C E:\Repos\Huddle status --porcelain` and compare with step 3.
8. In `T-C` re-run the root listing from step 4 and compare.
9. In `T-A`, read any lines beginning `[agent stderr]` emitted during the probe.

**Pass if — all of these**

- `App_Data\work` exists after the card open.
- `git status --porcelain` output is identical before and after the probe (no new or modified files at the repository root).
- The repository root's file list is unchanged - in particular no new `.claude` artifacts, session files or logs appeared there.
- No `[agent stderr]` line references `E:\Repos\Huddle\CLAUDE.md`, `agents/CSharpPrinciples.md`, or any repository-root instruction file.

**Fail if — any of these**

- New untracked files appear at the repository root after a probe -> the adapter is running with the repo root as its working directory, which means it is auto-loading this repository's own CLAUDE.md and .claude/settings.json into a throwaway process; completely invisible from the UI, since the picker looks identical either way.
- `App_Data\work` is not created but the catalog populates -> the probe is running somewhere else entirely; find out where before passing anything in this area.

**Inconclusive if**

If `git status` was already dirty before the test, the comparison is unreliable - stash or commit first, or compare only the specific filenames that are new. If the adapter emits no stderr at all, step 9 is simply empty, which is fine.

> [!NOTE]
> There is no log line naming the probe's working directory; the created `App_Data\work` folder and a clean repo root are the whole oracle.

### MODELEFFORT-17 — Two browser tabs opening cards at once spawn one adapter, not two

**Free** · about 6 min

*Proves the single gate holds across concurrent Blazor circuits, which is also the only configuration in which the effort cache's concurrency matters.*

**Before you start**

- Free lane.
- The app has just been restarted, so both catalogs are cold and no card has been opened in this run.
- `T-B` running and resting at 0.

**Steps**

1. Restart the app.
2. Open `http://localhost:5100/teammates` in TWO separate browser windows, side by side. Do not click anything in either yet.
3. Position `T-B` where you can see it while clicking.
4. Click **New teammate** in the first browser window, then within about half a second click **New teammate** in the second.
5. Watch `T-B` continuously for 45 seconds and write down the highest number it ever reaches.
6. In both browser windows, check that the **Model** select is populated and that the Model hint has settled.
7. In both browser windows, check that the **Effort** select is populated and its hint has settled.
8. In `T-A`, read the log for any unhandled exception or stack trace.
9. In both browser windows, look at the bottom of the page for a Blazor error bar (a strip reading that an unhandled error has occurred).

**Pass if — all of these**

- `T-B`'s peak is exactly 1 across the whole 45 seconds.
- Both browser windows end with a populated **Model** select showing the same entries.
- Both browser windows end with a settled **Effort** hint (either the normal hint or the no-effort sentence).
- No unhandled exception or stack trace appears in the app log.
- Neither browser window shows a Blazor error bar.

**Fail if — any of these**

- `T-B` peaks at 2 -> the gate is no longer shared across circuits and two tabs cost two adapter processes.
- Either tab hangs with the loading hint for more than 25 seconds -> a deadlock or a lost gate release, which the single-tab tests cannot see.
- An unhandled exception appears in the log, or a Blazor error bar appears -> the concurrent path is corrupting shared state; the effort cache's concurrency is the documented suspect and it reproduces only under two circuits at once.
- One tab's Effort list appears in the other tab (they differ though both selects show `Use the agent's default`) -> cross-circuit state bleed.

**Inconclusive if**

If you cannot click both within roughly a second, the overlap may not have happened - repeat after restarting the app (a warm cache makes the second open a pure cache hit and proves nothing). If `T-B` never leaves 0 but both lists populate, the poll missed a short spawn: record the count half INCONCLUSIVE and judge only the no-error, both-populated half.

> [!NOTE]
> Two ordinary windows are enough; they do not need to be different browsers or different users.

### MODELEFFORT-18 — The model catalog is cached for the whole app run, and goes fresh on restart

**Free** · about 7 min

*Records the deliberate staleness of the presentational catalog so it is not filed as a defect, and proves a restart does pick up a changed adapter.*

**Before you start**

- Free lane.
- The app has just been restarted.
- No other test is mid-run, because this one moves the adapter aside.

**Steps**

1. Restart the app, open `http://localhost:5100/teammates`, click **New teammate** and write down every entry in the **Model** select.
2. Click **Cancel**.
3. In `T-C` run `Rename-Item 'E:\Repos\Huddle\tools\acp\node_modules' 'node_modules.off'`.
4. Without restarting the app, click **New teammate** again and write down every entry in the **Model** select.
5. Watch `T-B` during step 4.
6. In `T-A`, check whether any new log line appeared during step 4.
7. Click **Cancel**. Restart the app.
8. Open `/teammates` and click **New teammate**. Write down the **Model** select's contents and the Model hint text.
9. In `T-C` run `Rename-Item 'E:\Repos\Huddle\tools\acp\node_modules.off' 'node_modules'` to restore the adapter.
10. Restart the app and confirm with one more **New teammate** that the real list is back.

**Pass if — all of these**

- Step 4's list is identical to step 1's, even though the adapter has been moved aside - the catalog is cached for the app run.
- `T-B` stays at 0 during step 4 and no new probe log line appears - the cached answer is returned without spawning anything.
- After the restart in step 7, the **Model** select contains only `Use the agent's default` and the Model hint reads exactly `This agent advertises no models, so it will use its own default.`
- After restoring the adapter and restarting, the real model list is back.

**Fail if — any of these**

- Step 4 spawns an adapter or produces a different list -> the successful catalog is no longer cached; every card open now costs a process. (Note this is a cost and consistency defect, not a correctness one: the model actually applied to a session is re-resolved live on every session start regardless.)
- After the restart in step 7 the full model list is STILL shown with the adapter moved aside -> the list is not coming from the adapter at all; it is hardcoded or persisted somewhere it should not be. This is the serious failure this test can expose.
- The restore in step 9 fails or leaves the app unable to find the adapter after a restart -> stop and fix the filesystem state before running anything else in this area.

**Inconclusive if**

If step 3's rename fails with 'file in use', a runner or a probe still holds the adapter - stop the app, rename, then start it again (and note that doing so invalidates the 'without restarting' part of step 4; re-run the whole test). If the catalog was already empty in step 1, this test proves nothing - fix the environment first (see MODELEFFORT-01's inconclusive notes).

> [!NOTE]
> A stale picker after an adapter upgrade is documented as deliberate, not a bug: the cache is presentational only. Do not file it. What WOULD be a defect is a stale model actually being applied to a running session - MODELEFFORT-25 tests that side.

### MODELEFFORT-19 — No adapter installed: the page still works, and a failed probe is never cached

**Free** · about 10 min

*Proves the app degrades gracefully with no adapter, says so in the picker, still saves a teammate, and - the opposite of MODELEFFORT-18 - picks the adapter back up with no restart once it returns, because a failure is never cached.*

**Before you start**

- Free lane.
- The app must be STOPPED before the adapter is moved, and the first card open after starting it must be the one this test observes.
- `T-B` running.

**Steps**

1. Stop the app (Ctrl+C in `T-A`).
2. In `T-C` run `Rename-Item 'E:\Repos\Huddle\tools\acp\node_modules' 'node_modules.off'`.
3. Start the app in the free lane and note the last log line in `T-A`.
4. Open `http://localhost:5100/teammates`. Confirm the page renders normally - heading, intro paragraph, tiles for existing teammates.
5. Click **New teammate** - this must be the FIRST card open of this app run.
6. Read the hint under the **Model** select and the hint under the **Effort** select, and write both down exactly.
7. Open both selects and write down every option each contains.
8. In `T-A`, read the new log lines.
9. Watch `T-B` for 15 seconds.
10. Type **Name** `Probe NoAdapter`, **Title** `Tester`, **Alias** `pna`, **Persona body** `You are a tester.` and click **Add teammate**.
11. In `T-C` run `Get-TeamDb "SELECT * FROM persona_models WHERE persona_name='Probe NoAdapter'"` and `Get-TeamDb "SELECT * FROM persona_efforts WHERE persona_name='Probe NoAdapter'"`.
12. WITHOUT restarting the app, run in `T-C`: `Rename-Item 'E:\Repos\Huddle\tools\acp\node_modules.off' 'node_modules'`.
13. In the browser, close the card (× button) and click **New teammate** again. Wait for the hints to settle.
14. Read the **Model** select's contents.
15. Clean up: click **Cancel**, open `Probe NoAdapter`, click **Remove**, then **Confirm**.

**Pass if — all of these**

- `/teammates` renders normally with no adapter present - no error banner, no blank page.
- The card opens, and the Model hint reads exactly `This agent advertises no models, so it will use its own default.`
- The Effort hint reads exactly `This model offers no effort choice, so it will think as it normally does.`
- Both selects contain ONLY the option `Use the agent's default`.
- The log contains `No ACP adapter is installed; the model catalog is empty.` at Information from category `Agency.Huddle.App.Acp.ModelCatalogProbe`.
- `T-B` stays at 0 throughout - nothing is spawned when there is nothing to spawn.
- The teammate saves successfully, with zero rows in both `persona_models` and `persona_efforts`.
- After the adapter is restored and the card is re-opened WITHOUT an app restart, the real model list appears.

**Fail if — any of these**

- The page errors, shows an error banner, or the card never opens -> a missing adapter is being treated as fatal instead of as an empty catalog.
- **Add teammate** fails or writes a row into either table -> saving is coupled to a catalog that does not exist.
- After restoring the adapter, the list does NOT appear without an app restart -> a FAILED probe is being cached, which is explicitly forbidden: installing or authenticating the adapter must take effect on the next card open.
- The full model list still appears while the adapter is moved aside AND the app was restarted before the card was opened -> the list is not coming from the adapter (see MODELEFFORT-18's fail note).

**Inconclusive if**

FALSE PASS TRAP: if any card was opened earlier in the SAME app run, the cached catalog is still being shown and moving the adapter aside will look like it did nothing. If you are not certain step 5 was the first card open of the run, restart the app and redo the test. If the rename in step 2 fails with 'file in use', the app is still running - stop it properly first.

> [!NOTE]
> This is manual checklist step 8 plus the never-cache-a-failure guarantee, which is the deliberate opposite of MODELEFFORT-18.

### MODELEFFORT-20 — Adapter present but broken: launch failure and auth failure degrade to the same picker, with distinct log lines

**Free** · about 10 min

*Proves a broken adapter never hangs or crashes the page, that the timeout is bounded, and that the log - not the picker - is what distinguishes the four causes.*

**Before you start**

- Free lane.
- Each part needs its own app run, and the observed card open must be the FIRST of that run.
- The adapter is installed and `tools\acp\node_modules` is in place.

**Steps**

1. PART A - launch failure. Stop the app. In `T-A` run `$env:Team__Acp__Command='node-does-not-exist'` and then the free-lane `dotnet run` line.
2. Open `http://localhost:5100/teammates` and confirm the page renders normally.
3. Click **New teammate** and start a stopwatch.
4. Note how long it takes for the Model hint to stop reading `Reading the models this agent offers…`.
5. Write down the Model hint, the Effort hint, and the contents of both selects.
6. In `T-A`, read the new log lines.
7. Confirm the card can be closed with **Cancel** and the page is still usable (click a tile, open and close it).
8. Stop the app and run `Remove-Item Env:Team__Acp__Command` in `T-A`.
9. PART B - authentication. If you have a way to put the Claude CLI into an unauthenticated state for this machine, do so now; otherwise skip to step 12 and record Part B as inconclusive.
10. Start the app in the free lane, open `/teammates`, and click **New teammate** as the first card open.
11. Write down both hints, both select contents, and the new log lines. Then restore authentication.
12. PART C - timeout. Record the expected shape without forcing it: a probe that neither answers nor fails must be abandoned after a hard 20 seconds with the log line `Model catalog probe timed out after 00:00:20.` and the same two 'no choice' sentences in the card. Only report a timeout observation if you actually saw one during any test in this set.

**Pass if — all of these**

- In Part A the page renders and the card opens; both selects contain only `Use the agent's default`.
- Part A's Model hint reads exactly `This agent advertises no models, so it will use its own default.` and its Effort hint reads exactly `This model offers no effort choice, so it will think as it normally does.`
- Part A's log contains `Model catalog probe failed to start or talk to the adapter.` at Warning.
- The card settles in well under 20 seconds and never hangs; the page stays usable afterwards.
- If Part B ran: the same two sentences appear, and the log contains `Model catalog probe skipped: the adapter needs authentication.` at Warning - a DIFFERENT line from Part A's, for the same on-screen text.

**Fail if — any of these**

- The page throws, shows an error banner, or the card never opens -> a broken adapter is crashing the surface instead of degrading it.
- The card hangs for more than about 25 seconds -> the 20-second probe timeout is not being enforced, so one bad adapter can wedge every card open.
- The log line does not distinguish the cause (for example a launch failure logs the auth message, or nothing at all is logged) -> the picker is deliberately ambiguous and the log is the ONLY disambiguator; losing it makes 'the adapter is missing' and 'the adapter needs authentication' indistinguishable to anyone debugging.

**Inconclusive if**

The identical on-screen text across all four causes is DOCUMENTED and must not be reported as a bug - only a page that throws, a card that never opens, or a hang past about 20 seconds is a defect. If you have no safe way to deauthenticate the Claude CLI, record Part B INCONCLUSIVE rather than improvising with credentials. Part C is expected to be inconclusive unless a hang happened naturally.

> [!NOTE]
> Always run `Remove-Item Env:Team__Acp__Command` in `T-A` before any later test, or every subsequent probe will fail for a reason you introduced.

### MODELEFFORT-21 — An existing App_Data opens with the two tables added in place, no wipe

**Free** · about 8 min

*Proves a database that predates these two tables gains them on startup without losing anything and without the user being told to delete App_Data.*

**Before you start**

- Free lane.
- The app must be STOPPED while the database is edited.
- At least one teammate and, ideally, some existing chat data exist.

**Steps**

1. Stop the app.
2. In `T-C` run `Copy-Item 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db.backup'` so you can restore if anything goes wrong.
3. In `T-C` run `Get-TeamDb "SELECT COUNT(*) AS users FROM users"` and `Get-TeamDb "SELECT COUNT(*) AS rooms FROM rooms"` and write both counts down.
4. In `T-C` run `Set-TeamDb "DROP TABLE IF EXISTS persona_models; DROP TABLE IF EXISTS persona_efforts;"`.
5. In `T-C` run `Get-TeamDb "SELECT name FROM sqlite_master WHERE type='table'"` and confirm neither table is listed.
6. Start the app in the free lane. Watch `T-A` for a startup exception.
7. Open `http://localhost:5100/teammates` and confirm the page renders, with the same teammate tiles as before.
8. In `T-C` run `Get-TeamDb "SELECT name FROM sqlite_master WHERE type='table'"` again.
9. In `T-C` re-run the two count queries from step 3.
10. Click **New teammate**, create `Probe Migrate` with **Title** `Tester`, **Alias** `pm`, **Persona body** `You are a tester.`, **Model** = LADDER-MODEL and **Effort** = `Low`, then click **Add teammate**.
11. In `T-C` run `Get-TeamDb "SELECT * FROM persona_models WHERE persona_name='Probe Migrate'"` and `Get-TeamDb "SELECT * FROM persona_efforts WHERE persona_name='Probe Migrate'"`.
12. Clean up: remove `Probe Migrate` through the card (**Remove**, then **Confirm**), and delete the backup file if everything passed.

**Pass if — all of these**

- The app starts with no exception in the log.
- `/teammates` renders with the same teammates as before the drop.
- After startup, `sqlite_master` lists BOTH `persona_models` and `persona_efforts` again.
- The `users` and `rooms` counts are unchanged from step 3 - nothing was wiped.
- The new teammate's Model and Effort rows are written into the freshly created tables.

**Fail if — any of these**

- The app throws on startup with the tables missing -> an existing App_Data is not being migrated in place, and users would be told to delete their data.
- The tables are recreated but the chat tables are emptied -> the migration is destructive.
- The teammate saves but no row appears in the new tables -> the tables were created but the stores are not writing into them.

**Inconclusive if**

If `Set-TeamDb` errors with 'database is locked', the app is still running - stop it fully and retry. If anything goes wrong, restore with `Copy-Item 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db.backup' 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' -Force` while the app is stopped, and record INCONCLUSIVE.

> [!NOTE]
> This is manual checklist step 9. Each setting deliberately gets its own table rather than a column, because a column added to an existing table is silently ignored forever by `CREATE TABLE IF NOT EXISTS`.

### MODELEFFORT-22 — RESTART LANE: changing the Model restarts the runner, exactly once

**Free** · about 10 min

*Proves a Model change actually takes effect - a Model is fixed at session start, so a restart is the only mechanism - and that it does not cause a restart storm.*

**Before you start**

- Restart lane: `T-A` started with `Remove-Item Env:Team__Acp__Enabled` then the `dotnet run` line, so teammate runners start.
- The adapter is installed and authenticated enough to start a session.
- `Probe One` exists with Model = HAIKU and Effort = `Low` (re-create through the UI per MODELEFFORT-03 if needed).
- You will type NOTHING into any Room during this test, so no tokens are spent.

**Steps**

1. Start the app in the restart lane. Open `http://localhost:5100/teammates`.
2. Wait until the `Probe One` tile shows a green dot and the label `Online`. This may take several seconds.
3. In `T-B` note the resting adapter count now that runners are up, and in `T-C` run `Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like '*claude-agent-acp*' } | Select-Object ProcessId` and write down the PIDs.
4. In `T-A`, note the last log line.
5. Click the `Probe One` tile, then click **Edit**. Wait for both selects to settle.
6. Change the **Model** select from HAIKU to SONNET. Leave the Persona text and everything else untouched.
7. Click **Save**.
8. Watch the card's status line and the tile's dot continuously for the next 30 seconds. Write down every status word you see, in order (`Online`, `Offline`, `Starting`, `Degraded`).
9. Count how many times the status leaves `Online` and returns to it.
10. In `T-A`, read every new log line since step 4.
11. In `T-C` re-run the PID query from step 3 and compare the PIDs.
12. In `T-C` run `Get-TeamDb "SELECT model FROM persona_models WHERE persona_name='Probe One'"`.

**Pass if — all of these**

- The status visibly leaves `Online` - through `Offline` and/or `Starting` - and returns to `Online` within about 30 seconds.
- That transition happens exactly ONCE.
- The log contains `The agent process disconnected.` at Warning from `Agency.Huddle.Acp.DotAcp.DotAcpAgentHost`, followed by a fresh burst of `[agent stderr] …` lines from the new process.
- The adapter PID for this teammate has changed, and the total adapter count is back to what it was before.
- The log contains NO line matching `Requested model '…' is not in the agent's advertised model catalog`.
- `persona_models.model` for `Probe One` is now SONNET's wire id.

**Fail if — any of these**

- The status never leaves `Online` and the PID is unchanged -> the Model change is not triggering a restart; since a Model is fixed at session start there is no other way for it to take effect, so the teammate keeps running the OLD model while the card shows the new one. Entirely silent.
- The status leaves and returns to `Online` MORE than once -> a restart storm: the file write's watcher event is producing a second restart on top of the explicit one, and each restart destroys the session's conversation memory.
- A `Requested model '…' is not in the agent's advertised model catalog` warning appears -> the stored value does not match the adapter's option value (a display name, or a stale id); the session is silently running on the agent default with nothing on screen saying so.
- The teammate ends `Offline` or `Degraded` and never returns to `Online` -> the restart broke the teammate rather than replacing its session.

**Inconclusive if**

If `Probe One` never reaches `Online` at all in this lane, this test cannot run - read the reason line on its card (and the tile's status tooltip), fix the environment, and mark INCONCLUSIVE. If the status transition is too fast to see, judge on the log and PID evidence instead and say so.

> [!NOTE]
> Losing the Room's conversation memory on every save is the documented cost of this design, not a defect. Starting a session is not a prompt turn, so this test spends nothing.

### MODELEFFORT-23 — RESTART LANE: changing Effort Low to Medium restarts, with no catalog warning

**Free** · about 8 min

*Proves an Effort change takes effect the only way it can - a restart - and, more importantly, that the stored effort resolves against the adapter's advertised catalog for the CURRENT model rather than a pre-switch snapshot.*

**Before you start**

- Restart lane, app running.
- A teammate that is `Online` whose current Model advertises at least `Low` and `Medium` (use LADDER-MODEL from MODELEFFORT-02), with Effort currently `Low`.

**Steps**

1. On `/teammates`, confirm the teammate's tile shows `Online`.
2. In `T-C` record its adapter PIDs: `Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like '*claude-agent-acp*' } | Select-Object ProcessId`.
3. In `T-A`, note the last log line.
4. Click the teammate's tile, then click **Edit**. Wait for both selects to settle and confirm **Effort** shows `Low`.
5. Change the **Effort** select from `Low` to `Medium`. Do NOT touch the Model select or the Persona text.
6. Click **Save**.
7. Watch the card status and the tile dot for 30 seconds and write down every status word in order.
8. In `T-A`, read every new log line since step 3, and specifically search for the words `advertised effort catalog`.
9. In `T-C` re-run the PID query and compare.
10. In `T-C` run `Get-TeamDb "SELECT effort FROM persona_efforts WHERE persona_name='<the teammate name>'"`.

**Pass if — all of these**

- The status leaves `Online` and returns to `Online` exactly once, within about 30 seconds.
- The log contains `The agent process disconnected.` followed by a fresh `[agent stderr] …` burst.
- The log contains NO line matching `Requested effort 'medium' is not in the agent's advertised effort catalog for the current model; continuing on the model's default.`
- The adapter PID for this teammate has changed.
- `persona_efforts.effort` is exactly `medium` - lowercase.

**Fail if — any of these**

- No status transition and no PID change -> the Effort never takes; an Effort is fixed at session start exactly like a Model and a system prompt, so without a restart the teammate keeps thinking at the old level, invisibly.
- The warning `Requested effort 'medium' is not in the agent's advertised effort catalog for the current model; continuing on the model's default.` appears -> the stored value does not match the adapter's option value: either a display name was stored, or the effort was resolved against the pre-model-switch snapshot instead of the post-switch one. The session then runs on the model's default with nothing on screen saying so.
- `persona_efforts.effort` holds `Medium` with a capital M -> a display name reached the database and will warn on every restart forever.
- More than one transition -> a restart storm, as in MODELEFFORT-22.

**Inconclusive if**

If MODELEFFORT-02 found no model advertising both `Low` and `Medium`, this test has no fixture: record INCONCLUSIVE with the adapter version. If the teammate is not `Online` to begin with, fix that first - a restart cannot be observed from a teammate that was never running.

> [!NOTE]
> This is manual checklist step 17. The card deliberately shows the STORED effort, not the session's live value, so if the adapter clamps a level the card will not say so - that is a recorded limit, not a defect.

### MODELEFFORT-24 — RESTART LANE: saving with nothing changed does NOT restart the runner

**Free** · about 8 min

*Proves the no-op guard works, so touching the card does not throw away a teammate's conversation memory for nothing - and does so on a fixture that cannot produce a false defect.*

**Before you start**

- Restart lane, app running.
- The teammate under test MUST have been created through the UI, not hand-authored in an editor. Use `Probe One`.
- The teammate is `Online`.

**Steps**

1. In `T-C` confirm the Persona file is LF-only: `[bool]((Get-Content -Raw 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Probe One.md') -match "\r\n")` must print **False**. If it prints True, see INCONCLUSIVE.
2. In `T-C` record the adapter PIDs: `Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like '*claude-agent-acp*' } | Select-Object ProcessId`.
3. In `T-A`, note the last log line.
4. On `/teammates`, confirm `Probe One` shows `Online`. Click its tile, then click **Edit**.
5. Wait for both selects to settle. Touch NOTHING - do not click into the textarea, do not open either select.
6. Click **Save**.
7. Watch the card status line and the tile dot continuously for at least 15 seconds - the file write also fires a 500 ms-debounced watcher event, and that second event must also decide 'no change'.
8. In `T-A`, read every new log line since step 3.
9. In `T-C` re-run the PID query and compare with step 2.
10. Repeat steps 4-9 once more (a second consecutive no-op save) and record the same observations.

**Pass if — all of these**

- The status reads `Online` continuously throughout - no flicker, no `Starting`, no `Offline`, on both the card and the tile.
- The log contains NO `The agent process disconnected.` line and no new `[agent stderr] …` burst.
- The adapter PIDs are identical before and after both saves.
- The second consecutive no-op save behaves identically to the first.

**Fail if — any of these**

- The teammate restarts on a no-op save of an LF file -> the record comparison that decides 'nothing changed' has broken; every teammate now loses its conversation memory on any touch of the card, which is exactly the cost this guard exists to avoid.
- The teammate restarts TWICE on one save -> the watcher event is producing a restart of its own on top of the save path.
- The status flickers to `Degraded` -> the save is disturbing a healthy session even when it does not restart it.

**Inconclusive if**

FALSE DEFECT TRAP: if step 1 prints True, the Persona file has CRLF line endings (hand-authored in an editor). The browser textarea normalises CRLF to LF, so the FIRST save legitimately changes the file and legitimately restarts the teammate - that is not a defect. Judge this test only on a UI-created Persona, or only on the second consecutive save. If you cannot confirm the line endings, mark INCONCLUSIVE rather than filing a restart.

> [!NOTE]
> This is manual checklist step 18 and the single most likely test in this set to produce a false defect report. Confirm the line endings before you judge it.

### MODELEFFORT-25 — RESTART LANE: a stale stored Model or Effort warns but never blocks the teammate

**Free** · about 8 min

*Proves a value the adapter no longer advertises is a warning and not a failure - the teammate must still come Online, running on the default.*

**Before you start**

- Restart lane.
- The app must be STOPPED while the database is edited.
- `Probe One` exists.

**Steps**

1. Stop the app.
2. In `T-C` run `Set-TeamDb "UPDATE persona_models SET model='claude-nonexistent-9' WHERE persona_name='Probe One';"`.
3. In `T-C` run `Set-TeamDb "INSERT INTO persona_efforts(persona_name, effort) VALUES('Probe One','vintage') ON CONFLICT(persona_name) DO UPDATE SET effort='vintage';"`.
4. Start the app in the restart lane and watch `T-A` from the first line.
5. Open `http://localhost:5100/teammates` and watch the `Probe One` tile for up to 60 seconds.
6. Write down the final status word on the tile and, if it is not `Online`, hover it to read the status tooltip.
7. In `T-A`, search the log for `advertised model catalog` and for `advertised effort catalog`.
8. Click the `Probe One` tile and read the card: the status line, any reason line beneath it, and the **Model** and **Effort** sections.
9. Restore the fixture: click **Edit**, set **Model** back to HAIKU, wait for the Effort list, set **Effort** to `Low`, click **Save**.

**Pass if — all of these**

- The log contains `Requested model 'claude-nonexistent-9' is not in the agent's advertised model catalog; continuing on the agent's default.` at Warning.
- The log contains `Requested effort 'vintage' is not in the agent's advertised effort catalog for the current model; continuing on the model's default.` at Warning.
- Despite both warnings, the `Probe One` tile reaches `Online`.
- The card's **Model** section shows `claude-nonexistent-9` and its **Effort** section shows `vintage` - the stored values, not defaults.

**Fail if — any of these**

- `Probe One` stays `Offline` or `Degraded` -> a stale stored value is bricking a session; a value the agent does not advertise must be a warning, never a failure.
- Neither warning appears AND the teammate is Online -> the stale value is being silently swallowed, so nobody debugging a teammate that is 'running the wrong model' has any evidence.
- The app throws on startup -> an unrecognised stored value is being treated as fatal.

**Inconclusive if**

If the app cannot start a session at all in this lane for unrelated reasons (adapter not authenticated), this test is INCONCLUSIVE - the reason line on the card will say which. If `Set-TeamDb` errors with 'database is locked', the app is still running.

> [!NOTE]
> This is the restart-lane half of MODELEFFORT-12's fixture. Always run step 9 to restore `Probe One` before any later test uses it.

### MODELEFFORT-26 — RESTART LANE: the manual Restart button appears only on an Offline or Degraded teammate

**Free** · about 10 min

*Proves the Restart action is offered exactly where it is needed and never on a healthy teammate (where it would throw away conversation memory for nothing), and that it is not a no-op on a teammate that never started.*

**Before you start**

- Restart lane, app running.
- One teammate is `Online`.
- You can make one teammate fail: either stop its adapter process, or start one app run with a bad adapter command.

**Steps**

1. On `/teammates`, click the tile of a teammate showing `Online`.
2. Write down every button in the card's action row.
3. Confirm whether a hint line appears below the action row.
4. Close the card.
5. Now make a teammate unhealthy. Easiest: in `T-C` run `Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like '*claude-agent-acp*' } | Select-Object ProcessId, CommandLine` to find the adapter process, then `Stop-Process -Id <pid> -Force` for one of them.
6. Watch `/teammates` until that teammate's tile shows `Offline` or `Degraded`.
7. Click that tile to open its card. Write down every button in the action row and the hint line below it.
8. Click **Restart** and immediately read the button's label.
9. Immediately click the same button a second time.
10. Watch the status line for up to 60 seconds and write down every status word in order.
11. In `T-A`, read the new log lines.
12. In `T-C` confirm an adapter process exists again for that teammate.

**Pass if — all of these**

- A healthy (`Online`) teammate's action row shows only **Message**, **Edit**, **Open** and **Remove** - there is NO **Restart** button, and no restart hint line.
- An `Offline` or `Degraded` teammate's action row additionally shows **Restart**, with the hint below the actions reading exactly `Like saving an edit, this restarts the teammate, which clears what it remembers.`
- Clicking **Restart** changes the button's label to `Restarting…` and the button is disabled while the restart is in flight.
- The second click does nothing - only one restart runs.
- The status reaches `Online` (or, if it fails again, shows `Offline`/`Degraded` with a reason line on the card), and the log shows either a fresh `[agent stderr] …` burst or `Persona '<Name>' failed to start.` at Warning with a reason.

**Fail if — any of these**

- **Restart** is offered on a healthy `Online` teammate -> clicking it would restart a session for nothing and throw away its conversation memory.
- **Restart** is missing on an `Offline` or `Degraded` teammate -> the Human has no way to act on a failure they can see.
- Clicking **Restart** on a teammate with no running process does nothing at all (no status change, no new process, no log line) -> the restart path is a no-op exactly where it matters most, for a teammate that failed to start in the first place.
- The button stays enabled during the restart and a double click produces two restarts -> the busy flag is gone.

**Inconclusive if**

If you cannot produce an `Offline` or `Degraded` teammate (the process restarts itself too quickly, or none is running), record the healthy-teammate half as PASS and the rest as INCONCLUSIVE. A `Degraded` teammate NEVER recovering on its own is documented and correct - do not report the absence of automatic recovery as a defect; the Human clicks this button.

> [!NOTE]
> Stopping an adapter process with Stop-Process is the cheapest way to create the fixture and spends nothing.

### MODELEFFORT-27 — MONEY: the chosen Model and Effort actually reach the model - ask it

**💰 Spends money** · about 15 min

*The only end-to-end proof that the picker's value reaches the live session rather than stopping at the database. Everything else in this set proves the plumbing; this proves the water arrives.*

**Before you start**

- Restart lane, with a real, authenticated Claude subscription.
- Before starting, set a low message budget in `T-A`: `$env:Team__AgentMessageBudget='6'` then start the app, so a loop cannot run away.
- A teammate exists with Model = HAIKU and Effort = `Low`. Use LADDER-MODEL if Haiku offers no ladder.
- COST: three short prompt turns of a few hundred tokens each - single-digit cents. Do not send any other message while this test runs.

**Steps**

1. Open `/teammates` and wait until the teammate's tile shows `Online`.
2. Click its tile and click the **Message** action on the card. This opens its Room.
3. In the message box type exactly `@Probe One which model are you running as, and what thinking effort?` (substituting the teammate's real Name after the @) and press Enter.
4. Wait for the reply and write it down verbatim.
5. In `T-A`, search the log for `advertised model catalog` and `advertised effort catalog`, and note the last log line.
6. Go back to `/teammates`, click the teammate's tile, click **Edit**, change the **Model** select from HAIKU to SONNET, and click **Save**.
7. Wait until the tile shows `Online` again, then confirm the log shows `The agent process disconnected.` and a fresh `[agent stderr] …` burst.
8. Return to the same Room and send the identical message again. Write down the reply.
9. Go back to the card, click **Edit**, change the **Effort** select from `Low` to `Medium` (leaving the Model on SONNET), and click **Save**.
10. Wait for `Online`, return to the Room, send the identical message a third time, and write down the reply.
11. In `T-A`, search the whole log from step 5 onward for `advertised model catalog` and `advertised effort catalog`.
12. In `T-C` run `Get-TeamDb "SELECT persona_name, model FROM persona_models"` and `Get-TeamDb "SELECT persona_name, effort FROM persona_efforts"`.
13. Clean up: `Remove-Item Env:Team__AgentMessageBudget` in `T-A` after stopping the app.

**Pass if — all of these**

- The first reply names a Haiku-family model; the second reply names a Sonnet-family model. The two answers DIFFER in the model they name.
- Between each pair of turns the log shows `The agent process disconnected.` followed by a fresh `[agent stderr] …` burst - the restart evidence.
- Across all three turns the log contains NO `Requested model '…' is not in the agent's advertised model catalog` and NO `Requested effort '…' is not in the agent's advertised effort catalog for the current model` warning.
- The database holds the ids matching what was selected: SONNET's wire id and `medium` after step 9.

**Fail if — any of these**

- The model names the SAME model on both turns -> the picker's value is not reaching `session/new`; the session is running on the default and the card is showing a choice that never took effect.
- Any `not in the agent's advertised … catalog` warning appears -> a display name or a stale value is stored, and the session is silently running on a default.
- No restart evidence appears between turns -> the save is not restarting the session, so the new Model could not possibly have applied.
- The teammate replies but never reaches `Online` again after a save -> the restart is breaking the session rather than replacing it.

**Inconclusive if**

The model's self-report about its own EFFORT level is not reliable - treat the MODEL answer as the load-bearing half and the absence of the effort-catalog warning in the log as the oracle for effort; if the model refuses to name its effort, that half is INCONCLUSIVE, not failed. If the teammate never reaches `Online`, or the adapter is unauthenticated, stop: do not spend on a run that cannot answer. If a reply never arrives, check the budget - `Team__AgentMessageBudget` of 6 is deliberately tight.

> [!NOTE]
> This is manual checklist step 7. Each save destroys the Room's conversation memory, so the model will not remember the earlier question - that is expected and is why the identical message is retyped each time rather than asked as a follow-up.

---

Back to [the manual test script](../manual-tests.md).
