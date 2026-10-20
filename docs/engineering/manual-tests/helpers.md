# Helpers

Prove, in a real browser, that the Needs Human Helper turns the Human gate on for a Task a Teammate wrote that leaves the team, leaves a harmless Task alone, never judges a Task the Human turned the gate off on, pauses at its daily cap and asks before it spends more, and fails without touching the Task; and that Settings › Helpers shows and saves all of that. The automated suite proves the dispatcher, the budget, the trigger, the Helper's apply step and the card with a fake process in place of the adapter (no test may reach the real runner, [Rules](../rules.md)); this page proves what only the real adapter and a browser show: that a Haiku call returns JSON the Helper can read, what the card prints, and that a restart loses nothing. The design is [the Helpers spec](../../Huddle.Helpers-Specifications.md); its use cases U-1 to U-12 map to the tests below, and the decision record is [ADR-0064](../../adr/0064-a-helper-judges-an-event-and-the-app-applies-its-verdict.md).

**7 tests** · 3 free · 4 paid 💰 · about 1.2 hours. Written 2026-10-20; **not yet run by a person**. The real adapter was driven once, by the HL-0 spike on 2026-10-20 (Haiku at low effort: one call cost $0.0024 with the toolless session settings); none of the tests below has been run end to end.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build). The paid tests need the **real** Claude adapter, not the mock cast: run `tools/acp/install.ps1` once, sign in to Claude on this computer, and launch the app with `Team:Acp:Enabled` set to true and the default Adapter. Each paid call is one Haiku call at low effort, a fraction of a cent. This area adds:

1. Hire or have two Teammates **Online** on `/teammates`: **Leo**, who writes Tasks, and **Miriam**, a second assignee. Both may run on the real adapter; their own Turns are not what this page tests.
2. Open **Settings › Helpers** and note the **Needs Human** card: **On**, `0 of 50 calls today`.
3. Task files are `{DataDir}\Teams\<Team>\_tasks\<ID>.md`; the Helper's choices and counts are in `{DataDir}\team.db` (tables `helper_settings`, `helper_usage`, `helper_status`, `needs_human_overrides`).
4. The exact interface strings below are those of the Helpers tab, the Task panel and the Change log. A pass means the text matches, not just the meaning.

## Tests

### HELPERS-01 — The Helpers tab shows the Needs Human card (U-1 setup)

**Free** · about 8 min

*Proves the tab, the card's parts, the prompt link and the daily cap field.*

**Before you start**

- Setup is done. No Helper has run today.

**Steps**

1. Open **Settings** and read the section menu. Open **Helpers**.
2. Read the intro, the line under it, and the **Needs Human** card from top to bottom.
3. Clear the **Daily cap** field and type `0`, then leave the field.
4. Press **Edit the prompt**.

**Pass if — all of these**

- The menu reads Profile, Prompts, Helpers, Appearance, Personas, Skills, Team types, Library, and the address is `/settings/helpers`.
- The intro reads `Helpers are small background agents that make one judgement and let Huddle act on it. They don't chat, keep memory or use tools.`; the next line shows `Stored at` and the path of `team.db`.
- The card shows the chip `On`, the **On** switch, **Adapter** (when more than one is configured), **Model**, **Effort**, **Daily cap** `50` with the helper text `Calls a day before it pauses and asks you.`, the link `Edit the prompt`, `0 of 50 calls today`, and the buttons **Save** (disabled) and **Reset to defaults**.
- At step 3 the field does not keep `0`: it returns to a value of 1 or more.
- At step 4 Prompts opens with the `Helpers` group and `Needs Human: system prompt` scrolled to and flashing once.

### HELPERS-02 — Save and Reset to defaults, and a restart keeps both (U-7 setup)

**Free** · about 10 min

*Proves Save, Reset to defaults and that the Human's row survives a restart.*

**Before you start**

- HELPERS-01 passed. Needs Human is at its defaults.

**Steps**

1. Set **Daily cap** to `7` and switch **On** off. Read the **Save** button, then press it. Read the chip.
2. Stop the app and relaunch it with the same launch command.
3. Open **Settings › Helpers** and read the card.
4. Press **Reset to defaults** and read the card.

**Pass if — all of these**

- At step 1 **Save** enables as soon as the cap or the switch differs, and after it the chip reads `Off`.
- At step 3 the card still shows the cap `7`, the switch off and the chip `Off`.
- At step 4 the cap returns to `50`, the switch to on, and the chip to `On`.

### HELPERS-03 — A Teammate's Task that leaves the team gets the gate (U-1, U-5)

**Paid 💰** · about 12 min · one Haiku call

*Proves U-1: the judgement, the Change log line by `Needs Human (Helper)`, the wake, the Task panel line and the card's last run.*

**Before you start**

- Needs Human is at its defaults and **On**. Leo and Miriam are Online.

**Steps**

1. In Leo's Room, ask Leo to create a Task assigned to Miriam: `Reply to Acme about the renewal price`, with no Action.
2. Wait up to 30 seconds. Open the Task and read the **Human gate** section and its Change log.
3. Open Miriam's Room.
4. Open **Settings › Helpers**.

**Pass if — all of these**

- At step 2 the switch `Requires your acceptance` is on, with the note `Turned on by the Needs Human Helper:` followed by a reason, then `If you turn it off, the Helper won't ask about this Task again.`
- The last Change log entry reads `human gate on (reason: …)`, by `Needs Human (Helper)`, and the Task's other fields are unchanged.
- At step 3 Miriam was woken with a Message that says the Task was changed by `Needs Human (Helper)` and lists the change; the Message appears to be from the Human.
- At step 4 the card reads `1 of 50 calls today`, and `Last run` with the time and `Turned the gate on for <id>`; no `Last error`.

### HELPERS-04 — A harmless Task is left alone (U-2, U-4, U-11)

**Paid 💰** · about 10 min · one Haiku call

*Proves U-2: a no leaves the Task untouched, still costs a call, and a status move or Evidence line costs none.*

**Before you start**

- HELPERS-03 passed; note the count on the card.

**Steps**

1. Ask Leo to create a Task: `Summarise the interview notes`, no Action. Wait up to 30 seconds.
2. Open the Task and read its Change log. Open the card on **Settings › Helpers**.
3. Ask Leo to move the Task to In Progress, then to add an Evidence line. Wait 30 seconds and read the card again.

**Pass if — all of these**

- At step 2 the gate is off and the Change log has only Leo's own entries; the card's count went up by one and `Last run` reads `Judged <id>: no change`.
- At step 3 the count has not moved and `Last run` is unchanged.

### HELPERS-05 — The Human's override is final (U-5)

**Paid 💰** · about 8 min · one Haiku call

*Proves U-5: after the Human turns the gate off, the Helper never judges that Task again.*

**Before you start**

- The Task from HELPERS-03 has the gate on. Note the card's count.

**Steps**

1. Open that Task and switch `Requires your acceptance` off, then save.
2. Ask Leo to rewrite its title to `Send Acme the new contract`. Wait 30 seconds.
3. Stop and relaunch the app. Repeat step 2 with the title `Send Acme the signed contract`. Wait 30 seconds.
4. Open the card.

**Pass if — all of these**

- The gate stays off after both rewrites and the Change log shows no entry by `Needs Human (Helper)` after yours.
- The card's count has not moved, and `needs_human_overrides` in `team.db` holds the Task id.

### HELPERS-06 — The daily cap pauses the Helper and Continue allows a few more (U-7)

**Paid 💰** · about 15 min · up to four Haiku calls

*Proves U-7: the pause, the skipped count, Continue's bounded grant, and that Teammates' saves are still saved.*

**Before you start**

- Set **Daily cap** to `2` on the card and press **Save**. Note how many calls the card shows for today.

**Steps**

1. Ask Leo to create three Tasks, one after another, with different titles, such as `Draft notes A`, `Draft notes B` and `Draft notes C`. Wait 30 seconds after each.
2. Read the card.
3. Ask Leo for a fourth Task, `Draft notes D`, and wait 30 seconds. Read the card.
4. Press **Continue**. Ask Leo for a fifth Task, `Draft notes E`, and wait 30 seconds.

**Pass if — all of these**

- After the cap is reached the card shows the chip `Paused`, the warning `Paused — daily cap reached` and the button `Continue (+10 today)`.
- The Tasks Leo made while it was paused exist, and each woke Miriam; the card says `<n> skipped while paused`.
- After **Continue** the chip reads `On` again and `<calls> of 12 calls today`, and the fifth Task is judged.

### HELPERS-07 — A failure leaves the Task as saved and says why (U-8, U-10)

**Free** · about 10 min

*Proves U-8: an Adapter that cannot run is a card message, not a lost or changed Task.*

**Before you start**

- Set `Team:Helpers:NeedsHuman:Adapter` to `nope` in the configuration and relaunch, or save an Adapter on the card that is not installed.

**Steps**

1. Ask Leo to create a Task, `Reply to Acme about the renewal price`. Wait 30 seconds.
2. Open the Task, then the card.

**Pass if — all of these**

- At step 2 the Task is exactly as Leo saved it: the gate is off and the Change log has only Leo's entries.
- The card shows a Warning `Last error <time> · The Adapter 'nope' is not configured. Pick another on this card.` (or the installed-Adapter wording of your setup), and the count of calls today did not move.

---

Back to [the manual test script](../manual-tests.md).
