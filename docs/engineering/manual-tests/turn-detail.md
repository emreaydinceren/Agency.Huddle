# Turn detail and Spend

Prove, in a real browser against a real Adapter, that a Draft shows its recent tool calls as rows, that an edit opens to an Edit preview while the Turn runs, that an Agent's card shows the Spend its Adapter reported and that the figure behaves as the Adapter's own running total, and that the view holds up at phone width, under different Themes and through a burst of fast calls. The automated suite proves the parts; this page proves they hold together on a screen, against wire data a fake cannot invent. **Written 2026-09-30, repaired 2026-10-09, not run**: four of the five tests spend money, because nothing shows a tool call without a Turn.

**5 tests** · 1 free, 4 paid 💰 · about 1.8 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below. The
design is [the Turn detail spec](../../Huddle.TurnDetail-Specifications.md); the copy these tests
assert is its Appendix D.5.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named in each test from
[Common procedures](common.md). This area adds:

1. Use a **scratch** Agent, never one that matters, made with
   [`P-NEW-PERSONA`](common.md#p-new-persona): Name `Scratch`, **Model** Haiku, **Effort** `low`. A Model
   or Effort change restarts an Agent and clears what it remembers, and every paid test below asks
   it to edit or write a file.
2. Put one small text file in the scratch Agent's Work Dir, `<data>\Agents\Scratch\work\notes.md`
   (a few lines, two of which carry a typo each): `Teh meeting is on Friday.` and
   `Please recieve the report by noon.` Two typos, because TURNDETAIL-01 fixes one in step 1 and a
   different one in step 6.
3. Keep the Agent's Effort at `low`, the script's convention. `claude-agent-acp` sends no Plan or
   thinking at the default Effort, which is what these tests assume; do not raise it here.
4. "Restart the app" always means `P-STOP` then the launch line for the lane the test names.
   Spend is in memory, so a restart starts it from zero, which TURNDETAIL-02 relies on.
5. A tool call is observed on the Draft's rows. For a second record, `O-TOOLCALLS` for `Scratch` has
   one line per call, written as the call is made, with the Adapter's own tool name; the folder does
   not exist until the first call, so read it after the Turn, not before. `TraceWire` is forbidden
   (common.md, standing conventions) and `O-WIRE` never sees an in-process Agent, so no test here
   reads the wire.

## Tests

### TURNDETAIL-01 — An edit shows a live, expandable preview in a Room

**Paid** 💰 · about 20 min

*Proves the whole path on real wire data: the Adapter's `diff` and `locations` reach the runner, cross the pipe on `ToolActivity`, land on the Draft and render as a row the Human can open. Also proves the row disappears with the Draft.*

**Before you start**

- Paid lane (`P-LAUNCH-PAID`). `Scratch` is Online and its Work Dir holds `notes.md` with the two typos.
- A direct Room with `Scratch` is open in the browser, and `T-A` is showing the app log.

**Steps**

1. Type `Fix the typo "Teh" in notes.md and tell me what you changed.` and send it.
2. While the Draft streams, watch the area under its text. Write down each row that appears, in order, with its status label.
3. When an `Edit` or `Write` row is present, press its expander (the small chevron at the end of the row). Do not press anything else.
4. Read the opened preview: the path in its header, the **Removed** block, the **Added** block.
5. Keep the preview open and wait for the Turn to finish.
6. Type `Now fix the typo "recieve" in notes.md.` and send it. Press **Stop** before the reply completes, and watch the rows.
7. Read the last line of `O-TOOLCALLS` for `Scratch`.

**Pass if — all of these**

- Rows appear under the streaming text, oldest first, never reordering, each with a status label of exactly `Waiting`, `Running`, `Done` or `Failed` (read from the element's `aria-label`). A `Notice` row may also appear: it is an Adapter notice, not a tool call.
- The edit row has an expander whose accessible name reads `Show change to notes.md`, and `Hide change to notes.md` once open.
- The opened preview's header is the full path. The **Removed** block contains `Teh` with a `−`, the **Added** block contains `The` with a `+`, and neither block carries a note. (The model may send only the changed word as `old_string`; the block shows what it sent, so do not require the whole line.)
- Opening the row does not scroll the Room.
- When the Turn ends the whole list goes with the Draft; the posted reply contains only text.
- Stopping the second Turn also removes its list.
- Step 7 shows a line for the edit, with the Adapter's tool name (write it down).

**Fail if — any of these**

- Rows appear only after the Turn ends -> the Draft is not being re-read on `DraftChanged`.
- The row shows a title but never an expander on an edit, and `O-TOOLCALLS` names an edit tool -> the `diff` is not reaching the Draft. If the model used a shell or other non-edit tool instead, that is not a defect: repeat once.
- The preview shows the same text in both blocks, or an empty block -> the mapper is reading the wrong side, or a write is being treated as an edit.
- Opening the preview jumps the Room to the bottom -> the scroll signature depends on view state.

### TURNDETAIL-02 — Spend is the Adapter's running total across two Turns

**Paid** 💰 · about 25 min

*Proves the running-total rule: the card's figure is the Adapter's own running total for this run, not the sum of the totals it reported. The Adapter's figure is not observable outside the card (the wire trace is forbidden), so the proof is the shape of the two figures.*

**Before you start**

- Paid lane, app just restarted, so Spend is zero. `Scratch` is Online.
- Write down your decimal separator: the line uses the current culture (`N3`), so a comma-decimal browser locale reads `0,091`, not `0.091` (`AgentProfileCard.razor:125`).

**Steps**

1. Open the Agent's card from `/agents`. Confirm there is **no** `Spent since start` line.
2. Send the Agent a one-word request (`Reply with the word yes.`) in a direct Room. Wait for the reply.
3. Reopen the card. Read the Spend line and write the amount down (call it A).
4. Send the same request again. Wait for the reply, reopen the card, read the line (call it B).
5. Keep the card open, send a third request from another browser tab, and watch the line.

**Pass if — all of these**

- Step 1 shows no Spend line at all, and no zero.
- After step 3 the line reads `Spent since start: {amount} USD`, with the amount to three decimals and above zero.
- After step 4, B is larger than A, and B minus A is smaller than A. A sum of running totals would make B at least double A plus a second Turn's cost; the first Turn pays for the fixed overhead of the session, so the second adds less. Write down A, B and the difference.
- The line updates by itself in step 5, without closing the card.
- The figure reads as a limit on this run, not an invoice: the line is labelled `since start`, and a restart of the app clears it. A role-hired Agent would also show `Total spent:` (the persisted total); `Scratch` is not role-hired, so that line is absent.

**Fail if — any of these**

- B is roughly double A, or B minus A is at least A -> a running total is being added as if it were a per-Turn amount.
- The line never appears after a reply -> the Adapter's cost is dropped in the mapper or the runner; check that you are on the stock Claude Adapter, not a profile that sends no cost.
- The line only updates after the card is reopened -> the card is not subscribed to `SpendChanged`.

### TURNDETAIL-03 — A Persona on `agency-acp` shows rows with no preview and no Spend line

**Free** · about 15 min

*Proves the feature degrades quietly for an Adapter that sends no `diff`, no `locations` and no `cost`: a row is a title and a status, and nothing is invented.*

**Before you start**

- This is not `P-LAUNCH-FREE`: that lane sets `Acp:Enabled=false`, so no runner starts and even Restart refuses (`PersonaSupervisor.cs:508-512`). Use the **agency lane**: ACP on, with the profile list of [adapters](adapters.md) Setup 1 and the third `agency` profile of its Setup 2, **with `mock` first** so the default Adapter, and the Chief of Staff, are free. Launch as that page's RESTART LANE does, after `P-BUILD`.
- Cost guard: no Agent in this test is on `claude`. Before the first Message, open each card and check the Adapter field; set any Agent that reads `claude` to `mock`, or leave it unused.
- A scratch Agent `Scratch` on Adapter `agency` is Online, with a local model reachable (adapters.md Setup 2), and `notes.md` in its Work Dir.

**Steps**

1. Send the Agent `Read notes.md and tell me what it says.`
2. Watch the Draft's rows.
3. Open its card.

**Pass if — all of these**

- Rows appear, each with a status label, and none has an expander.
- The card has no `Spent since start` line and no `0.000`.

**Fail if — any of these**

- A row shows an empty expander or an empty preview -> the view renders a container for an absent `Edit`.
- The card shows a zero -> an absent cost is being reported as zero.
- No row appears although the Agent answered from the file -> the Adapter sends no tool activity at all, which is not a defect of this feature; record it.

### TURNDETAIL-04 — The list and preview at 320 px and under three Themes

**Paid** 💰 · about 25 min

*Proves the layout and colour rules in [Appendix D.6 and D.7](../../Huddle.TurnDetail-Specifications.md): no horizontal scroll at phone width, a long block scrolling inside its capped height, and every mark and border readable under Huddle Dark, Huddle Light and Nordic Dark.*

**Before you start**

- Paid lane. A direct Room with `Scratch` is open.
- A Write long enough to reach the cap: more than 4,096 characters, so the preview shows its clip note, and many lines, so its block reaches `max-height: 12rem` (`app.css:659-662`). The one-line fix of TURNDETAIL-01 reaches neither.

**Steps**

1. Send `Create long.md with 120 lines, each reading "Line N: the quick brown fox jumps over the lazy dog" with N counting up, then read it back twice and summarise it.` The Turn must still be running while you do the steps below; if it finishes first, send the request again with a different file name.
2. When the `Write` row appears, press its expander. Scroll inside the opened block with the mouse wheel.
3. In DevTools, emulate a 320 px wide viewport. Read the row and the open preview.
4. Switch the Theme at `/settings/appearance` to each of three Themes: Huddle Dark, Huddle Light and Nordic Dark. Open the Room again each time you come back. After each switch, check whether the row is still open; if it closed, open it again. Look at the status marks, the two borders and the text in the preview under each.
5. In the 320 px view press Tab until the expander has focus and press Enter, then Space.

**Pass if — all of these**

- The opened preview shows `Shortened: showing the first 4,096 characters.` and its block scrolls inside its own capped height, not the Room.
- At 320 px the page has no horizontal scrollbar and the preview's text wraps inside its block.
- The expander is at least 44 px tall and its focus outline is visible.
- Under all three Themes every status mark shows by shape as well as colour, and the Removed and Added blocks (a Write has an Added block only; if a Removed block shows, record it) are still told apart by their labels and `−` / `+` even where their borders are faint.
- Enter and Space both toggle the row and focus stays on the button.
- Write down, per switch, whether the row stayed open. Choosing a Theme applies it in place without a page load ([appearance-theme](appearance-theme.md) APPEARANCETHEME-04), so staying open is expected; a close is a note, not a failure.

**Fail if — any of these**

- The Room scrolls sideways at 320 px -> the preview is wider than its container.
- A border or mark disappears under one Theme -> a colour literal has crept into the stylesheet; run `agents/scripts/Run-Tests.ps1 -FilterClass '*ThemeCatalogTests'` and `'*ThemeSourceTests'`.
- Focus leaves the button when it toggles -> the row is being re-created instead of re-rendered in place.
- The long block grows past 12rem and pushes the Room down -> the height cap is not applied.

### TURNDETAIL-05 — A burst of ten quick tool calls neither reorders nor flickers the list

**Paid** 💰 · about 20 min

*Proves the two risks the automated tests cannot see: rows twitching when calls arrive and finish inside one frame, and `MudCollapse` animating on every re-render ([Appendix D.8](../../Huddle.TurnDetail-Specifications.md)). A read-only burst has no edit and so no expander, so the burst includes one edit, early.*

**Before you start**

- Paid lane. `Scratch`'s Work Dir holds about ten small files and `notes.md` with a typo.

**Steps**

1. Send `First fix the typo "Teh" in notes.md, then read every other file in your work folder, one at a time, then say done.`
2. As soon as the edit row appears, open its expander (only an edit row has one: `MessageList.razor:142-156`).
3. Watch the list for the whole burst, with the preview open.

**Pass if — all of these**

- Rows only ever append at the bottom; nothing already shown moves. A Draft keeps at most six rows (`Drafts.cs:38`): once six are shown the top row, the oldest, leaves as the next arrives, and a row with no title reads `Tool call` or its file name.
- No row flashes or changes height unprompted.
- The opened row's animation plays once, on the click, and never again during the burst. If the edit row is dropped from the six, the preview goes with it; write down when.

**Fail if — any of these**

- Rows jump up and down as statuses change -> the list is being rebuilt instead of patched by id.
- An open preview collapses and reopens as updates arrive -> open state is being lost on re-render.
- The preview's expand animation replays on each new row -> `MudCollapse` is re-animating on every render.
