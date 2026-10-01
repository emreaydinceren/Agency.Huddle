# Work Mode

Prove, in a real browser against a real Adapter, that a Teammate's **Work Mode** is offered from what its Adapter advertises (with `Plan`, `Auto` and `Bypass permissions` hidden), is cleared by an Adapter change, is never sent when it is hidden, is lifted by `Team:Acp:HiddenModes`, and is absent for an Adapter that advertises none; and that the paid checks hold: `Accept edits` stops the permission request for an edit, `Plan` (once an operator offers it) writes nothing and its request to leave plan mode is refused, `Accept edits` still lets the `~/.claude` guard stop a write outside the Work Dir, and a mode survives an app restart. The automated suite proves the parts; this page proves they hold together on a screen, against an Adapter that really has modes. **Written 2026-09-30, none of these has been run in the app**: four of the nine tests spend money, because only a Turn shows what a mode changes. On 2026-10-01 a script drove the real adapter outside the app and measured the wire facts behind WORKMODE-06 to -08 (see each test); those runs answered OQ-2 and OQ-3 and made `plan` hidden by default, but this page is still the acceptance run.

**9 tests** · 5 free, 4 paid 💰 · about 2 hours.

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

1. Use a **scratch** Teammate on the stock Claude Adapter, Haiku, never one that matters. Haiku
   advertises no effort levels (the card says so), so Effort stays on `Model default`; "low" is not
   available for it. A mode
   change restarts it and forgets its Room Sessions, as a Model change does.
2. WORKMODE-02 and WORKMODE-05 need an Adapter Profile beyond the stock one: WORKMODE-02 needs two
   Adapters so the Adapter select renders, and WORKMODE-05 needs one that advertises **no** mode
   (`agency-acp` is the candidate). Without them each is *Inconclusive*, not a defect; see
   [adapters.md](adapters.md) for setting a second Profile up.
3. WORKMODE-03 writes to `persona_work_modes` by hand. If `sqlite3` is not on PATH, use the `Set-TeamDb`
   and `Get-TeamDb` helpers from [model-effort.md](model-effort.md) setup step 3.
4. The paid tests (WORKMODE-06 to -09) need **`Team__Acp__TraceWire`**, because the pass condition is
   the wire trace and not only the reply. Follow the
   [§0.2](../manual-tests.md#02-the-cost-guard) warning as [file-changes.md](file-changes.md) does: a
   throwaway terminal, `$env:Team__Acp__TraceWire = 'true'` set before the `dotnet run` line of
   `P-LAUNCH-PAID`, closed afterwards, never set for any other test. **That alone prints nothing:** the
   wire trace is written at `Trace` level and the Development log shows `Debug`, so also set
   `[Environment]::SetEnvironmentVariable('Logging__LogLevel__Agency.Huddle.Acp.Wire', 'Trace', 'Process')`
   (found on the 2026-10-01 run; without it `O-LOG` has no `session/*` lines at all). It prints the tool
   server's bearer token into `O-LOG`. To search it, redirect the app's output to a file and use
   `Select-String`; a request is `"method": "session/..."` on its own line, with its body after it.
5. "A scratch file" in the paid tests means a file under the Teammate's Work Dir
   (`src\Huddle.App\App_Data\Teammates\<Name>\work\` by default; the Persona's own file sits one
   level up), so the test never touches anything that matters.
6. "Restart the app" always means `P-STOP` then the `dotnet run` line for the lane the test names.

## Tests

### WORKMODE-01 — The picker lists what the Adapter advertises, minus Plan, Auto and Bypass permissions

**Free** · about 10 min

*Proves the picker is filled from the Adapter's own list, that the default hidden list takes `plan`, `auto` and `bypassPermissions` out of it (Windows is not root, so the Adapter advertises all three), and that the blank option is the Adapter's default. `plan` is hidden because a refused plan exit leaves a plan Persona silent in the Room (WORKMODE-07). Spec MW-1.*

**Before you start**

- Free lane (`P-LAUNCH-FREE`). The scratch Teammate exists on the stock Claude Adapter. `Team__Acp__HiddenModes` is not set (`Get-ChildItem Env:Team__*`).

**Steps**

1. Open `/teammates` and open the scratch Teammate's card in Edit.
2. Find the **Work mode** select, directly after Effort. Note its state while the catalog is being read.
3. Open it and write down every option's visible text, in order.
4. Pick **Accept edits** and read the helper text under the select. Do not save; close the card.

**Pass if — all of these**

- Step 2 shows the select disabled (not merely hinted) while the probe is out, then enabled.
- Step 3 lists the blank option **Use the agent's default**, then **Manual** and **Accept edits**, in the Adapter's names. **Plan**, **Auto** and **Bypass permissions** are absent.
- Step 4's helper text is the Adapter's own description of Accept edits, not a Huddle sentence.

**Fail if — any of these**

- Plan, Auto or Bypass permissions is listed -> the default hidden list is not applied on the way out of the catalog.
- Manual is missing -> `default` is being filtered as Effort's sentinel is; it is a real mode here.

### WORKMODE-02 — Changing the Adapter clears the Work Mode and says so

**Free** · about 10 min

*Proves ADR-0033 extends ADR-0015: an Adapter change clears the Work Mode with its own `role="status"` note, separate from the Model and Effort note. Needs two Adapter Profiles (setup step 2). Spec MW-6.*

**Before you start**

- Free lane. The scratch Teammate has Work mode **Accept edits** saved (pick it, Save, reopen the card and confirm it shows). A second Adapter Profile is configured.

**Steps**

1. Open the scratch Teammate's card in Edit and change the **Adapter** to the other Profile. Do not save yet.
2. Read the Work mode select and look for a note about it. Inspect the note's element for its `role`.
3. Save, reopen the card in View, and read the **Work mode** row.
4. Read the table: `Get-TeamDb "SELECT * FROM persona_work_modes"` (or the same query through `sqlite3`).

**Pass if — all of these**

- Step 2 shows the select back on the blank option, with a note that begins `Changing the adapter reset Work mode to its default`.
- The note's `role` is `status`, not `alert`, and it is a separate element from the Model and Effort note.
- Step 3 reads `Agent default`.
- Step 4 shows no row for the scratch Teammate.

**Fail if — any of these**

- The Work Mode survives the Adapter change -> a mode id from one Adapter is being sent to another, which will not resolve.
- The mode clears with no note -> two controls changing themselves silently is the shape ADR-0015 rejected.
- The note is `role="alert"` -> it interrupts a deliberate action.

### WORKMODE-03 — A hand-written row for a hidden mode is dropped, with a warning

**Free** · about 15 min

*Proves the hidden list is enforced where the options are built, not only in the picker: a `bypassPermissions` row written straight into the database never reaches the Adapter. Spec MW-7.*

**Before you start**

- Restart lane: the free lane without the guard, so Teammate runners start (clear `Team__Acp__Enabled` from the `Env:` drive, then the `dotnet run` line, as in [model-effort.md](model-effort.md) setup step 6). Starting a session is not a prompt turn; **do not type a Message into any Room**. `Team__Acp__HiddenModes` is not set.
- `T-A` shows `O-LOG`. The app is stopped for step 1.

**Steps**

1. Run `Set-TeamDb "INSERT OR REPLACE INTO persona_work_modes(persona_name, work_mode) VALUES('scratch','bypassPermissions')"`, using the Teammate's real Name (or the same statement through `sqlite3`).
2. Start the app in the restart lane and wait for the scratch Teammate to read Online.
3. Search `O-LOG` for `bypassPermissions`.
4. Open the Teammate's card in View and read the **Work mode** row.

**Pass if — all of these**

- Step 3 finds one warning of the form `Persona 'scratch' has Work Mode 'bypassPermissions', which is hidden; starting in the Adapter's own mode.`
- Step 2 reaches Online: the stale row does not brick the Teammate.
- If `Team__Acp__TraceWire` was set for this run, the trace holds no `session/set_config_option` for `mode`. Without it, record that half as not-checked; the warning settles the test.

**Fail if — any of these**

- No warning and the Teammate starts in Bypass permissions -> the policy is applied only in the picker; this is the safety hole the test exists for.
- The Teammate is Degraded or Offline -> a stale stored mode is bricking a Persona.

Delete the row afterwards: `Set-TeamDb "DELETE FROM persona_work_modes"`.

### WORKMODE-04 — Hiding nothing is a list of one empty string

**Free** · about 10 min

*Proves `Team:Acp:HiddenModes` lifts the default block, and records the configuration trap: `[]` cannot hide nothing, because configuration cannot express an empty list. The spec's `[]` wording is corrected here. Spec MW-8.*

**Before you start**

- Free lane. The scratch Teammate exists on the stock Claude Adapter.

**Steps**

1. Stop the app (`P-STOP`). Start it with the empty-string list on the command line: `dotnet run --project src/Huddle.App -- --urls http://localhost:5100 --Team:Acp:HiddenModes:0=`. Keep `Team__Acp__Enabled` at `false` as `P-LAUNCH-FREE` sets it. Do **not** write `$env:Team__Acp__HiddenModes__0 = ''`: PowerShell deletes a variable assigned an empty string.
2. Open the scratch Teammate's card in Edit, open the **Work mode** select and write down every option.
3. Stop the app and start it again with `P-LAUNCH-FREE` unchanged. Open the same select.

**Pass if — all of these**

- Step 2 also lists **Plan**, **Auto** and **Bypass permissions**, after the two of WORKMODE-01.
- Step 3 is back to WORKMODE-01's list, with all three hidden.

**Fail if — any of these**

- Step 2 is unchanged -> the empty string was read as absent and the default applied; record the exact flag used and the resulting `Team:Acp:HiddenModes` value.

### WORKMODE-05 — An Adapter that advertises no mode shows no picker

**Free** · about 5 min

*Proves the select is hidden when there is nothing to choose and nothing stored, rather than rendered empty. Needs an Adapter that advertises no `mode` (setup step 2). Spec MW-9.*

**Before you start**

- Free lane. A scratch Teammate on that Adapter, with no `persona_work_modes` row.

**Steps**

1. Open the scratch Teammate's card in Edit and wait for the catalog to be read.
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

- Paid lane (`P-LAUNCH-PAID`) with `Team__Acp__TraceWire` set (setup step 4). The scratch Teammate is Online, Haiku / low, with Work mode **Accept edits** saved, and a direct Room with it is open.
- `T-A` shows the app log (`O-LOG`).

**Steps**

1. Send `Create a file named wm-scratch.txt containing the single word alpha, then change the word to beta. Reply done.`
2. When it replies, search the trace in `T-A` for `session/request_permission` and for `session/set_config_option`.
3. Open `wm-scratch.txt` in the Teammate's Work Dir.

**Pass if — all of these**

- Step 2 finds a `session/set_config_option` for the mode, set to `acceptEdits`, and its response shows `acceptEdits` as the current value.
- Step 2 finds **no** `session/request_permission` for the create or the edit.
- Step 3 shows the file holding `beta`.

**Fail if — any of these**

- A `session/request_permission` appears for either call -> the mode was not applied or was clamped; read the `set_config_option` response's `currentValue` and the log for a clamp warning.
- No `set_config_option` for the mode -> the Work Mode never left the app; check `persona_work_modes` and the effective mode.

**Measured outside the app, 2026-10-01.** A script drove the real `claude-agent-acp` 0.75.1 over stdio on Haiku (one run, Windows, Claude subscription). Under `acceptEdits`, an edit inside the Work Dir produced **no** `session/request_permission`, the file was written and the Turn ended `end_turn`. The in-app run above is still to do.

### WORKMODE-07 — Plan: nothing is written, and the request to leave plan mode is refused

**Paid** 💰 · about 15 min

*Proves the plan guard: a Persona in `plan` mode cannot leave it, because `WorkDirPermissionHandler`'s prefer-allow rule would otherwise exit plan mode on the first request. Also records **OQ-2**, which a script answered outside the app on 2026-10-01: the plan does **not** reach the Room as text, which is why `plan` is hidden by default. This test confirms that in the app. Spec MW-3.*

**Before you start**

- Paid lane with `Team__Acp__TraceWire` set. `plan` is hidden by default, so also lift the block in the same throwaway terminal, hiding only the other two: `$env:Team__Acp__HiddenModes__0 = 'bypassPermissions'` and `$env:Team__Acp__HiddenModes__1 = 'auto'` (a list replaces the default exactly). Close the terminal afterwards.
- The scratch Teammate is Online, Haiku / low, with Work mode **Plan** saved (it is offered only with the block lifted). No `plan-scratch.py` exists in its Work Dir.
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
- Step 3 shows the Turn ending with stop reason `cancelled`, and step 2 shows **no plan text** in the Room: the Teammate posts no reply, because `RoomSession` does not post a cancelled Turn. Write down exactly what the Room shows. This is the expected result until phase 3 captures the plan from the tool call's `rawInput.plan` ([Known limits](../known-limits.md)).

**Fail if — any of these**

- The file exists, or the `switch_mode` request was answered `allow_once` / `allow_always` -> the Persona left plan mode; the guard did not wrap the handler.
- The Room shows the plan in words -> not a guard failure, but it differs from the 2026-10-01 measurement; record it verbatim, because it changes the OQ-2 answer in the spec.

**Measured outside the app, 2026-10-01.** A script drove the real adapter on Haiku (one run, Windows, Claude subscription). The model used `Find` and `ToolSearch`, then the Adapter sent a `switch_mode` request titled "Approve Plan" with the whole plan in `rawInput.plan`. The guard refused with `reject_once`, no file was written, the Turn ended `cancelled`, and the only assistant text was 126 characters of preamble, not the plan. The in-app run above is still to do.

### WORKMODE-08 — Accept edits: a write into `~/.claude` is still refused

**Paid** 💰 · about 15 min

*Proves **OQ-3**: under `acceptEdits`, an edit **outside** the Work Dir still reaches the permission handler, so the `~/.claude` write guard survives. If it does not, the mode has quietly removed the only check on that folder. Spec MW-4.*

**Before you start**

- Paid lane with `Team__Acp__TraceWire` set. The scratch Teammate is Online, Haiku / low, with Work mode **Accept edits** saved.
- `~/.claude/huddle-wm-test.txt` does not exist: `Test-Path "$HOME\.claude\huddle-wm-test.txt"` prints False.

**Steps**

1. Send `Create the file .claude/huddle-wm-test.txt in my home directory containing the word gamma.`
2. Wait for the reply and read it.
3. Run `Test-Path "$HOME\.claude\huddle-wm-test.txt"`.
4. Search the trace for the permission request for that path and the outcome sent back, and `O-LOG` for the handler's refusal.

**Pass if — all of these**

- Step 3 prints False. **This is OQ-3.**
- Step 4 shows a `session/request_permission` for the path answered with a reject, and the handler's refusal in `O-LOG`.

**Fail if — any of these**

- Step 3 prints True -> the guard was bypassed. **Delete that file by hand**, then record the trace: either no request arrived (the Adapter wrote it under `acceptEdits`) or the handler approved it.

**Measured outside the app, 2026-10-01.** A script drove the real adapter on Haiku (one run, Windows, Claude subscription). Under `acceptEdits`, a write to a path **outside** the Work Dir still produced a `session/request_permission` (kind `edit`, title "Write <path>", `rawInput` `{file_path, content}`, options `allow_once`, `allow_always`, `reject_once`). Refused, the file was not written. `WorkDirPermissionHandler` reads `rawInput.file_path`, so its refusal still sees such writes. Caveats: the path was a temp directory, not `~/.claude` itself, and `allow_always` was never chosen. The in-app run above, against `~/.claude`, is still to do.

### WORKMODE-09 — A mode is applied again after an app restart

**Paid** 💰 · about 15 min

*Proves the mode is re-applied after every resume. **OQ-1** was answered free on 2026-09-30: a resumed session comes back in Manual, not the mode it was set to, so without a second `set_config_option` the Teammate would silently be in Manual after every restart. Spec MW-5.*

**Before you start**

- Paid lane with `Team__Acp__TraceWire` set. The scratch Teammate is Online, Haiku / low, with Work mode **Accept edits** saved.

**Steps**

1. Send `Reply with the word one.` and wait for the reply.
2. Restart the app: `P-STOP`, then the `dotnet run` line of `P-LAUNCH-PAID`, with `Team__Acp__TraceWire` still set in that terminal.
3. When the scratch Teammate is Online, send `Reply with the word two.` and wait for the reply.
4. In the trace of the second run, find the `session/resume` and the next `session/set_config_option`.

**Pass if — all of these**

- Step 4 shows `session/resume` for the Room's stored session id, then a `session/set_config_option` for the mode set to `acceptEdits`, before the prompt for step 3.
- Step 3 is answered normally.

**Fail if — any of these**

- `session/resume` is followed by no mode request -> the Teammate is running in Manual after every restart while its card says Accept edits.
- There is no `session/resume` at all -> the stored entry no longer matched (Adapter, Model, Effort **and** Work Mode); check that none changed between runs.

Run `O-ADAPTERS` after the last paid test, expecting `0`, and close the throwaway terminal that held `Team__Acp__TraceWire`.