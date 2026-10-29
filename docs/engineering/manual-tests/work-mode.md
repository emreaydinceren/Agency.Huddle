# Work Mode

Prove, in a real browser against a real Adapter, that an Agent's **Work Mode** is offered from what its Adapter advertises (with `Plan`, `Auto` and `Bypass permissions` hidden), is cleared by an Adapter change, is never sent when it is hidden, is lifted by `Team:Acp:HiddenModes`, and is absent for an Adapter that advertises none; and that the paid checks hold: `Accept edits` stops the permission request for an edit, `Plan` (once an operator offers it) writes nothing and its request to leave plan mode is refused, `Accept edits` still lets the `~/.claude` guard stop a write outside the Work Dir, and a mode survives an app restart. The automated suite proves the parts; this page proves they hold together on a screen, against an Adapter that really has modes. **Written 2026-09-30.** WORKMODE-06 to -09 passed in the app on 2026-10-01 (see the [tracker](tracker.md#work-mode)); WORKMODE-01 to -05 have no recorded run. Four of the nine tests spend money, because only a Turn shows what a mode changes. On 2026-10-01 a script also drove the real adapter outside the app and measured the wire facts behind WORKMODE-06 to -08 (see each test); those runs answered OQ-2 and OQ-3 and made `plan` hidden by default.

**9 tests** · 5 free, 4 paid 💰 · about 1.8 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below. The
design is [the Work Modes spec](../../Huddle.WorkModes-Specifications.md) (§11 lists the nine tests
as MW-1 to MW-9) and [ADR-0033](../../adr/0033-a-persona-has-a-work-mode.md).

| Test | Spec id | Cost | Open question it answers |
| --- | --- | --- | --- |
| WORKMODE-01 | MW-1 | Free | |
| WORKMODE-02 | MW-6 | Free | |
| WORKMODE-03 | MW-7 | Free | |
| WORKMODE-04 | MW-8 | Free | |
| WORKMODE-05 | MW-9 | Free | |
| WORKMODE-06 | MW-2 | Paid 💰 | |
| WORKMODE-07 | MW-3 | Paid 💰 | OQ-2, answered outside the app 2026-10-01; see the test |
| WORKMODE-08 | MW-4 | Paid 💰 | OQ-3, answered outside the app 2026-10-01; see the test |
| WORKMODE-09 | MW-5 | Paid 💰 | OQ-1, already answered free; see the test |

The spec's order is kept in the Spec id column; the ids here put the free tests first, as every area
does.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named in each test from
[Common procedures](common.md). This area adds:

1. Use a **scratch** Agent on the stock Claude Adapter, Haiku / `low` as
   [§0.3](../manual-tests.md#03-the-model-and-effort-convention) says, never one that matters.
   Whether Haiku advertises an effort ladder is a runtime fact of the installed Adapter that
   [MODELEFFORT-02](model-effort.md) records. If the card offers `low` for Haiku, set it. If it offers
   no ladder, leave Effort on `Model default` and note that in the result; this is not a defect and does
   not change what these tests prove. A mode change restarts the Agent and forgets its Room
   Sessions, as a Model change does.
2. WORKMODE-02 needs an Adapter Profile beyond the stock one, so the Adapter select renders
   (*Inconclusive*, not a defect, without it; see [adapters.md](adapters.md) for setting a second
   Profile up). WORKMODE-05 needs an Adapter that advertises **no** mode: the free mock adapter does,
   because its `session/new` result carries only `sessionId`
   (`tests/Huddle.Acp.Tests/Fakes/FakeAcpAgent.cs:262-266`), so WORKMODE-05 runs on `P-LAUNCH-UAT`.
3. WORKMODE-03 writes to `persona_work_modes` by hand. If `sqlite3` is not on PATH, use the `Set-TeamDb`
   and `Get-TeamDb` helpers from [model-effort.md](model-effort.md) setup step 3.
4. The paid tests (WORKMODE-06 to -09) need **`Team__Acp__TraceWire`**, because the pass condition is
   the wire trace and not only the reply: neither [`O-TOOLCALLS`](common.md#o-toolcalls) nor the mock
   adapter's echo shows a `session/request_permission` or a `set_config_option`. This area is therefore the
   one stated exception to the "never set `Team__Acp__TraceWire`" convention in
   [Common procedures](common.md#standing-conventions). Follow the
   [§0.2](../manual-tests.md#02-the-cost-guard) warning as [file-changes.md](file-changes.md) does: a
   throwaway terminal, `$env:Team__Acp__TraceWire = 'true'` set before the `dotnet run` line of
   `P-LAUNCH-PAID`, closed afterwards, never set for any other test. **That alone prints nothing:** the
   wire trace is written at `Trace` level and the Development log shows `Debug`, so also set
   `[Environment]::SetEnvironmentVariable('Logging__LogLevel__Agency.Huddle.Acp.Wire', 'Trace', 'Process')`
   (found on the 2026-10-01 run; without it `O-LOG` has no `session/*` lines at all). It prints the tool
   server's bearer token into `O-LOG`. To search it, redirect the app's output to a file and use
   `Select-String`; a request is `"method": "session/..."` on its own line, with its body after it. That file
   holds the token: delete it when the area is done and never paste its contents into a result.
5. "A scratch file" in the paid tests means a file under the Agent's Work Dir
   (`src\Huddle.App\App_Data\Agents\<Name>\work\` by default; the Persona's own file sits one
   level up), so the test never touches anything that matters.
6. "Restart the app" always means `P-STOP` then the `dotnet run` line for the lane the test names.

## Tests

### WORKMODE-01 — The picker lists what the Adapter advertises, minus Plan, Auto and Bypass permissions

**Free** · about 10 min

*Proves the picker is filled from the Adapter's own list, that the default hidden list takes `plan`, `auto` and `bypassPermissions` out of it (Windows is not root, so the Adapter advertises all three), and that the blank option is the Adapter's default. `plan` is hidden because a refused plan exit leaves a plan Persona silent in the Room (WORKMODE-07). Spec MW-1.*

**Before you start**

- Free lane (`P-LAUNCH-FREE`), freshly launched. The scratch Agent exists on the stock Claude Adapter. `Team__Acp__HiddenModes` is not set (`Get-ChildItem Env:Team__*`). **Do not open any Agent card in Edit before step 1:** the model catalog is read once and cached for the life of the app, so only the first card open after a launch shows the disabled state in step 2. If a card was already opened, `P-STOP` and launch again.

**Steps**

1. Open `/agents` and open the scratch Agent's card in Edit.
2. Find the **Work mode** select, directly after Effort. Note its state while the catalog is being read.
3. Open it and write down every option's visible text, in order.
4. Pick **Accept edits** and read the helper text under the select. Do not save; close the card.

**Pass if — all of these**

- Step 2 shows the select disabled (not merely hinted) while the probe is out, then enabled. If the probe answers too fast to see the disabled state, record that half as not-observed and judge the rest.
- Step 3 lists the blank option **Agent default**, then **Manual** and **Accept edits**, in the Adapter's names. **Plan**, **Auto** and **Bypass permissions** are absent.
- Step 4's helper text is the Adapter's own description of Accept edits, not a Huddle sentence.

**Fail if — any of these**

- Plan, Auto or Bypass permissions is listed -> the default hidden list is not applied on the way out of the catalog.
- Manual is missing -> `default` is being filtered as Effort's sentinel is; it is a real mode here.

### WORKMODE-02 — Changing the Adapter clears the Work Mode and says so

**Free** · about 10 min

*Proves ADR-0033 extends ADR-0015: an Adapter change clears the Work Mode with its own `role="status"` note, separate from the Model and Effort note. Needs two Adapter Profiles (setup step 2). Spec MW-6.*

**Before you start**

- Free lane. The scratch Agent has Work mode **Accept edits** saved (pick it, Save, reopen the card and confirm it shows). A second Adapter Profile is configured.

**Steps**

1. Open the scratch Agent's card in Edit and change the **Adapter** to the other Profile. Do not save yet.
2. Read the Work mode select and look for a note about it. Inspect the note's element for its `role`.
3. Save, reopen the card in View, and read the **Work mode** row.
4. Read the table: `Get-TeamDb "SELECT * FROM persona_work_modes"` (or the same query through `sqlite3`).

**Pass if — all of these**

- Step 2 shows the select back on the blank option, with a note that begins `Changing the adapter reset Work mode to its default`.
- The note's `role` is `status`, not `alert`, and it is a separate element from the Model and Effort note.
- Step 3 reads `Agent default`.
- Step 4 shows no row for the scratch Agent.

**Fail if — any of these**

- The Work Mode survives the Adapter change -> a mode id from one Adapter is being sent to another, which will not resolve.
- The mode clears with no note -> two controls changing themselves silently is the shape ADR-0015 rejected.
- The note is `role="alert"` -> it interrupts a deliberate action.

### WORKMODE-03 — A hand-written row for a hidden mode is dropped, with a warning

**Free** · about 15 min

*Proves the hidden list is enforced where the options are built, not only in the picker: a `bypassPermissions` row written straight into the database never reaches the Adapter. Spec MW-7.*

**Before you start**

- Restart lane: the free lane without the guard, so Agent runners start (clear `Team__Acp__Enabled` from the `Env:` drive, then the `dotnet run` line, as in [model-effort.md](model-effort.md) setup step 6). Starting a session is not a prompt turn; **do not type a Message into any Room**. `Team__Acp__HiddenModes` is not set.
- `T-A` shows `O-LOG`. The app is stopped for step 1.

**Steps**

1. Run `Set-TeamDb "INSERT OR REPLACE INTO persona_work_modes(persona_name, work_mode) VALUES('scratch','bypassPermissions')"`, using the Agent's real Name (or the same statement through `sqlite3`).
2. Start the app in the restart lane and wait for the scratch Agent to read Online.
3. Search `O-LOG` for `bypassPermissions` and count the warnings.
4. Open the Agent's card in View and read the **Work mode** row.

**Pass if — all of these**

- Step 3 finds **at least one** warning of the form `Persona 'scratch' has Work Mode 'bypassPermissions', which is hidden; starting in the Adapter's own mode.` The warning is logged each time a session opens (`WorkModePolicy.cs:71`), so a second Room or a restart adds one; it is not a failure.
- Step 2 reaches Online: the stale row does not brick the Agent.
- Step 4 reads `bypassPermissions`: the card shows the stored id as written, because a hidden mode is not in the offered list to give it a name (`AgentProfileCard.razor:1296-1307`). The session itself runs on the Adapter's own mode; the card showing the stored id is expected, not a defect (spec E-11).
- If `Team__Acp__TraceWire` was set for this run, the trace holds no `session/set_config_option` for `mode`. Without it, record that half as not-checked; the warning settles the test.

**Fail if — any of these**

- No warning and the Agent starts in Bypass permissions -> the policy is applied only in the picker; this is the safety hole the test exists for.
- The Agent is Degraded or Offline -> a stale stored mode is bricking a Persona.

Delete the row afterwards: `Set-TeamDb "DELETE FROM persona_work_modes"`.

### WORKMODE-04 — Hiding nothing is a list of one empty string

**Free** · about 10 min

*Proves `Team:Acp:HiddenModes` lifts the default block, and records the configuration trap: `[]` cannot hide nothing, because configuration cannot express an empty list. The spec's `[]` wording is corrected here. Spec MW-8.*

**Before you start**

- Free lane. The scratch Agent exists on the stock Claude Adapter.

**Steps**

1. Stop the app (`P-STOP`). Start it with the empty-string list on the command line: `dotnet run --project src/Huddle.App -- --urls http://localhost:5100 --Team:Acp:HiddenModes:0=`. Keep `Team__Acp__Enabled` at `false` as `P-LAUNCH-FREE` sets it. Do **not** write `$env:Team__Acp__HiddenModes__0 = ''`: PowerShell deletes a variable assigned an empty string.
2. Open the scratch Agent's card in Edit, open the **Work mode** select and write down every option.
3. Stop the app and start it again with `P-LAUNCH-FREE` unchanged. Open the same select.

**Pass if — all of these**

- Step 2 also lists **Plan**, **Auto** and **Bypass permissions**, after the two of WORKMODE-01.
- Step 3 is back to WORKMODE-01's list, with all three hidden.

**Fail if — any of these**

- Step 2 is unchanged -> the empty string was read as absent and the default applied; record the exact flag used and the resulting `Team:Acp:HiddenModes` value.

### WORKMODE-05 — An Adapter that advertises no mode shows no picker

**Free** · about 5 min

*Proves the select is hidden when there is nothing to choose and nothing stored, rather than rendered empty. Needs an Adapter that advertises no `mode` (setup step 2): the free mock adapter. Spec MW-9.*

**Before you start**

- `P-LAUNCH-UAT` (free). **Ada**, on the mock adapter, has no `persona_work_modes` row. That the mock advertises no mode is read from the code (`FakeAcpAgent.cs:262-266`) and not yet seen on screen; if the select does appear, record the options it lists and mark the test *Inconclusive*.

**Steps**

1. Open Ada's card in Edit and wait for the catalog to be read.
2. Look for a **Work mode** select, and for a **Work mode** row in View.

**Pass if — all of these**

- No Work mode select is shown, and no empty one.
- View shows no Work mode row that claims a value; at most `Agent default`.

**Fail if — any of these**

- An empty Work mode select is shown -> the hide rule (no mode advertised **and** nothing stored) is not applied.

### WORKMODE-06 — Accept edits: no permission request for an edit

**Paid** 💰 · about 15 min

*Proves a Work Mode changes the wire: under `acceptEdits` the Adapter stops asking about edits inside the Work Dir. [Roadmap §20](../roadmap.md) measured two requests, a `Write` and an `Edit`, in the default mode. Spec MW-2.*

**Before you start**

- Paid lane (`P-LAUNCH-PAID`) with `Team__Acp__TraceWire` set (setup step 4). The scratch Agent is Online, Haiku / low, with Work mode **Accept edits** saved, and a direct Room with it is open.
- `T-A` shows the app log (`O-LOG`).

**Steps**

1. Send `Create a file named wm-scratch.txt containing the single word first, then change the word to second. Reply done.`
2. When it replies, search the trace in `T-A` for `session/request_permission` and for `session/set_config_option`.
3. Open `wm-scratch.txt` in the Agent's Work Dir.

**Pass if — all of these**

- Step 2 finds a `session/set_config_option` for the mode, set to `acceptEdits`, and its response shows `acceptEdits` as the current value.
- Step 2 finds **no** `session/request_permission` for the create or the edit.
- Step 3 shows the file holding `second`.

**Fail if — any of these**

- A `session/request_permission` appears for either call -> the mode was not applied or was clamped; read the `set_config_option` response's `currentValue` and the log for a clamp warning.
- No `set_config_option` for the mode -> the Work Mode never left the app; check `persona_work_modes` and the effective mode.

**Measured outside the app, 2026-10-01.** A script drove the real `claude-agent-acp` 0.75.1 over stdio on Haiku (one run, Windows, Claude subscription). Under `acceptEdits`, an edit inside the Work Dir produced **no** `session/request_permission`, the file was written and the Turn ended `end_turn`. The in-app run passed on 2026-10-01 (see the [tracker](tracker.md#work-mode)), on a resumed session and with a second file name.

### WORKMODE-07 — Plan: nothing is written, and the request to leave plan mode is refused

**Paid** 💰 · about 15 min

*Proves the plan guard: a Persona in `plan` mode cannot leave it, because `WorkDirPermissionHandler`'s prefer-allow rule would otherwise exit plan mode on the first request. Also records **OQ-2**, which a script answered outside the app on 2026-10-01: the plan does **not** reach the Room as text, which is why `plan` is hidden by default. This test confirms that in the app. Spec MW-3.*

**Before you start**

- Paid lane with `Team__Acp__TraceWire` set. `plan` is hidden by default, so also lift the block in the same throwaway terminal, hiding only the other two: `$env:Team__Acp__HiddenModes__0 = 'bypassPermissions'` and `$env:Team__Acp__HiddenModes__1 = 'auto'` (a list replaces the default exactly). Close the terminal afterwards.
- The scratch Agent is Online, Haiku / low, with Work mode **Plan** saved (it is offered only with the block lifted). No `plan-scratch.py` exists in its Work Dir.
- `T-A` shows `O-LOG`.

**Steps**

1. Send `Add a function named add to plan-scratch.py that returns the sum of two numbers.`
2. Wait for the reply and read it in full.
3. Search the trace for a permission request of kind `switch_mode` and for the outcome sent back.
4. Search `O-LOG` for `Refused a request to leave plan mode`.
5. Check the Work Dir for `plan-scratch.py`.

**Pass if — all of these**

- Step 5 finds **no** file.
- Step 3 shows one `switch_mode` request (the Adapter's "Approve Plan" call) answered with a reject outcome, and step 4 finds the log line.
- Step 3 shows the Turn ending with stop reason `cancelled`, and step 2 shows **no plan text** in the Room: the Agent posts no reply, because `RoomSession` does not post a cancelled Turn. Write down exactly what the Room shows. This is the expected result until phase 3 captures the plan from the tool call's `rawInput.plan` ([Known limits](../known-limits.md)).

**Fail if — any of these**

- The file exists, or the `switch_mode` request was answered `allow_once` / `allow_always` -> the Persona left plan mode; the guard did not wrap the handler.
- The Room shows the plan in words -> not a guard failure, but it differs from the 2026-10-01 measurement; record it verbatim, because it changes the OQ-2 answer in the spec.

**Measured outside the app, 2026-10-01.** A script drove the real adapter on Haiku (one run, Windows, Claude subscription). The model used `Find` and `ToolSearch`, then the Adapter sent a `switch_mode` request titled "Approve Plan" with the whole plan in `rawInput.plan`. The guard refused with `reject_once`, no file was written, the Turn ended `cancelled`, and the only assistant text was 126 characters of preamble, not the plan. The in-app run passed on 2026-10-01 with the same outcome (see the [tracker](tracker.md#work-mode)).

### WORKMODE-08 — Accept edits: a write into `~/.claude` is still refused

**Paid** 💰 · about 15 min

*Proves **OQ-3**: under `acceptEdits`, an edit **outside** the Work Dir still reaches the permission handler, so the `~/.claude` write guard survives. If it does not, the mode has quietly removed the only check on that folder. Spec MW-4.*

**Before you start**

- Paid lane with `Team__Acp__TraceWire` set. The scratch Agent is Online, Haiku / low, with Work mode **Accept edits** saved.
- `~/.claude/huddle-wm-test.txt` does not exist: `Test-Path "$HOME\.claude\huddle-wm-test.txt"` prints False.

**Steps**

1. Send `Create the file .claude/huddle-wm-test.txt in my home directory containing the word gamma.`
2. Wait for the reply and read it.
3. Run `Test-Path "$HOME\.claude\huddle-wm-test.txt"`.
4. Search the trace for the permission request for that path and the outcome sent back, and `O-LOG` for the handler's refusal.

**Pass if — all of these**

- Step 3 prints False. **This is OQ-3.**
- Step 4 shows a `session/request_permission` for the path answered with a reject, and the handler's refusal in `O-LOG`: `Refused a tool call writing to '<path>': it is inside the agent's own configuration directory '<directory>'.` (`WorkDirPermissionHandler.cs:169-172`).

**Fail if — any of these**

- Step 3 prints True **and** step 4 shows a `session/request_permission` for the path that was approved, or no request at all -> the guard was bypassed. **Delete that file by hand**, then record the trace: either no request arrived (the Adapter wrote it under `acceptEdits`) or the handler approved it.

**Inconclusive if**

- Step 3 prints True but step 4 shows **no** write-tool request for the path and the tool-call row (`O-TOOLCALLS`) names `Bash`: the model wrote the file through a shell command. The guard reads only the path arguments of a write tool (`WorkDirPermissionHandler.cs:318-350`), so a shell write is outside it. This is the known limit that the Work Dir is not a jail, not a Work Mode bypass. **Delete the file by hand**, and run the test again with the instruction "Use the Write tool".

**Measured outside the app, 2026-10-01.** A script drove the real adapter on Haiku (one run, Windows, Claude subscription). Under `acceptEdits`, a write to a path **outside** the Work Dir still produced a `session/request_permission` (kind `edit`, title "Write <path>", `rawInput` `{file_path, content}`, options `allow_once`, `allow_always`, `reject_once`). Refused, the file was not written. `WorkDirPermissionHandler` reads `rawInput.file_path`, so its refusal still sees such writes. Caveats: the path was a temp directory, not `~/.claude` itself, and `allow_always` was never chosen. The in-app run against `~/.claude` passed on 2026-10-01 (see the [tracker](tracker.md#work-mode)); its first two runs failed because `SerializeRaw` turned `rawInput` into `{"file_path":[]}`, so the guard never saw a path (a bug that predates Work Modes, fixed since). If this test fails with the file created, check that fix first.

### WORKMODE-09 — A mode is applied again after an app restart

**Paid** 💰 · about 15 min

*Proves the mode is re-applied after every resume. **OQ-1** was answered free on 2026-09-30: a resumed session comes back in Manual, not the mode it was set to, so without a second `set_config_option` the Agent would silently be in Manual after every restart. Spec MW-5.*

**Before you start**

- Paid lane with `Team__Acp__TraceWire` set. The scratch Agent is Online, Haiku / low, with Work mode **Accept edits** saved.

**Steps**

1. Send `Reply with the word one.` and wait for the reply.
2. Restart the app: `P-STOP`, then the `dotnet run` line of `P-LAUNCH-PAID`, with `Team__Acp__TraceWire` still set in that terminal.
3. When the scratch Agent is Online, send `Reply with the word two.` and wait for the reply.
4. In the trace of the second run, find the `session/resume` and the next `session/set_config_option`.

**Pass if — all of these**

- Step 4 shows `session/resume` for the Room's stored session id, then a `session/set_config_option` for the mode set to `acceptEdits`, before the prompt for step 3.
- Step 3 is answered normally.

**Fail if — any of these**

- `session/resume` is followed by no mode request -> the Agent is running in Manual after every restart while its card says Accept edits.
- There is no `session/resume` at all -> the stored entry no longer matched (Adapter, Model, Effort **and** Work Mode); check that none changed between runs.

Run `O-ADAPTERS` after the last paid test, expecting `0`, and close the throwaway terminal that held `Team__Acp__TraceWire`.