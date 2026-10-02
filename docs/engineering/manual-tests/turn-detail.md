# Turn detail and Spend

Prove, in a real browser against a real Adapter, that a Draft shows its recent tool calls as rows, that an edit opens to an Edit preview while the Turn runs, that a Teammate's card shows the Spend its Adapter reported and that the figure matches the Adapter's own, and that the view holds up at phone width, under different Themes and through a burst of fast calls. The automated suite proves the parts; this page proves they hold together on a screen, against wire data a fake cannot invent. **Written 2026-09-30, not run**: four of the five tests spend money, because nothing shows a tool call without a Turn.

**5 tests** · 1 free, 4 paid 💰 · about 1.5 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below. The
design is [the Turn detail spec](../../Huddle.TurnDetail-Specifications.md); the copy these tests
assert is its Appendix D.5.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named in each test from
[Common procedures](common.md). This area adds:

1. Use a **scratch** Teammate, never one that matters. A Model or Effort change restarts a
   Teammate and clears what it remembers, and every paid test below asks it to edit a file.
2. Put one small text file in the scratch Teammate's Work Dir (`notes.md`, a few lines, one of which
   contains a typo such as `Teh meeting is on Friday.`).
3. Keep the Teammate's Effort at its default. `claude-agent-acp` sends no Plan or thinking at the
   default Effort, which is what these tests assume; do not raise it here.
4. "Restart the app" always means `P-STOP` then the `dotnet run` line for the lane the test names.
   Spend is in memory, so a restart starts it from zero, which TURNDETAIL-02 relies on.

## Tests

### TURNDETAIL-01 — An edit shows a live, expandable preview in a Room

**Paid** 💰 · about 20 min

*Proves the whole path on real wire data: the Adapter's `diff` and `locations` reach the runner, cross the pipe on `ToolActivity`, land on the Draft and render as a row the Human can open. Also proves the row disappears with the Draft.*

**Before you start**

- Paid lane (`P-LAUNCH-PAID`). The scratch Teammate is Online and its Work Dir holds `notes.md` with the typo.
- A direct Room with the scratch Teammate is open in the browser, and `T-A` is showing the app log.

**Steps**

1. Type `Fix the typo in notes.md and tell me what you changed.` and send it.
2. While the Draft streams, watch the area under its text. Write down each row that appears, in order, with its status label.
3. When an `Edit` or `Write` row is present, press its expander (the small chevron at the end of the row). Do not press anything else.
4. Read the opened preview: the path in its header, the **Removed** block, the **Added** block.
5. Keep the preview open and wait for the Turn to finish.
6. Press **Stop** on a second, identical request about a different typo, before the reply completes, and watch the rows.

**Pass if — all of these**

- Rows appear under the streaming text, oldest first, never reordering, each with a status label of exactly `Waiting`, `Running`, `Done` or `Failed` (read from the element's `aria-label`).
- The edit row has an expander whose accessible name reads `Show change to notes.md`, and `Hide change to notes.md` once open.
- The opened preview's header is the full path, **Removed** shows the typo line with a `−`, **Added** shows the corrected line with a `+`, and neither block carries a note.
- Opening the row does not scroll the Room.
- When the Turn ends the whole list goes with the Draft; the posted reply contains only text.
- Stopping the second Turn also removes its list.

**Fail if — any of these**

- Rows appear only after the Turn ends -> the Draft is not being re-read on `DraftChanged`.
- The row shows a title but never an expander on an edit -> the `diff` is not reaching the Draft; check `O-WIRE` for `edit` on the `toolActivity` line.
- The preview shows the same text in both blocks, or an empty block -> the mapper is reading the wrong side, or a write is being treated as an edit.
- Opening the preview jumps the Room to the bottom -> the scroll signature depends on view state.

### TURNDETAIL-02 — Spend equals the Adapter's own figure across two Turns

**Paid** 💰 · about 25 min

*Proves the running-total rule: the card's figure is the Adapter's own running total for this run, not the sum of the totals it reported.*

**Before you start**

- Paid lane, app just restarted, so Spend is zero. The scratch Teammate is Online.
- `T-A` has `Team:Acp:TraceWire` set so the `usage_update` lines are in the log (`O-WIRE`).

**Steps**

1. Open the Teammate's card from `/teammates`. Confirm there is **no** `Spent since start` line.
2. Send the Teammate a one-word request (`Reply with the word yes.`) in a direct Room. Wait for the reply.
3. Reopen the card. Read the Spend line.
4. In the trace, find the `cost` on the last `usage_update` of that Turn.
5. Send a second one-word request. Wait for the reply, reopen the card, read the line, and read the new `cost` in the trace.
6. Keep the card open, send a third request from another browser tab, and watch the line.

**Pass if — all of these**

- Step 1 shows no Spend line at all, and no zero.
- After step 3 the line reads `Spent since start: {amount} USD`, with the amount to three decimals and equal to the traced `cost.amount` rounded to three decimals.
- After step 5 the amount equals the **second** traced total, not the sum of the two.
- The line updates by itself in step 6, without closing the card.

**Fail if — any of these**

- The figure is roughly double what the trace says -> a running total is being added as if it were a per-Turn amount.
- The line never appears while the trace shows a `cost` -> the cost is dropped in the mapper or the runner.
- The line only updates after the card is reopened -> the card is not subscribed to `SpendChanged`.

### TURNDETAIL-03 — A Persona on `agency-acp` shows rows with no preview and no Spend line

**Free** · about 15 min

*Proves the feature degrades quietly for an Adapter that sends no `diff`, no `locations` and no `cost`: a row is a title and a status, and nothing is invented.*

**Before you start**

- Free lane. A scratch Teammate whose Adapter is `agency-acp` (see [adapters](adapters.md)) is Online, with a local model reachable.

**Steps**

1. Send the Teammate a request that makes it read a file.
2. Watch the Draft's rows.
3. Open its card.

**Pass if — all of these**

- Rows appear, each with a status label, and none has an expander.
- The card has no `Spent since start` line and no `0.000`.

**Fail if — any of these**

- A row shows an empty expander or an empty preview -> the view renders a container for an absent `Edit`.
- The card shows a zero -> an absent cost is being reported as zero.

### TURNDETAIL-04 — The list and preview at 320 px and under three Themes

**Paid** 💰 · about 25 min

*Proves the layout and colour rules in [Appendix D.6 and D.7](../../Huddle.TurnDetail-Specifications.md): no horizontal scroll at phone width, and every mark and border readable under a dark Theme, a light Theme and a High contrast Theme.*

**Before you start**

- Paid lane. Reproduce TURNDETAIL-01 until an edit row with an opened preview is on screen, then leave the Turn running (a longer request helps).

**Steps**

1. In DevTools, emulate a 320 px wide viewport. Read the row and the open preview.
2. Switch the Theme to one from each of three groups: a dark one, a light one and one from the High contrast group. Look at the status marks, the two borders and the text in the preview under each.
3. In the 320 px view press Tab until the expander has focus and press Enter, then Space.

**Pass if — all of these**

- At 320 px the page has no horizontal scrollbar; the preview's text wraps inside its block and a long block scrolls inside its own capped height.
- The expander is at least 44 px tall and its focus outline is visible.
- Under all three Themes every status mark shows by shape as well as colour, and the Removed and Added blocks are still told apart by their labels and `−` / `+` even where their borders are faint.
- Enter and Space both toggle the row and focus stays on the button.

**Fail if — any of these**

- The Room scrolls sideways at 320 px -> the preview is wider than its container.
- A border or mark disappears under one Theme -> a colour literal has crept into the stylesheet; run the theme tests.
- Focus leaves the button when it toggles -> the row is being re-created instead of re-rendered in place.

### TURNDETAIL-05 — A burst of ten quick tool calls neither reorders nor flickers the list

**Paid** 💰 · about 20 min

*Proves the two risks the automated tests cannot see: rows twitching when calls arrive and finish inside one frame, and `MudCollapse` animating on every re-render ([Appendix D.8](../../Huddle.TurnDetail-Specifications.md)).*

**Before you start**

- Paid lane. The scratch Teammate's Work Dir holds about ten small files.

**Steps**

1. Send `Read every file in your work folder, one at a time, then say done.`
2. Watch the list for the whole burst.
3. During the burst, open the first edit or read row that has an expander, if one exists, and watch whether its animation replays as further rows arrive.

**Pass if — all of these**

- Rows only ever append at the bottom; nothing already shown moves, and once six are shown the top row leaves as the next arrives.
- No row flashes or changes height unprompted.
- An opened row's animation plays once, on the click, and never again during the burst.

**Fail if — any of these**

- Rows jump up and down as statuses change -> the list is being rebuilt instead of patched by id.
- An open preview collapses and reopens as updates arrive -> open state is being lost on re-render.
