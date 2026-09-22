# Room view, posting Messages and Transcript rendering

Prove, in a real browser, that the Room view at `/` and `/rooms/{RoomId}` posts, renders and durably stores Messages correctly — the Enter/Shift+Enter split that lives only in JavaScript, the Markdown pipeline's deliberately narrow extension set and its three safety behaviours (HTML disabled, link schemes rewritten, advanced extensions absent), Draft streaming settling into a rendered Message, the Budget pause, autoscroll, and the on-disk JSONL Transcript that is the only oracle able to settle a rendering question against what was actually stored. Almost none of this is reachable from the test suite: it is browser-resolved (DOM, CSS cascade, HTML parser), JS-driven, or a file on disk.

**32 tests** · 31 free, 1 paid 💰 · about 3.5 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-FREE`](common.md#p-launch-free) from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB`, the four resets, and the
standing conventions. This area adds:

1. Wait for `O-LOG` to print `Demo agent echo connected.` and `Demo agent alpha connected.` before starting. Two Rooms and two free in-process echo agents exist from that point. If those lines never appear, every test needing an agent reply is INCONCLUSIVE — say so rather than failing it.
2. Open a browser at `http://localhost:5100` with DevTools (F12) available. Several tests are settled only by inspecting the DOM in the Elements panel.
3. In `T-B` set the data path once: `$data = 'E:\Repos\Huddle\src\Huddle.App\App_Data'`.
4. Get the open Room's id — the 32 hex characters after `/rooms/` in the address bar — and set `$room = '<those characters>'`. Then define the two helpers this area reuses throughout: `function Lines { if (Test-Path "$data\rooms\$room.jsonl") { (Get-Content "$data\rooms\$room.jsonl").Count } else { 0 } }` and `function Tail { Get-Content "$data\rooms\$room.jsonl" -Tail 3 }`. Re-set `$room` every time you switch Rooms.
5. To type a multi-line Message, Shift+Enter between the lines. Every step below that says Shift+Enter means exactly that and never means Enter.
6. When a test says to reset, it means `P-RESET-ROOMS`. ROOMMESSAGING-32 is the one test that runs on `P-LAUNCH-PAID` instead.

## Tests

### ROOMMESSAGING-01 — Landing on / redirects into the first Room, and /rooms/{id} deep-links

**Free** · about 4 min

*Proves the Room view's routing, header, member list and sidebar all render the Room's Name rather than its id.*

**Before you start**

- The app is running with demo agents connected.
- No test has deleted team.db.

**Steps**

1. Navigate the browser to `http://localhost:5100/` (no path after the slash).
2. Read the address bar and write down what it now shows.
3. Read the `<h1>` at the top of the main column.
4. Read the muted grey line directly beneath that `<h1>`.
5. Look at the right-hand end of the header row for a button and read its label.
6. Read the sidebar top to bottom and list every item you see.
7. Select the whole URL from the address bar, open a new browser tab, paste it, press Enter.
8. In the new tab, look at the sidebar and note whether the entry for the currently open Room is visually distinguished from the other entry.
9. Click `alpha` in the sidebar and confirm the `<h1>` and the address bar both change.

**Pass if — all of these**

- The address bar changed from `http://localhost:5100/` to `http://localhost:5100/rooms/` followed by exactly 32 hexadecimal characters.
- The `<h1>` reads `echo` (a word, not 32 hex characters).
- The muted line beneath it reads exactly `You, echo`.
- A button labelled `Add teammate` sits at the right of the header row.
- The sidebar contains, in order: a `New chat` button, a room list containing `echo` and `alpha`, a `Teammates` link and a `Settings` link.
- The pasted URL in the second tab opens the same Room with the same `<h1>` and the same transcript.
- The sidebar entry for the open Room is visually distinguished (different colour or weight) from the other entry.
- Clicking `alpha` changes the `<h1>` to `alpha` and changes the id in the address bar.

**Fail if — any of these**

- The browser stays on `http://localhost:5100/` with a blank main column -> the redirect never fired; the Room lookup returned nothing where the sidebar shows Rooms.
- The address bar cycles between `/` and `/rooms/...` repeatedly -> a redirect loop.
- The `<h1>` shows 32 hex characters instead of `echo` -> the header is rendering the Room id, not its Name.
- The members line shows 32-hex ids instead of `You, echo` -> the member projection is reading Id where it should read Name.
- The sidebar shows `echo`/`alpha` but the main column reads `No rooms yet. Start an agent to create one.`, or the reverse -> the two copies of that same literal have drifted apart; the one that disagrees with the sidebar's actual contents is the bug.
- Clicking `alpha` changes the URL but the `<h1>` and transcript still show `echo` -> the page is not reloading on a route-parameter change.

**Inconclusive if**

If BOTH the sidebar and the main column read `No rooms yet. Start an agent to create one.`, no agent connected and there are no Rooms to test. Check `T-A` for `Demo agent echo connected.`. If it is absent, this test and every agent-dependent test below are INCONCLUSIVE — restart the app and retry once before recording anything. Do not record a FAIL.

> [!NOTE]
> Optional cross-check if you have `sqlite3` on PATH: `sqlite3 "$data\team.db" "select id, name from rooms order by created, id;"`. The first row's `id` must equal the 32 hex characters in the address bar, and its `name` must equal the `<h1>`. Skip this cross-check silently if sqlite3 is not installed — its absence is not a finding.

### ROOMMESSAGING-02 — Enter sends the Message, clears the textarea, and appends exactly one JSONL line

**Free** · about 5 min

*Proves the whole free posting path end to end: the JavaScript keydown handler, the server round-trip, the live event that paints the row, and the append to disk.*

**Before you start**

- A Room is open in the browser and `$room` in `T-B` holds that Room's id.
- No agent is required — a Human Message posts whether or not anyone is listening.

**Steps**

1. In `T-B` run `Lines` and write down the number.
2. In the browser, click into the textarea with placeholder `Message… (/invite @agent)`.
3. Type exactly `hello there`.
4. Press **Enter** (not Shift+Enter).
5. Immediately look at the textarea and note whether it still contains text.
6. Look at the bottom of the transcript area and read the new row: the sender name, the time beside it, and the body text.
7. In `T-B` run `Lines` again and write down the number.
8. In `T-B` run `Tail` and read the last line in full.

**Pass if — all of these**

- The textarea is empty the instant Enter is released — no text remains.
- A new row appears at the bottom of the transcript without any page reload.
- That row's meta line shows the sender `You` in a heavier weight and a time in `HH:mm` form beside it.
- That row's body reads `hello there`.
- The transcript is scrolled so the new row is visible.
- `Lines` increased by exactly 1 (or the file was created and now holds 1 line).
- The last JSONL line is one complete JSON object ending with `,"senderId":"human","senderName":"You","text":"hello there"}`.

**Fail if — any of these**

- Enter inserts a newline in the textarea and nothing posts -> the keydown listener in `wwwroot/app.js` never attached; check the browser Console for an error naming `teamComposer.attach`.
- The textarea still holds `hello there` after Enter -> the JS clear did not run even though the send did.
- The row appears only after pressing F5 -> the live `MessagePosted` subscription on the Room page is broken; the store is fine but the page is not listening.
- `Lines` grew by 2 or more for one Enter -> the Message is being appended more than once.
- The row appears on screen but `Lines` did not change -> the Message never reached disk; a restart would lose it.
- Nothing happens at all — no row, no file change, no console error -> the send path is dead end to end.

**Inconclusive if**

If `$data\rooms` does not exist AND no row appeared, you cannot tell a posting failure from a storage failure. Check `T-A` for an exception, then retry once in the other demo Room (`alpha`). If it works there, record a FAIL scoped to the first Room; if it fails in both, record INCONCLUSIVE and report the console output verbatim.

> [!NOTE]
> The textarea is cleared by JavaScript BEFORE the server round-trip finishes. That is why a rejected send loses the typed text — tested deliberately in ROOMMESSAGING-22, not here.

### ROOMMESSAGING-03 — Shift+Enter inserts a newline and does not send

**Free** · about 4 min

*Proves the Enter/Shift+Enter split, and that the newline really is stored even though it is not rendered.*

**Before you start**

- A Room is open; `$room` is set.

**Steps**

1. In `T-B` run `Lines` and write down the number.
2. Click into the composer and type `line one`.
3. Press **Shift+Enter**.
4. Type `line two`.
5. Look at the transcript and note whether anything was posted.
6. Look at the textarea and note how many visible lines of text it holds.
7. In `T-B` run `Lines` again.
8. Press **Enter**.
9. Read the newly posted row in the transcript.
10. In `T-B` run `Tail` and read the last line.

**Pass if — all of these**

- After Shift+Enter, the textarea shows two lines of text and the transcript is unchanged.
- `Lines` is unchanged at step 7 — Shift+Enter posted nothing.
- After Enter, exactly one new row appears and `Lines` has grown by exactly 1.
- The posted row renders as the single line `line one line two` with the two halves on one rendered line.
- The last JSONL line contains `"text":"line one\nline two"` — a literal backslash-n escape between the two halves.

**Fail if — any of these**

- Shift+Enter posts the Message -> the shift-key guard in the keydown handler broke; Enter and Shift+Enter are now the same key.
- Shift+Enter both inserts a newline AND posts -> the handler is running twice (double attachment).
- The posted JSONL line reads `"text":"line oneline two"` or `"text":"line one line two"` with no `\n` -> the newline was destroyed before storage, which is real data loss.
- The textarea stays one line after Shift+Enter -> the default newline insertion is being suppressed for the shifted key too.

**Inconclusive if**

If Enter itself does not post, this test cannot run — go and settle ROOMMESSAGING-02 first and record this one INCONCLUSIVE, pending that.

> [!NOTE]
> `line one line two` rendering on ONE line is CORRECT and must not be filed as a bug. The Markdown pipeline follows CommonMark, where a single newline inside a paragraph is a space. ROOMMESSAGING-06 tests that rule directly.

### ROOMMESSAGING-04 — An empty or whitespace-only Enter posts nothing and shows no error

**Free** · about 3 min

*Proves the composer swallows a no-op submit before it reaches the service, so no `empty message` error is ever shown and no blank row is ever stored.*

**Before you start**

- A Room is open; `$room` is set.

**Steps**

1. In `T-B` run `Lines` and write down the number.
2. Click into the empty composer and press **Enter** without typing anything.
3. Look at the transcript and at the area directly above the composer.
4. Type exactly three space characters and press **Enter**.
5. Look at the transcript, at the area above the composer, and at the textarea.
6. Press **Shift+Enter** once (producing a blank line), then press **Enter**.
7. Look at the transcript and above the composer again.
8. In `T-B` run `Lines`.

**Pass if — all of these**

- No new row appears in the transcript for any of the three attempts.
- No red text and no green text appears above the composer at any point.
- After the three-space attempt the textarea is empty (the whitespace visibly disappears).
- `Lines` at the end equals `Lines` at the start.
- If the JSONL file did not exist at the start, it still does not exist at the end.

**Fail if — any of these**

- A row with an empty body appears in the transcript -> a blank Message reached the store.
- Red text reading `empty message` appears above the composer -> the guard that returns before calling the service is gone and the service's own exception is now surfacing to the Human.
- `Lines` increased -> whitespace-only text was persisted; a Mention scan and the `/invite` match both run against that stored text, so this breaks more than cosmetics.

**Inconclusive if**

If the file does not exist and never existed, `Lines` reads 0 both times and proves nothing on its own — post one real Message first (`marker`), re-read `Lines`, then rerun this test from step 1 so the comparison is against a non-zero baseline.

> [!NOTE]
> The textarea being cleared even though nothing was sent is DELIBERATE and expected — the JS clears unconditionally. Do not file it here; ROOMMESSAGING-22 covers the case where that clearing actually costs the Human something.

### ROOMMESSAGING-05 — Leading and trailing whitespace is trimmed before the Message is stored

**Free** · about 3 min

*Proves trimming happens in the data, not just in the CSS — the only test that can tell those two apart.*

**Before you start**

- A Room is open; `$room` is set.

**Steps**

1. Click into the composer.
2. Type three spaces, then `padded`, then three more spaces. The full typed string is `   padded   `.
3. Press **Enter**.
4. Read the new transcript row and note whether the text is indented.
5. In `T-B` run `Tail` and read the last line.

**Pass if — all of these**

- The transcript row reads `padded` with no visible leading indent.
- The last JSONL line contains exactly `"text":"padded"` — no space characters inside the quotes on either side.

**Fail if — any of these**

- The JSONL line reads `"text":"   padded   "` -> the text was stored untrimmed; the screen looks fine only because HTML collapses whitespace. Mention matching compares against this stored text character by character and `/invite` is anchored at the very start of it, so both will misbehave on untrimmed input.
- No row appears at all -> the trim-then-length-check path rejected a non-empty message.

**Inconclusive if**

If the JSONL file cannot be read (locked, missing), the screen alone cannot settle this — HTML collapses leading spaces regardless. Record INCONCLUSIVE and retry after the file becomes readable.

> [!NOTE]
> Checking this on screen only is worthless: the rendered result is identical whether the text was trimmed or not.

### ROOMMESSAGING-06 — A soft line break collapses to a space; a blank line makes two paragraphs; a list still works

**Free** · about 6 min

*Pins the single most likely false bug report in this area so a tester never files it, while still catching the genuine inverse failure.*

**Before you start**

- A Room is open.

**Steps**

1. In the composer type `alpha`, press **Shift+Enter**, type `beta`, press **Shift+Enter**, type `gamma`, then press **Enter**.
2. Read the rendered row.
3. Right-click that row's body text and choose Inspect. In the Elements panel, find the enclosing element with class `message-body` and count how many `<p>` children it has.
4. In the composer type `alpha`, press **Shift+Enter** twice (leaving a blank line), type `beta`, then press **Enter**.
5. Read the rendered row and inspect its `message-body` the same way.
6. In the composer type `- one`, press **Shift+Enter**, type `- two`, press **Shift+Enter**, type `- three`, then press **Enter**.
7. Read the rendered row and inspect its `message-body`.

**Pass if — all of these**

- The first message renders as one line reading `alpha beta gamma`.
- Its `message-body` contains exactly one `<p>` element whose text is `alpha beta gamma`.
- The second message renders as two stacked paragraphs with a small vertical gap.
- Its `message-body` contains exactly two sibling `<p>` elements, `alpha` and `beta`.
- The third message renders as a real bulleted list with three bullets.
- Its `message-body` contains a `<ul>` with three `<li>` children.

**Fail if — any of these**

- The blank-line-separated text renders as one paragraph, or its `message-body` holds only one `<p>` -> paragraph splitting is broken, which IS a real defect.
- The list renders as the literal text `- one - two - three` with no `<ul>` -> Markdown list parsing is broken.
- The `message-body` for any of the three contains literal `\n` characters or visible escape sequences -> the stored escape is leaking into the rendered output.

**Inconclusive if**

If devtools cannot be opened (locked-down browser), the first case cannot be distinguished from a rendering fault by eye. Record INCONCLUSIVE for the `<p>`-count checks and report only what was visible.

> [!NOTE]
> `alpha beta gamma` on ONE line is CORRECT — the pipeline deliberately does not treat a soft line break as a hard one, so a single newline is a space, exactly as CommonMark specifies. The newline is still stored (ROOMMESSAGING-03 proves that). Do NOT file this. The genuine bug is the opposite direction: blank lines failing to split, or the list failing to render.

### ROOMMESSAGING-07 — Core Markdown renders: bold, italic, heading, lists, blockquote, inline and fenced code, rule

**Free** · about 8 min

*Proves the Markdown pipeline runs at all and that its output reaches the DOM as real elements rather than escaped text.*

**Before you start**

- A Room is open.

**Steps**

1. Send each of the following as its own Message, pressing Enter after each: `**bold**`, then `*italic*`, then `# Heading`, then `1. first` , then `> quoted`, then `` `inline code` ``.
2. Build a bulleted list: type `- a`, press **Shift+Enter**, type `- b`, press **Enter**.
3. Build a fenced code block: type ```` ```cs ````, press **Shift+Enter**, type `int x;`, press **Shift+Enter**, type ```` ``` ````, press **Enter**.
4. Send `---` as its own Message.
5. Read every rendered row.
6. Inspect each row's `message-body` in the Elements panel and note which HTML elements it contains.

**Pass if — all of these**

- `**bold**` renders as bold text, and its `message-body` contains a `<strong>` element.
- `*italic*` renders as italic text, and its `message-body` contains an `<em>` element.
- `# Heading` renders as a visibly larger heading inside the message row, with an `<h1>` element.
- `- a` / `- b` renders as a bulleted list, with `<ul>` and two `<li>` elements.
- `1. first` renders as a numbered list, with an `<ol>`.
- `> quoted` produces a `<blockquote>` element.
- `` `inline code` `` renders in a monospace face, with a `<code>` element.
- The fenced block renders as a block in a monospace face, with `<pre>` containing `<code>`.
- `---` renders as a horizontal rule, with an `<hr>` element.
- No row shows literal asterisks, backticks or hash characters where formatting was expected.

**Fail if — any of these**

- Any of these shows as literal source text (`**bold**` with the asterisks visible) -> the Markdown pipeline is not being run for message bodies, or the HTML is being escaped instead of inserted.
- The fenced block renders as one run-on paragraph with the backticks visible -> fenced-code parsing is broken.
- A `message-body` contains text like `&lt;strong&gt;` -> the rendered HTML is being double-escaped.

**Inconclusive if**

If a fenced block cannot be typed because the keyboard layout makes backticks awkward, skip only that sub-case and mark the fenced-code line INCONCLUSIVE — do not fail the whole test.

> [!NOTE]
> Rendered Markdown inside a message is deliberately UNSTYLED: `<pre>`, `<code>`, `<blockquote>`, `<table>` and headings get only a monospace font where applicable, and no border, background or padding of their own. Unstyled-but-structurally-correct is the current, accepted state — do not file it as a rendering failure.

### ROOMMESSAGING-08 — The four enabled Markdig extensions render: pipe tables, task lists, autolinks, emphasis extras

**Free** · about 8 min

*Proves exactly the four opted-in extensions work — dropping any one of them is a silent regression with no build or log signal. Message rendering itself was untouched by the MudBlazor migration ("Convert the chat page chrome, and nothing else" left `MarkdownRenderer` and the `message-body` markup exactly as they were), so this test's mechanics stand unchanged.*

**Before you start**

- A Room is open.

**Steps**

1. Build a pipe table: type `| a | b |`, press **Shift+Enter**, type `|---|---|`, press **Shift+Enter**, type `| 1 | 2 |`, press **Enter**.
2. Build a task list: type `- [x] done`, press **Shift+Enter**, type `- [ ] todo`, press **Enter**.
3. Send `https://example.com` on its own, with no brackets and no parentheses.
4. Send `www.example.com` on its own.
5. Send `~~struck~~`.
6. Send `==marked==`.
7. Send `^sup^`.
8. Send `~sub~`.
9. Send `++ins++`.
10. Inspect each resulting `message-body` in the Elements panel.

**Pass if — all of these**

- The pipe table renders as a grid of cells (borderless is fine) and its `message-body` contains a `<table>` element with `<tr>` and `<td>` children — not the literal pipe characters.
- The task list renders with two checkboxes, the first ticked, and contains `<input type="checkbox" disabled>` elements.
- The bare `https://example.com` renders as a clickable link, with `<a href="https://example.com">`.
- The bare `www.example.com` also renders as a clickable link with an `<a>` element.
- `~~struck~~` renders struck through, with a `<del>` element.
- `==marked==` renders highlighted, with a `<mark>` element.
- `^sup^` renders raised, with a `<sup>` element.
- `~sub~` renders lowered, with a `<sub>` element.
- `++ins++` renders underlined/inserted, with an `<ins>` element.

**Fail if — any of these**

- The pipe table shows as literal `| a | b |` text -> the pipe-tables extension was dropped from the pipeline.
- The task list shows as literal `- [x] done` with no checkbox -> the task-lists extension was dropped.
- A bare URL stays plain text -> the autolinks extension was dropped.
- Any of the five emphasis forms renders as literal characters -> the emphasis-extras extension was dropped.
- A checkbox is clickable (not disabled) -> the task-list rendering is not in its read-only form.

**Inconclusive if**

If the pipe table renders as a table but with no visible cell borders, that is NOT inconclusive — it is the expected unstyled state; check the Elements panel for the `<table>` element and judge on that alone.

> [!NOTE]
> These four are the ONLY extensions enabled on purpose. Anything from the advanced set rendering is a security regression, tested directly in ROOMMESSAGING-09.

### ROOMMESSAGING-09 — Advanced Markdown extensions are absent: generic attributes stay literal and never become live handlers

**Free** · about 8 min

*The highest-value security check in this area, and it is free: proves no attacker-supplied (or model-supplied) text can attach an event handler or embed a player through the Markdown path.*

**Before you start**

- A Room is open.
- Devtools Console is open and cleared before you start.

**Steps**

1. Send exactly `# Hi {onclick="alert(1)"}`.
2. Read the rendered row, then click directly on the rendered heading text.
3. Send exactly `Hi {onmouseover="alert(1)"}`.
4. Hover the mouse over the rendered text for three seconds.
5. Send exactly `:smile:`.
6. Send exactly `^[a footnote]`.
7. Send `Term`, press **Shift+Enter**, type `:  definition`, press **Enter**.
8. Send exactly `::: warning`.
9. Send exactly `https://www.youtube.com/watch?v=dQw4w9WgXcQ`.
10. In the Elements panel, inspect each of these rows' rendered elements and read their attribute lists.
11. In the Elements panel, use Ctrl+F and search the whole document for `iframe`, then for `onclick`, then for `onmouseover`.
12. Read the devtools Console.

**Pass if — all of these**

- The heading renders with the characters `{onclick="alert(1)"}` visible as literal text inside it.
- Clicking the heading produces no dialog and does nothing.
- Hovering the `{onmouseover=...}` text produces no dialog and does nothing.
- `:smile:` stays as the literal characters `:smile:` — no emoji appears.
- `^[a footnote]` stays literal; no footnote marker or footnote section appears.
- The `Term` / `:  definition` pair stays literal; no definition list appears.
- `::: warning` stays literal; no styled callout box appears.
- The YouTube URL renders as an ordinary text link only.
- No element inside any `message-body` carries an `onclick` or `onmouseover` attribute.
- The document contains no `<iframe>` element anywhere.
- The Console shows no new errors and no evidence of script execution.

**Fail if — any of these**

- A dialog appears at any point -> arbitrary script from message text is executing; STOP TESTING and report immediately, because agent replies travel this identical path.
- The rendered heading or paragraph carries a live `onclick=` or `onmouseover=` attribute in the Elements panel -> generic attributes are being parsed; the advanced extension bundle was reintroduced.
- An emoji appears where `:smile:` was typed, or a footnote/definition list/callout renders -> the advanced extension bundle was reintroduced.
- The YouTube URL becomes an embedded `<iframe>` player -> the media extension is active; remote content is now being embedded from message text.

**Inconclusive if**

If the browser blocks `alert()` dialogs by configuration, the click and hover steps prove nothing on their own. In that case judge PASS/FAIL strictly on the Elements-panel attribute inspection at step 10 and record the dialog sub-steps as INCONCLUSIVE. Never accept "nothing visibly happened" as a pass when dialogs are suppressed.

> [!NOTE]
> Do NOT accept the argument "raw HTML is disabled so this is safe" — disabling raw HTML does not stop generic attributes, which are a separate extension. The absence of the advanced extension bundle is a binding rule in `docs/agencyteam/rules.md`.

### ROOMMESSAGING-10 — Raw HTML inside a Message is escaped, never executed

**Free** · about 5 min

*Proves the second half of the safety story: HTML typed into a Message (or produced by a model) becomes visible text, not live DOM. Message rendering itself was untouched by the MudBlazor migration, so this test's mechanics stand unchanged — the "red overlay" and "layout breaks" language below describes what the INJECTED payload would do if it executed, not any part of the app's own UI.*

**Before you start**

- A Room is open.
- Devtools Console is open and cleared.

**Steps**

1. Send exactly `<script>alert(1)</script>`.
2. Send exactly `<b>bold?</b>`.
3. Send exactly `<img src=x onerror=alert(1)>`.
4. Send exactly `<div style="position:fixed;inset:0;background:red">x</div>`.
5. Look at the whole browser window after each send.
6. Inspect each of the four rows' `message-body` in the Elements panel.
7. Read the devtools Console.

**Pass if — all of these**

- No dialog appears for any of the four.
- The literal text `<script>alert(1)</script>` is visible in the transcript.
- `<b>bold?</b>` shows its tags literally and the words are NOT bold.
- The `<img ...>` line shows as literal text; no broken-image icon appears.
- No red overlay covers the screen; the `<div ...>` line shows as literal text.
- In the Elements panel, each `message-body` contains escaped entity text such as `&lt;script&gt;` and contains no `<script>`, `<b>`, `<img>` or `<div>` node of its own.

**Fail if — any of these**

- Any dialog appears -> raw HTML is executing; stop and report immediately.
- `bold?` actually renders bold -> raw HTML is being passed through; every agent reply can now inject markup.
- A red overlay covers the page or the layout breaks -> raw HTML is live and can hijack the viewport.
- The Elements panel shows a real `<script>`, `<img>` or `<div>` node inside a `message-body` -> HTML is not being escaped.

**Inconclusive if**

If dialogs are suppressed by browser configuration, judge on the Elements panel alone and record the dialog checks as INCONCLUSIVE.

> [!NOTE]
> In the JSONL the stored text will additionally show `<` and `>` as `<` / `>` escapes. That is the wire encoder doing its job — see ROOMMESSAGING-12 — and must not be read as corruption.

### ROOMMESSAGING-11 — Link schemes are rewritten: only http, https and mailto survive

**Free** · about 5 min

*Proves the link rewriter neutralises dangerous URL schemes while leaving legitimate ones untouched.*

**Before you start**

- A Room is open.
- Devtools Console is open.

**Steps**

1. Send exactly `[click me](javascript:alert(1))`.
2. Click the rendered `click me` link and observe.
3. Send exactly `[a](https://example.com)`.
4. Send exactly `[b](http://example.com)`.
5. Send exactly `[c](mailto:someone@example.com)`.
6. Send exactly `[d](data:text/html,<h1>x</h1>)`.
7. Send exactly `[e](file:///C:/Windows/win.ini)`.
8. Inspect each of the seven rendered `<a>` elements in the Elements panel and read the exact `href` value of each.

**Pass if — all of these**

- `click me` renders as a link, and clicking it produces no dialog and navigates nowhere.
- The `click me` anchor's `href` is exactly `#`.
- Anchor `d` (`data:`) has `href="#"` exactly.
- Anchor `e` (`file:///`) has `href="#"` exactly.
- Anchor `a` has `href="https://example.com"` verbatim.
- Anchor `b` has `href="http://example.com"` verbatim.
- Anchor `c` has `href="mailto:someone@example.com"` verbatim.

**Fail if — any of these**

- Any anchor's `href` still begins `javascript:`, `data:` or `file:` -> the scheme rewriter is not running; a crafted or model-generated link can now execute script or open a local file.
- Clicking `click me` produces a dialog -> as above, and actively exploitable.
- A legitimate `https:`, `http:` or `mailto:` href was rewritten to `#` -> the rewriter is over-broad and has broken ordinary links.

**Inconclusive if**

If the Elements panel cannot be opened, clicking alone cannot distinguish "neutralised" from "browser refused to navigate". Record INCONCLUSIVE rather than guessing.

> [!NOTE]
> A neutralised link still LOOKS like a link and still responds to a click with no navigation — that is expected. Only the `href` value changes, so the `href` is the only thing worth judging on.

### ROOMMESSAGING-12 — Non-ASCII and HTML-sensitive characters are escaped in the JSONL but render correctly on screen

**Free** · about 5 min

*Pins a documented trap so a tester never files "the transcript file is corrupted", while still proving the escaping is lossless.*

**Before you start**

- A Room is open; `$room` is set.

**Steps**

1. In the composer type exactly: `He said "go" — A&B 😀 日本語` (use a real em dash and a real emoji; paste it if typing is awkward).
2. Press **Enter**.
3. Read the rendered row character by character and compare it with what you typed.
4. In `T-B` run `Tail` and read the last line.
5. In `T-B` run `Select-String -Path "$data\rooms\$room.jsonl" -Pattern '日本語' -SimpleMatch` and note that it finds nothing.
6. Press F5 to reload the browser page.
7. Read the row again after the reload.

**Pass if — all of these**

- On screen, before and after the reload, the row reads exactly `He said "go" — A&B 😀 日本語` with real characters, not escapes.
- In the JSONL line, EVERY one of those characters appears as a `\uXXXX` escape, not just the non-ASCII ones: `"` for each double quote around `go`, `&` for the ampersand, `—` for the em dash, `😀` for the emoji and `日本語` for the CJK. That is `System.Text.Json`'s default `JavaScriptEncoder`, which escapes `"`, `&`, `<`, `>`, `'` and `+` along with all non-ASCII — stricter than strictly necessary, and the safe direction. Do NOT expect a raw `"`, `&` or `—` in the file.
- The `Select-String` search for the literal CJK text finds no match.
- The reload proves the round trip is lossless — the screen is identical before and after.

**Fail if — any of these**

- The transcript on screen shows `—` or `&amp;` or similar escapes instead of the real characters -> the escaping is leaking into the rendered output, which IS a real bug.
- After the reload the characters are mangled, replaced with question marks, or truncated -> the escaping is lossy and data is being destroyed.
- The JSONL line contains a raw, unescaped `<` or `>` character in a text field -> the wire encoder is not applying its HTML-safe encoding, which weakens every downstream consumer that puts this text on a page.

**Inconclusive if**

If your terminal cannot display the emoji or CJK correctly when running `Tail`, open the JSONL file in a UTF-8-aware editor instead and judge there. A mojibake terminal is not a finding.

> [!NOTE]
> Escapes IN THE FILE are correct and deliberate — the serializer inherits an HTML-safe encoder. A tester grepping the transcript for the literal characters they typed will find nothing, and that is documented in `docs/agencyteam/traps.md`. Only escapes ON SCREEN are a bug.

### ROOMMESSAGING-13 — A long Message, a very long unbroken token and a wide code block

**Free** · about 8 min

*Proves long content is stored and rendered in full, and characterises the known horizontal-overflow gap so it is reported once, accurately.*

**Before you start**

- A Room is open; `$room` is set.

**Steps**

1. In `T-B` generate prose and copy it to the clipboard: `('lorem ipsum dolor sit amet ' * 200) | Set-Clipboard`.
2. Click into the composer, press Ctrl+V, then press **Enter**.
3. Read the rendered row: confirm the text wraps inside the message column and that the end of it is visible.
4. Scroll the transcript up and back down and confirm earlier messages are intact.
5. In `T-B` run `('x' * 400) | Set-Clipboard`, paste into the composer and press **Enter**.
6. Look at the resulting row: note whether the long token wraps, or whether a horizontal scrollbar appears on the page or the message column.
7. Look at the sidebar and the composer and note whether either has been pushed off screen.
8. Build a wide code block: type ```` ``` ````, press **Shift+Enter**, paste a 300-character single line (`('y' * 300) | Set-Clipboard` in `T-B`), press **Shift+Enter**, type ```` ``` ````, press **Enter**.
9. Note whether the `<pre>` block overflows the message column horizontally.
10. In `T-B` run `Tail` and check the long prose line is present and complete.

**Pass if — all of these**

- The ~5000-character prose message posts successfully, renders in full, wraps inside the message column, and the transcript scrolls to the bottom.
- Earlier messages remain intact and readable after scrolling back up.
- The JSONL line for the prose message contains the whole text — its length matches what was pasted.
- The sidebar and the composer both remain fully on screen and usable in all three cases.

**Fail if — any of these**

- The long prose message is truncated on screen but complete in the JSONL -> the defect is in rendering.
- The long prose message is truncated in the JSONL -> the defect is in posting; that is data loss and outranks everything else here.
- The long message fails to post at all, with or without an error -> there is no length cap in the composer or the service, so a silent rejection is a real defect.
- The sidebar or the composer is pushed off screen by the long token or the wide code block -> the overflow escapes the transcript column and breaks the app shell, which IS reportable as a layout defect.

**Inconclusive if**

If the paste is silently truncated by the browser rather than by the app, the prose case proves nothing. Verify by checking the character count in the JSONL against the clipboard length (`(Get-Clipboard).Length`) before judging; if they differ by a browser-side limit, record INCONCLUSIVE.

> [!NOTE]
> KNOWN GAP, report as cosmetic/layout only: message bodies get no word-breaking rule and fenced code blocks get no horizontal-scroll container, so a 400-character unbroken token or a 300-character code line CAN overflow the message column sideways. Other parts of the stylesheet do set aggressive word-breaking; the transcript does not. Report it once, as a layout gap, at low severity — unless it pushes the sidebar or composer off screen, which is a real failure.

### ROOMMESSAGING-14 — Messages render in post order, and the order on screen equals the order in the file

**Free** · about 8 min

*Proves the per-Room serialisation that keeps transcript order equal to event order, and catches the documented duplicate-message window when navigating between Rooms mid-reply.*

**Before you start**

- The `echo` Room is open; `$room` is set to its id.
- Demo agents are connected.

**Steps**

1. Send five Messages in quick succession, one per Enter: `1`, then `2`, then `3`, then `4`, then `5`.
2. Read the transcript top to bottom and write down the order of those five rows.
3. Send `hi @echo` and wait for the reply to finish arriving.
4. Note whether the reply row sits below the `hi @echo` row.
5. Press F5 to reload the page.
6. Read the transcript top to bottom again and compare with what you wrote down.
7. In `T-B` run `Get-Content "$data\rooms\$room.jsonl" | ForEach-Object { ($_ | ConvertFrom-Json).text }` and read the list top to bottom.
8. Now the duplicate hunt: send `hi @echo` and, within one second of pressing Enter, click `alpha` in the sidebar, then click the first Room's entry again to come back.
9. Read the last few rows of the transcript carefully.

**Pass if — all of these**

- The five rows appear top to bottom as `1`, `2`, `3`, `4`, `5`.
- The echo reply row sits immediately below the `hi @echo` row that prompted it.
- After the reload the on-screen order is identical to the order before the reload.
- The text list printed from the JSONL, read top to bottom, matches the on-screen order row for row, one line per row.
- After the rapid Room switch, the echo reply appears exactly once — no row is duplicated.

**Fail if — any of these**

- A reply appears above the Message it answers -> post ordering is not serialised; the transcript no longer describes the conversation.
- The order changes across the reload -> the live-event order and the file order disagree; one of the two paths is wrong and both are used.
- The same Message renders twice after the Room switch -> the page marks a Room as loaded before it has finished reading the file, so a Message arriving inside that window is both read from disk and appended by the event. This is the documented race; capture the exact sequence of clicks and timing.
- The JSONL text list and the screen disagree in order or count -> the transcript is not the record of what was shown.

**Inconclusive if**

If the echo agent never replies, the interleaving half cannot be judged — check `T-A` for `Demo agent echo connected.`, confirm you typed `@echo` with the at-sign, and record the interleaving and duplicate sub-cases as INCONCLUSIVE while still judging the five-message ordering.

> [!NOTE]
> The duplicate at step 8 is timing-dependent — if it does not reproduce in three attempts, record "not reproduced in 3 attempts" rather than PASS on that sub-case.

### ROOMMESSAGING-15 — The transcript survives a browser reload and is present in the prerendered HTML

**Free** · about 6 min

*Proves the transcript is read from the store on every page load, not held in a per-circuit cache — so the text is in the very first HTML response, before any live connection exists.*

**Before you start**

- A Room is open with at least three Messages; `$room` is set.

**Steps**

1. Note the sender names, times and bodies of the last three rows.
2. Press F5 and compare the transcript with what you noted.
3. Note whether the view is scrolled to the bottom after the reload.
4. Press Ctrl+Shift+R (hard reload) and compare again.
5. Open the same `/rooms/{id}` URL in a second browser tab and compare.
6. Open the same URL in a private/incognito window and compare.
7. In `T-B` run `curl.exe "http://localhost:5100/rooms/$room" -o "$env:TEMP\prerender.html"` (use `curl.exe`, not `curl`, in PowerShell).
8. In `T-B` run `Select-String -Path "$env:TEMP\prerender.html" -Pattern 'hello there' -SimpleMatch` using text you know is in the transcript.

**Pass if — all of these**

- After F5 every Message is still present, in the same order, with the same sender names and the same `HH:mm` times.
- The view is scrolled to the bottom after the reload, not parked at the top.
- The hard reload, the second tab and the private window all show the same transcript.
- The `curl.exe` output file contains the Message text, proving it is in the prerendered HTML before any live connection is made.

**Fail if — any of these**

- The transcript is empty after a reload -> the page is not re-reading the store on load; the messages only ever existed in the browser session.
- Messages appear only a second or two after the page paints -> the transcript is arriving over the live connection instead of in the prerendered HTML; a tester with scripting disabled would see nothing.
- The `curl.exe` output does not contain the Message text but the browser shows it -> the prerender path is broken and only the live path works.
- The private window shows a different (or empty) transcript -> transcript visibility is tied to something per-browser, which it must not be.

**Inconclusive if**

If `curl.exe` is unavailable, substitute the browser's View Source (Ctrl+U) and search that raw text — View Source shows the server response, not the live DOM, so it is an acceptable substitute. Do not use "Inspect" for this check; it shows the live DOM and would pass even if the prerender were broken.

> [!NOTE]
> This is the test that catches the failure mode where a transcript renders correctly for an interactive user but is invisible to anything reading the initial HTML.

### ROOMMESSAGING-16 — Sender name and timestamp in the message meta line

**Free** · about 6 min

*Proves each row is attributed to a Name (not an id) with a stable, correctly formatted time drawn from the stored timestamp.*

**Before you start**

- A Room is open with at least one Human Message and one agent reply; `$room` is set.

**Steps**

1. Read the meta line above a Human Message body.
2. Read the meta line above an agent reply body.
3. Note the exact format of the time: count the digits, look for seconds, look for AM/PM, look for a date.
4. Send a Message, wait a full minute, send another, and compare the two times.
5. Press F5 and check whether either time changed.
6. Look at the avatar in the gutter of each transcript row, and check that the sender name and body line up against it rather than sitting under it.
7. In `T-B` run `Tail` and read the `timestamp` field of the last line.
8. Convert that UTC timestamp to your machine's local time by hand and compare with the time shown on screen.

**Pass if — all of these**

- A Human Message's meta line shows the sender `You`.
- An agent reply's meta line shows the agent's Name (`echo` or `alpha`), not a 32-hex id.
- The time is 24-hour `HH:mm` — four digits and a colon, with no seconds, no AM/PM and no date.
- The two Messages sent a minute apart show different times.
- Neither time changes across a reload.
- The screen time equals the stored UTC `timestamp` converted to the machine's local time.
- Every row — a Message and a live Draft alike — carries an avatar in its own gutter, with the sender name and the body aligned in the column beside it rather than beneath it. A Teammate that has chosen nothing shows the initials of its Name; `You` shows yours.

> **Reversed 2026-09-22.** This step used to assert the opposite — *"No avatar, circle or monogram appears anywhere in the transcript"* — and the note at the end of this file used to send you to `/teammates` for the avatar half of the appearance check. Avatars are a Room-view concern now. See [ADR-0019](../../adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md).

**Fail if — any of these**

- A 32-hex id appears where a Name should be -> the meta line is rendering the sender id.
- The time includes seconds, a date, or AM/PM -> the format is not `HH:mm`.
- A time changes on reload -> the time is being computed at render from the clock rather than from the stored timestamp.
- The stored `senderName` in the JSONL differs from the Name shown on screen for the same row -> the two disagree, and the file is the record.

**Inconclusive if**

If no agent reply exists yet, judge only the Human half and record the agent-attribution checks as INCONCLUSIVE until ROOMMESSAGING-18 has produced a reply.

> [!NOTE]
> Two documented NON-bugs: (a) the displayed time is converted to local time on the SERVER, so in a multi-machine deployment it would show the server's timezone, not the browser's — irrelevant when testing on one machine, and not to be filed from a single-machine run; (b) there is no date and no day separator, so a Message from last week shows only `HH:mm`. Optional extra: setting `$env:Team__HumanName='Emre'`, deleting `team.db` and restarting causes NEW Messages to be attributed to `Emre` — only run this if a full reset is acceptable.

### ROOMMESSAGING-17 — The transcript autoscrolls on a new Message and while a Draft grows

**Free** · about 6 min

*Proves the scroll interop fires for both kinds of growth — a new Message and a still-streaming Draft — since only the second one can catch the case where counting Messages alone would leave a streaming reply off screen. `MessageList.razor`'s autoscroll arithmetic, the `.message-list` container and the `teamScroll` interop are all untouched by the MudBlazor migration ("Convert the chat page chrome, and nothing else" changed only the Stop button), so this test's mechanics stand unchanged.*

**Before you start**

- The `echo` Room is open and holds enough Messages to overflow the transcript area vertically (send `filler` twenty times if not).
- Devtools Console is open.
- Demo agents are connected.

**Steps**

1. Scroll the transcript area to the bottom.
2. Send `bottom check` and watch whether the view moves to show it.
3. In the devtools Console run: `const l=document.querySelector('.message-list'); l.scrollTop, l.scrollHeight - l.clientHeight` and compare the two numbers.
4. Send `hi @echo` and watch the streaming Draft row appear and grow.
5. While the text is still arriving, watch whether the view follows the growing text down, or only jumps once the reply settles.
6. Scroll the transcript up so older messages fill the view.
7. While scrolled up, send `yank test` and watch what the view does.
8. Read the devtools Console for any errors.

**Pass if — all of these**

- Sending a Message while at the bottom keeps the new row visible.
- `scrollTop` equals `scrollHeight - clientHeight` within one pixel after the new Message.
- The view follows the Draft down continuously as it grows, not only once at the end.
- Sending a Message while scrolled up snaps the view back to the bottom.
- The Console shows no errors.

**Fail if — any of these**

- A new Message arrives below the fold and the view does not move -> the scroll interop call failed; check the Console for an error naming the scroll function.
- The view scrolls on every render even when the content has not changed (visible jitter) -> the change-detection guard on the scroll is not working.
- The view jumps only when the reply settles, never during the stream -> the scroll trigger is counting Messages only and ignoring Draft growth.

**Inconclusive if**

If the transcript does not overflow its area, none of this is observable — send more filler Messages until a scrollbar appears on the transcript area, then restart the test. If the echo agent never replies, record the Draft sub-cases as INCONCLUSIVE.

> [!NOTE]
> The snap-back at step 7 IS the current behaviour: there is no "the reader is scrolled up" guard, so any new Message or Draft growth yanks the view to the bottom. Report it as a UX limitation at low severity, not as a functional defect.

### ROOMMESSAGING-18 — A demo echo reply streams as a plain-text Draft and settles into rendered Markdown

**Free** · about 6 min

*The free end-to-end check of the whole pipeline — the wire, the Draft-streaming path and the Markdown path — and the one test that proves a Draft is replaced rather than duplicated.*

**Before you start**

- The `echo` Room is open; `$room` is set.
- Demo agents are connected (`T-A` shows `Demo agent echo connected.`).

**Steps**

1. In `T-B` run `Lines` and write down the number.
2. In the composer type exactly `hi @echo` and press **Enter**.
3. Watch the transcript closely for the next few seconds — do not blink; the reply arrives in about three visible chunks.
4. Note whether a second row appears below your Message showing the sender `echo`.
5. Note whether that row carries a button beside the sender name, and read its label.
6. Note whether the text of that row arrives in chunks with a blinking caret at its end.
7. While it is streaming, note whether the characters `**echo:**` are briefly visible as literal asterisks.
8. Once it stops, read the settled row: note whether `echo:` is now rendered bold and whether the asterisks are gone.
9. Confirm the settled row carries NO button beside the sender name.
10. Confirm the reply text is your own text quoted back.
11. In `T-B` run `Lines` and then `Tail`.

**Pass if — all of these**

- A streaming row appears with the sender `echo` and a button labelled `Stop` beside the name.
- The text visibly arrives in chunks with a blinking caret at the end.
- During streaming the text is plain — `**echo:**` is visible with its asterisks.
- When it settles, the row is replaced: `echo:` renders bold and the asterisks are gone.
- The settled row has no `Stop` button.
- The reply text quotes your message back.
- `Lines` grew by exactly 2 (your Message and the reply).
- The last JSONL line contains `"text":"**echo:** hi echo"` and `"senderName":"echo"`.

**Fail if — any of these**

- The reply text appears twice — once as a streaming row and again as a settled row below it -> the Draft is not being removed when the real Message posts.
- Markdown renders DURING streaming (`echo:` already bold while chunks are still arriving) -> the Draft is being run through the Markdown path, which makes a half-written code fence flip appearance mid-stream.
- The Draft never clears and the caret blinks indefinitely -> the completion signal never arrived.
- No `Stop` button appears beside a live Draft, or a `Stop` button appears beside a settled Message -> the Stop control is attached to the wrong row type.
- `Lines` grew by 3 or more -> the Draft is reaching the transcript file, which it must never do.

**Inconclusive if**

If nothing replies, first confirm you typed the at-sign (`hi @echo`, not `hi echo`) — the demo agent only replies when mentioned. Then check `T-A` for `Demo agent echo connected.`. If that line is absent, the demo agent never connected and this test plus ROOMMESSAGING-19, -20 and -14's interleaving half are all INCONCLUSIVE.

> [!NOTE]
> The chunks arrive roughly 40ms apart, so the streaming phase is short — consider screen-recording the first attempt so the chunked arrival can be judged frame by frame rather than from memory.

### ROOMMESSAGING-19 — The echo reply strips every '@' from the quoted text (loop safety)

**Free** · about 4 min

*Guards against an unbounded agent-to-agent loop: a reply that reproduces a mention would re-trigger another agent, and the recorded failure was 4299 messages in two seconds.*

**Before you start**

- The `echo` Room is open; `$room` is set.
- Demo agents are connected.

**Steps**

1. In `T-B` run `Lines` and write down the number.
2. In the composer type exactly `hi @echo and @alpha` and press **Enter**.
3. Read the reply row when it settles.
4. Search the reply text for any at-sign character.
5. Wait ten seconds and confirm nothing further arrives.
6. In `T-B` run `Lines` again.
7. In `T-B` run `Select-String -Path "$data\rooms\$room.jsonl" -Pattern '@' -SimpleMatch | Select-Object -Last 3` and read which lines contain an at-sign.

**Pass if — all of these**

- The reply reads `**echo:** hi echo and alpha` — every at-sign has been removed from the quoted portion.
- No second reply arrives; `alpha` is not a member of this Room and stays silent.
- `Lines` grew by exactly 2 and then stopped growing.
- The only JSONL lines containing an at-sign are your own Human Messages, never an agent reply.

**Fail if — any of these**

- An at-sign survives anywhere in the reply text -> loop safety is broken. In a Room holding two agents this produces an unbounded exchange. STOP THE APP IMMEDIATELY (Ctrl+C in `T-A`) before the transcript file grows, then report.
- `Lines` keeps climbing after you stop typing -> a loop is already running; stop the app now and report the line count reached.

**Inconclusive if**

If no reply arrives, this proves nothing — settle ROOMMESSAGING-18 first and record this INCONCLUSIVE pending that.

> [!NOTE]
> Re-run `Lines` a few seconds after the reply settles as a cheap safety check. A number that is still climbing is the unmistakable signature of the loop.

### ROOMMESSAGING-20 — The demo agent replies only to a real Mention, at a word boundary

**Free** · about 8 min

*Pins the Mention-matching boundary rules, in a three-Member Room where Mention-gating still applies. A two-Member Room now answers every Message per ADR-0004, so it can no longer isolate mention parsing — this test deliberately invites a bystander Agent first so a plain `hello` is still a valid negative baseline.*

**Before you start**

- The `echo` Room is open; `$room` is set.
- Both demo agents are connected. Type `/invite @alpha` and confirm the green strip and member line show three Members before starting the numbered steps — with only two Members every Message below would draw a reply regardless of Mention, and none of the boundary assertions would be meaningful.

**Steps**

1. In `T-B` run `Lines` and write down the number.
2. Send exactly `hello` (no at-sign). Wait ten seconds.
3. Note whether anything replied, and run `Lines` again.
4. Send exactly `hi @echo`. Wait for the reply.
5. Send exactly `hi @ECHO` (uppercase). Wait ten seconds.
6. Send exactly `hi @echoes`. Wait ten seconds.
7. Send exactly `mail me@example.com`. Wait ten seconds.
8. Send exactly `see-@echo`. Wait ten seconds.
9. In `T-B` run `Get-Content "$data\rooms\$room.jsonl" | ForEach-Object { $m = $_ | ConvertFrom-Json; "$($m.senderName): $($m.text)" }` and read the resulting conversation top to bottom.

**Pass if — all of these**

- `hello` produces no reply from either Agent, no Draft, no error, and no new agent line in the file — three Members keeps the Room Mention-gated, so a Message naming nobody wakes nobody.
- `hello` DOES produce a quiet blue note above the composer reading `No teammate was @-mentioned -
  name one to ask for a reply.`, and so does every other negative case below (`hi @echoes`,
  `mail me@example.com`). That note is the Room explaining the silence rather than leaving the
  tester to guess, and its presence is as much a pass condition as the absent reply.
- `hi @echo` produces exactly one reply, from `echo`.
- `hi @ECHO` produces exactly one reply — the match is case-insensitive.
- `hi @echoes` produces NO reply — a Mention ends at a word boundary.
- `mail me@example.com` produces NO reply — a letter immediately before the at-sign blocks it.
- `see-@echo` DOES produce a reply — a hyphen before the at-sign does not block it.
- `alpha` never replies to anything in this test — it is never Mentioned.
- The printed conversation shows agent lines only for the three positive cases.

**Fail if — any of these**

- `hello` produces a reply from either Agent -> either membership never actually reached three (recheck before filing) or the Room is answering unconditionally at three Members, which would be a Reply Gate regression.
- `hi @echoes` produces a reply -> the word-boundary check after a mention is gone; every longer word starting with an agent's Name now wakes it.
- `mail me@example.com` produces a reply -> email addresses are being read as Mentions; ordinary prose will start waking agents.
- `hi @ECHO` produces no reply -> Mention matching became case-sensitive.
- `see-@echo` produces no reply -> the blocked-character set before the at-sign is too broad and is now suppressing legitimate Mentions.

**Inconclusive if**

If `hi @echo` itself produces no reply, nothing in this test can be judged — the agent is not responding at all. Confirm `Demo agent echo connected.` in `T-A`, restart the app once, and record INCONCLUSIVE if it still does not reply. If the member line does not read three Members after `/invite @alpha`, stop and fix the invite before running the numbered steps.

> [!NOTE]
> This test deliberately keeps a third Member (`alpha`) in the Room for its whole duration. In the `echo` Room's ordinary two-Member form, ADR-0004 means `echo` answers every Message regardless of Mention, so a plain `hello` producing a reply there is CORRECT and must never be filed as a bug — see STARTUPCONFIG-04 and REPLYGATEBUDGET-01. That two-Member rule is exactly what this test's extra Member exists to neutralise, so the boundary cases above stay meaningful.

### ROOMMESSAGING-21 — /invite @name from the composer: green info line, Room rename, live sidebar update

**Free** · about 6 min

*Proves a slash command is handled as a command — never posted as a Message — and that the resulting membership change propagates to the header, the members line and the sidebar with no refresh.*

**Before you start**

- The `echo` Room is open; `$room` is set to its id.
- Both demo agents are connected and `alpha` is NOT already a member of this Room.
- If `alpha` is already a member, do a full RESET (stop app, delete `team.db`, relaunch) before running this.

**Steps**

1. In `T-B` run `Lines` and write down the number.
2. Note the current `<h1>`, the members line beneath it, and the sidebar entry text for this Room.
3. In the composer type exactly `/invite @alpha` and press **Enter**.
4. Read the line that appears above the composer, and note its colour.
5. Read the `<h1>` and the members line again.
6. Read the sidebar entry for this Room again — do NOT refresh the page.
7. In `T-B` run `Lines` again.
8. Look at `T-A`'s console output for a line mentioning an invitation.
9. Send an ordinary Message `after invite` and check whether the line above the composer is still there.
10. Send `hi @alpha` and confirm alpha now replies in this Room.

**Pass if — all of these**

- No Message row is added to the transcript for the `/invite` command itself.
- A GREEN line appears above the composer reading exactly `Invited alpha. Room is now "echo, alpha".`
- The `<h1>` now reads `echo, alpha`.
- The members line now reads `You, echo, alpha`.
- The sidebar entry for this Room now reads `echo, alpha`, with no page refresh.
- `Lines` is unchanged by the invite.
- `T-A` shows a line of the form `Invited agent 'alpha' (<id>) into room '<roomId>'.`
- After sending `after invite`, the green line is gone.
- `hi @alpha` now gets a reply in this Room.

**Fail if — any of these**

- A transcript row appears containing the text `/invite @alpha` -> a slash-prefixed submit reached the Transcript, which it must never do.
- `Lines` increased -> the command was persisted as a Message.
- The line above the composer is RED rather than green -> a successful invite is being presented in the error style.
- The header renames but the sidebar entry does not -> the rooms-changed event is not reaching the sidebar; a refresh would be needed, which defeats the design.
- The green line persists after the next successful send -> the info line is not cleared at the start of a new submit and will mislead about a later action.

**Inconclusive if**

If `alpha` is already a member, the invite will behave differently and the rename will be a no-op — do the RESET named in the preconditions and re-run, rather than judging. If `alpha` never connected (no `Demo agent alpha connected.` in `T-A`), record INCONCLUSIVE.

> [!NOTE]
> Optional cross-check with sqlite3: `sqlite3 "$data\team.db" "select name from rooms where id='<roomId>';"` must now return `echo, alpha`, and `select count(*) from room_members where room_id='<roomId>';` must return 3.

### ROOMMESSAGING-22 — Composer errors: unknown command, unknown agent, and the typed text is lost

**Free** · about 6 min

*Proves the exact error strings reach the Human in the error style, that nothing is posted, and characterises the documented text-loss defect precisely enough to file once.*

**Before you start**

- A Room is open; `$room` is set.

**Steps**

1. In `T-B` run `Lines` and write down the number.
2. Type exactly `/hello` and press **Enter**.
3. Read the line above the composer and note its colour and exact wording.
4. Look at the textarea and note whether your typed text is still there.
5. Type exactly `/invite` (nothing after it) and press **Enter**. Read the line above the composer.
6. Type exactly `/invite @nobody` and press **Enter**. Read the line above the composer.
7. Type exactly `/usr/local/bin is where it lives` and press **Enter**. Read the line above the composer and check the transcript.
8. In `T-B` run `Lines` again.
9. Send an ordinary Message `recovered` and check whether the red line disappears.
10. Check `T-A` for any invitation log line.

**Pass if — all of these**

- `/hello` produces a RED line above the composer reading exactly `Unknown command`, and posts nothing.
- `/invite` alone produces the same RED line reading exactly `Unknown command`.
- `/invite @nobody` produces a RED line reading exactly `Unknown agent @nobody`.
- `/usr/local/bin is where it lives` produces a RED line reading `Unknown command` and is NOT posted.
- In every failing case the textarea is already empty — the typed text is gone.
- `Lines` is unchanged across all four attempts.
- After sending `recovered`, the red line is gone.
- `T-A` shows no `Invited agent` line for any of these.

**Fail if — any of these**

- Any error text differs from the quoted string -> the wording the Human relies on has changed and no test pins it.
- No line appears at all for a rejected submit -> the failure is silent and the Human has no idea why nothing posted.
- A slash-prefixed message is posted as an ordinary Message -> the command guard is not running.
- The red line survives a subsequent successful send -> a stale error will be read as applying to the new Message.
- `Lines` increased for any of these -> a rejected submit still reached the Transcript.

**Inconclusive if**

If the composer shows no line at all AND nothing posts, you cannot tell a missing error line from a dead submit path. Send a valid Message first to confirm posting still works, then re-run — if valid sends work and errors are silent, that is a FAIL, not INCONCLUSIVE.

> [!NOTE]
> TWO FINDINGS TO RECORD (both current behaviour, so file as findings, not regressions): (1) the textarea is cleared by JavaScript immediately after the send is dispatched and before the server's answer arrives, so a rejected submit destroys what the Human typed with no way to recover or edit it; (2) a legitimate message that merely begins with a slash — a file path like `/usr/local/bin ...` — is rejected as `Unknown command` and can never be posted.

### ROOMMESSAGING-23 — Typed-but-unsent composer text carries over into the Room you switch to

**Free** · about 6 min

*Characterises a documented cross-Room behaviour precisely, and separates it from the outright bug it could be mistaken for (posting into the OLD Room after a switch).*

**Before you start**

- At least two Rooms exist in the sidebar (`echo` and `alpha` by default).

**Steps**

1. Open the `echo` Room and note its 32-hex id from the address bar. Call this ID-ECHO.
2. In the composer type exactly `this was meant for echo` and DO NOT press Enter.
3. Click `alpha` in the sidebar.
4. Note the id now in the address bar. Call this ID-ALPHA.
5. Look at the composer and note whether it still contains the typed text.
6. Press **Enter**.
7. Read which Room's transcript the Message lands in.
8. In `T-B` run `Select-String -Path "$data\rooms\$($idAlpha).jsonl" -Pattern 'meant for echo' -SimpleMatch` and the same against ID-ECHO's file (substitute the real ids).
9. Now the second half: open the `echo` Room, type `second attempt` without pressing Enter, click `Teammates` in the sidebar, then click a Room in the sidebar to come back.
10. Look at the composer.

**Pass if — all of these**

- After the Room switch, the composer still contains `this was meant for echo`.
- Pressing Enter posts it into the Room whose id is in the address bar at that moment — ID-ALPHA.
- The text appears in ID-ALPHA's JSONL file and does NOT appear in ID-ECHO's.
- After navigating away to `Teammates` and back into a Room, the composer is EMPTY.

**Fail if — any of these**

- The Message posts into ID-ECHO after you switched to alpha -> the composer is still bound to a stale Room id; a Message can land in a Room the Human is not looking at. This is an outright bug and outranks everything else here.
- The text lands in neither file but appears on screen -> the post did not reach disk.
- The composer is empty immediately after the Room switch AND navigating away also clears it -> not a failure, but note it: the behaviour has changed and this test's premise no longer holds.

**Inconclusive if**

If only one Room exists, this cannot be run — create a second Room (`pwsh tools/echo-bot.ps1 -Name mybot` in `T-B` creates one) and re-run, or record INCONCLUSIVE.

> [!NOTE]
> The carry-over itself is CURRENT BEHAVIOUR, not a crash: the Room page is reused across route-parameter changes, so the same textarea element survives the switch and is not bound to any per-Room state. Record it as a finding with the exact reproduction. The genuine bug is only the stale-Room variant in the fail list.

### ROOMMESSAGING-24 — The JSONL Transcript: shape, append-only, and one file per Room

**Free** · about 6 min

*Establishes and validates the oracle that every other test in this area relies on — and proves posting in one Room never touches another Room's file.*

**Before you start**

- At least one Message has been posted in the `echo` Room and at least one in the `alpha` Room.
- `$data` is set in `T-B`.

**Steps**

1. In `T-B` run `Get-ChildItem "$data\rooms"` and list the files you see.
2. Confirm each filename is 32 hexadecimal characters followed by `.jsonl`.
3. Open the `echo` Room's file with `Get-Content "$data\rooms\<echoId>.jsonl"` and read every line.
4. Confirm each line is one complete JSON object on one line, and that every line has the fields `id`, `timestamp`, `senderId`, `senderName`, `text` and nothing else.
5. Read a `timestamp` value and confirm it ends with `+00:00`.
6. Find a Human line and confirm it has `"senderId":"human"` and `"senderName":"You"`.
7. Find an agent line and confirm its `senderId` is 32 hex characters and its `senderName` is the agent's Name.
8. Copy the whole `echo` file to a snapshot: `Copy-Item "$data\rooms\<echoId>.jsonl" "$env:TEMP\before.jsonl"`.
9. In the browser, post three more Messages into the `echo` Room.
10. Run `Compare-Object (Get-Content "$env:TEMP\before.jsonl") (Get-Content "$data\rooms\<echoId>.jsonl")` and read the result.
11. Record `alpha`'s file length, then post a Message in `echo`, then re-check `alpha`'s file length.

**Pass if — all of these**

- Every file under `$data\rooms` is named with exactly 32 hex characters plus `.jsonl`.
- Every line parses as one JSON object and carries exactly the five fields named above.
- Every `timestamp` is UTC and ends `+00:00`.
- Human lines carry `"senderId":"human"` and `"senderName":"You"`; agent lines carry a 32-hex `senderId` and the agent's Name.
- The `Compare-Object` result shows ONLY added lines (side indicator `=>`), never a changed or removed one — the file is append-only.
- Posting in `echo` leaves `alpha`'s file length unchanged.

**Fail if — any of these**

- An existing line changed after new posts -> the file is being rewritten, not appended to, and earlier history is now at risk.
- A Message appears in the wrong Room's file -> Room isolation is broken.
- Two JSON objects share one line, or the file's last line has no trailing newline -> the line-per-Message format is broken and a reader will mis-parse it.
- A transcript file appears outside `$data\rooms\` -> the path derivation changed; that path derivation is also the traversal guard.
- A field is missing, renamed, or an extra field appears -> the on-disk format changed and every consumer of it, including this test suite, now reads stale assumptions.

**Inconclusive if**

If `$data\rooms` does not exist at all, no Message has been posted anywhere yet — post one from the browser and restart this test. An absent folder on a fresh install is normal and is NOT a finding.

> [!NOTE]
> Optional cross-check with sqlite3 to map a filename back to a sidebar entry: `sqlite3 "$data\team.db" "select id, name from rooms;"`. Room names and membership live only in the database, never in the JSONL.

### ROOMMESSAGING-25 — An external pipe client's Room and Messages appear live, with no refresh

**Free** · about 8 min

*Proves the live event path works for a third-party client the app did not start, and that a client disconnecting does not take the browser session down with it.*

**Before you start**

- The app is running and a Room is open in the browser.
- PowerShell is available in `T-B`.

**Steps**

1. With a Room open in the browser, look at the sidebar and note its contents.
2. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name mybot`.
3. Wait for the bot to print that it sent hello, then look at the browser sidebar WITHOUT refreshing.
4. Click the new `mybot` entry in the sidebar.
5. Note the 32-hex id in the address bar and set `$room` to it in a THIRD terminal (or reuse `T-B` after the bot exits — do not stop the bot yet).
6. Type `hi @mybot` and press **Enter**. Watch the transcript without refreshing.
7. Check `T-A`'s console for a line about a room being created for an agent.
8. Press Ctrl+C in the bot's terminal to stop it.
9. Look at the browser: check the Room is still in the sidebar, the transcript is intact, and the composer still accepts text.
10. Type `still here` and press **Enter**.
11. Look at the bottom-right of the page for a yellow error banner.

**Pass if — all of these**

- A Room named `mybot` appears in the sidebar with no page refresh.
- Opening it and sending `hi @mybot` produces a reply in the browser with no reload.
- `T-A` shows a line of the form `Created direct room '<roomId>' for agent 'mybot'.`
- A JSONL file for the new Room exists under `$data\rooms` after the first Message.
- After Ctrl+C on the bot, the Room stays in the sidebar, the transcript stays intact, and `still here` posts successfully (with no reply).
- No yellow banner reading `An unhandled error has occurred.` appears at any point.

**Fail if — any of these**

- The new Room appears only after a manual refresh -> the rooms-changed event is not reaching the sidebar.
- The reply appears only after a manual refresh -> the message event is not reaching the Room page.
- The sidebar shows `mybot` twice -> the Room is being created or listed more than once.
- The browser session dies or the yellow `An unhandled error has occurred.` banner appears when the bot disconnects -> a disconnect is propagating an exception into the browser's live connection; this is the signature of a leaked event subscription that was never unsubscribed.

**Inconclusive if**

If the bot script cannot connect (it prints a connection error), the pipe is not reachable — confirm the app is running and that no other process holds the pipe, then retry once. If it still cannot connect, record INCONCLUSIVE; do not fail the app for a script that never started.

> [!NOTE]
> Leave the bot running if you intend to run ROOMMESSAGING-26 next — that test uses the same script with a different name.

### ROOMMESSAGING-26 — A Mention of a Name containing spaces resolves against the Room's Members

**Free** · about 7 min

*Proves multi-word Names are matched whole rather than truncated at the first space — the classic parser trap — without paying for a Persona.*

**Before you start**

- The app is running.
- PowerShell is available. Stop any previously started bot first.

**Steps**

1. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name "Chief of Staff"` (keep the quotes).
2. Wait for the bot to report that it sent hello.
3. Look at the browser sidebar without refreshing and find a Room named `Chief of Staff`.
4. Open that Room and read the members line beneath the `<h1>`.
5. Type exactly `hello @Chief of Staff` and press **Enter**. Wait ten seconds.
6. Note whether a reply arrived.
7. Type exactly `hello @Chief` and press **Enter**. Wait ten seconds.
8. Note whether a reply arrived.
9. Type exactly `hello @Chief of Staffing` and press **Enter**. Wait ten seconds.
10. Note whether a reply arrived.
11. In `T-B` (a second one, or after stopping the bot) run `Tail` against this Room's JSONL and read the stored text of each of your three Messages.

**Pass if — all of these**

- A Room named `Chief of Staff` appears in the sidebar with no refresh.
- The members line reads `You, Chief of Staff`.
- `hello @Chief of Staff` produces a reply — the whole multi-word Name was treated as one Mention.
- `hello @Chief` produces NO reply — `Chief` is not a Member Name.
- `hello @Chief of Staffing` produces NO reply — the Mention must end at a word boundary.
- The JSONL stores each Message's text verbatim, at-signs and spaces included.

**Fail if — any of these**

- `hello @Chief of Staff` produces no reply -> the Mention is being truncated at the first space; multi-word Names can never be mentioned, and any error message would name the wrong person.
- `hello @Chief` produces a reply -> a prefix of a Member Name is matching, so unrelated words will wake agents.
- `hello @Chief of Staffing` produces a reply -> the word-boundary check after a multi-word Name is missing.

**Inconclusive if**

If the bot does not connect or no Room appears, record INCONCLUSIVE — the Mention logic was never exercised. Do not substitute a single-word bot and claim the multi-word case passed.

> [!NOTE]
> Optional cross-check with sqlite3: `sqlite3 "$data\team.db" "select name from users;"` must show `Chief of Staff` with its interior spaces intact. Mentions themselves are never persisted — they are a property of delivery, not of the Message — so do not look for them in the JSONL.

### ROOMMESSAGING-27 — appearance.json no longer customises the Message font: the body just follows the Theme, and a legacy override key is silently inert

**Free** · about 7 min

*This test used to prove a `--font-chat` override was scoped to message bodies only. Stage 2 of the MudBlazor migration deleted the whole per-token override system — [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md): "the safest thing that could happen to a sole defence is for the feature it defends to stop existing" — so there is no longer any way to give the chat transcript its own font short of editing `ThemeCatalog` in C# and rebuilding (`docs/agencyteam/known-limits.md`). What is left to prove: the Message body's font comes from the same Theme typography as the rest of the app (there is no separate "chat font" any more), and a hand-edited file still carrying the old `overrides` key is read, its unknown key preserved, and never rewritten.*

**Before you start**

- The app is running and a Room with Messages is open.
- `$data` is set in `T-B`.

**Steps**

1. In DevTools, select an element with class `message-body`, open the Computed panel, and read its `font-family`. Select an element with class `message-sender` (or the `<h1>` room heading) and read its computed `font-family` too. Write both down.
2. In `T-B` write a file carrying the old, now-meaningless override shape: `'{"theme":"huddle","overrides":{"--font-chat":"Georgia, serif"}}' | Set-Content -Path "$data\appearance.json" -Encoding utf8`.
3. In `T-B` record a checksum: `(Get-FileHash "$data\appearance.json").Hash` and write it down.
4. Reload the browser page (F5).
5. Look at the Message bodies in the transcript. Compare against what you wrote down in step 1 — is anything serif?
6. Re-read the computed `font-family` on a `message-body` element and on a `message-sender` element; compare both against step 1.
7. Send `hi @echo` and, while the Draft is streaming, note the Draft text's typeface against the rest of the transcript.
8. In `T-B` run `(Get-FileHash "$data\appearance.json").Hash` again and compare with what you wrote down in step 3.
9. In `T-B` run `Get-Content "$data\appearance.json"` and confirm the `overrides` key you wrote in step 2 is still there, untouched.
10. In `T-B` run `Remove-Item "$data\appearance.json"` and reload the browser to restore the default.

**Pass if — all of these**

- Nothing anywhere turns serif — the `overrides.--font-chat` key has no visible effect at all.
- The computed `font-family` on `.message-body` is unchanged from step 1, and is IDENTICAL to the computed `font-family` on `.message-sender` — the message body draws its font from the same Theme typography as the rest of the app, not a scoped token of its own.
- The streaming Draft's typeface matches the rest of the transcript (it shares the message-body styling — this is correct).
- The file's checksum is unchanged after the reload — the app read it and did not rewrite it.
- Step 9 shows the `overrides` key is still present, byte for byte — an unknown top-level key is kept, never silently deleted (`AppearanceStore`'s own documented tolerance).
- Deleting the file and reloading changes nothing observable, since the file was already inert.

**Fail if — any of these**

- Any text anywhere turns serif -> a per-token override path was reintroduced without the allowlist and validation ADR-0010 deliberately removed; treat this as a regression of a closed decision, not a missing feature.
- `.message-body` and `.message-sender` resolve to DIFFERENT computed font families in the default (no-override) state -> there is a distinct chat-font mechanism after all and this test's premise needs re-checking against `ThemeCatalog.cs` before filing anything.
- The file's checksum changed, or the `overrides` key vanished from the file -> the app rewrote a file a human hand-edited, destroying hand-typed content — `AppearanceStore.Save` is documented to leave every key it does not itself manage exactly as found.
- The `Appearance` tab at `/settings` shows any UI for customising the message font -> the removed feature was partially reintroduced.

**Inconclusive if**

If `.message-body` and `.message-sender` already read different font families on a stock install, `ThemeCatalog.BuildHuddleTheme` has started setting `Typography.Body1` distinctly from `Typography.Default` — re-read that file before judging this test, since the "no scoped chat font" premise no longer holds and the test needs rewriting again, not a guess.

> [!NOTE]
> Only the transcript half of the appearance check belongs here — but since 2026-09-22 that includes avatars, which the Room view now renders beside every Message and every Draft. Check here that each row has one, that it is the sender's, and that the gutter does not shift when a Draft settles into a Message. What still belongs on `/teammates` is *choosing* an avatar: the three-way selector, the upload and the colour picker are edited on a Teammate's card, and the Human's own on Settings → Appearance.

### ROOMMESSAGING-28 — A torn or corrupt line in the Transcript is skipped with a warning, not fatal

**Free** · about 8 min

*Proves the transcript reader tolerates a half-written or garbage line — the failure mode a crash or an unclean shutdown actually produces — and says so in the log rather than silently dropping content.*

**Before you start**

- A Room with at least four well-formed Messages; you know its 32-hex id.
- You are willing to stop and restart the app.

**Steps**

1. In the browser, note the exact text of every Message in the Room, in order.
2. In `T-A` press Ctrl+C to stop the app.
3. In `T-B` back up the file: `Copy-Item "$data\rooms\$room.jsonl" "$env:TEMP\good.jsonl"`.
4. In `T-B` append a truncated line: `Add-Content -Path "$data\rooms\$room.jsonl" -Value '{"id":"x"'`.
5. In `T-B` insert a garbage line in the middle: read the file into an array, splice `not json at all` in after the second line, and write it back — `$l = Get-Content "$data\rooms\$room.jsonl"; $new = $l[0..1] + 'not json at all' + $l[2..($l.Count-1)]; Set-Content -Path "$data\rooms\$room.jsonl" -Value $new`.
6. In `T-B` also append one completely blank line: `Add-Content -Path "$data\rooms\$room.jsonl" -Value ''`.
7. In `T-A` relaunch: `$env:Team__Acp__Enabled = 'false'` then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
8. Open that Room in the browser and read the transcript.
9. Compare it with the list you noted at step 1.
10. Read `T-A`'s console output and count the warnings mentioning an unparsable line, noting each line number named.
11. Restore the good file: stop the app, `Copy-Item "$env:TEMP\good.jsonl" "$data\rooms\$room.jsonl" -Force`, relaunch.

**Pass if — all of these**

- The Room opens normally — no exception page, no error banner, no blank main column.
- Every well-formed Message still renders, in the original order.
- The malformed lines do not appear as rows.
- `T-A` shows one warning of the form `Skipping unparsable line <n> in room <roomId>.` per malformed line PER READ of the file — two per read, for the truncated line and the garbage line. The Transcript is re-read on every render of the Room, so the pair repeats: a prerender plus a few navigations gives six pairs, twelve lines. Count distinct line NUMBERS, not warning lines.
- The line numbers named in those warnings correspond to the actual positions of the bad lines in the file.
- The blank line produces no warning and no row.

**Fail if — any of these**

- The Room fails to open, or an exception page appears -> one bad line takes out the whole transcript; an unclean shutdown would make a Room permanently unopenable.
- The transcript truncates at the bad line and every later Message is missing -> the reader stops at the first failure instead of skipping it, silently hiding history.
- No warning appears in the console for a malformed line -> the line was dropped silently by something else, and a corrupted transcript would be undetectable in operation.
- A warning names the wrong line number -> the diagnostic cannot be used to find the bad line.

**Inconclusive if**

If you cannot tell which physical line numbers your edits produced (line endings, encoding), judge only "a warning appeared per bad line" and record the line-number accuracy as INCONCLUSIVE. If the file cannot be edited because the app still holds it, wait five seconds after Ctrl+C and retry — SQLite/file handles can linger briefly.

> [!NOTE]
> ALWAYS restore the backup at step 11. Leaving a corrupted transcript in place will make every later test's line counting wrong.

### ROOMMESSAGING-29 — The transcript survives an application restart; Drafts and the Budget do not

**Free** · about 8 min

*Proves the JSONL file is the only durable store — and pins the two deliberate losses so a tester does not file them.*

**Before you start**

- A Room with several Messages; you know its id.
- You are willing to stop and restart the app.

**Steps**

1. In the browser, note the exact text, sender and time of every Message in the Room.
2. In `T-B` run `(Get-FileHash "$data\rooms\$room.jsonl").Hash` and write it down.
3. In the browser, send `hi @echo` and, WHILE the Draft is still streaming, immediately press Ctrl+C in `T-A` to stop the app.
4. Relaunch in `T-A`: `$env:Team__Acp__Enabled = 'false'` then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
5. Reload the browser and reopen the same Room from the sidebar.
6. Compare the transcript with what you noted at step 1.
7. Note whether any streaming Draft row, caret or `Stop` button is present.
8. Note whether any budget panel or grey budget line is present.
9. In `T-B` run `(Get-FileHash "$data\rooms\$room.jsonl").Hash` and compare with what you wrote down (it will differ only by whatever Messages actually completed before the stop).
10. Now the inverse: stop the app, run `Remove-Item "$data\team.db*"`, relaunch, and look at the sidebar and at `$data\rooms`.

**Pass if — all of these**

- After the restart, every Message that had completed before the stop is still present, in order, with the same senders and times.
- No Draft row survives the restart — the streaming reply is simply gone.
- No budget pause or budget note survives the restart.
- The transcript file itself is unchanged by the act of restarting (only completed Messages were ever written to it).
- After deleting the database and relaunching, the Rooms vanish from the sidebar while the `.jsonl` files remain on disk.

**Fail if — any of these**

- Messages that were visible before the restart are missing afterwards -> they were never flushed to disk; that is real data loss, and the store writes and flushes per Message so this would be a genuine defect.
- A Draft's text appears in the transcript file -> a Draft reached the durable store, which it must never do.
- Deleting the database also destroys the `.jsonl` files -> the two stores are coupled, contradicting the design in which they are deliberately separate.

**Inconclusive if**

If the Draft finishes before you manage to press Ctrl+C, the Draft-loss half was not exercised — retry with a longer reply, or record that sub-case INCONCLUSIVE. If `team.db` cannot be deleted because a handle lingers, wait five seconds and retry; that lingering handle is a known pre-existing issue and not a finding.

> [!NOTE]
> The Draft loss and the Budget reset after a restart are DOCUMENTED AND DELIBERATE — do not file them. The Budget counter lives in memory and per Room, so a restart un-pauses every Room and shows a fresh allowance over a Transcript that already spent one. Step 10 destroys the Rooms; run it last, and be prepared to re-do earlier setup afterwards.

### ROOMMESSAGING-30 — The Budget pause panel sits between transcript and composer, and a Human Message resumes it

**Free** · about 12 min

*Proves the runaway-agent guard: the pause is visible in the right place, survives a reload, counts correctly, cannot be double-granted, and is cleared by the Human speaking.*

**Before you start**

- You are willing to stop and relaunch the app with a different environment variable.
- Demo agents connect on launch.

**Steps**

1. In `T-A` press Ctrl+C to stop the app.
2. In `T-A` run `$env:Team__Acp__Enabled = 'false'` and `$env:Team__AgentMessageBudget = '2'`.
3. In `T-A` relaunch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
4. Open the `echo` Room in the browser and set `$room` in `T-B` to its id.
5. Send `hi @echo` and wait for the reply.
6. Read any grey line between the transcript and the composer and write down its exact wording.
7. Put a SECOND Agent in the Room - type `/invite @alpha` and press Enter - then send ONE Message mentioning both: `hi @echo and @alpha`. Wait for both replies.

   > A Human Message RESETS this counter (the note says "since you last spoke"), so alternating
   > Human/agent Messages can never reach a budget above 1: each Message you type zeroes it and
   > draws exactly one reply. Two replies have to arrive between two Human Messages, which needs
   > two Agents in the Room. The same rule governs `STARTUPCONFIG-33`.
8. Look between the transcript and the composer and read the panel that appears, plus the labels of its two buttons.
9. Confirm the panel sits BELOW the last transcript row and ABOVE the composer, not above the transcript.
10. Press F5 and check whether the panel is still there.
11. Provoke a REFUSAL, which needs its own configuration: stop the app, relaunch with `$env:Team__AgentMessageBudget = '1'`, and send one `hi @echo and @alpha`. The first Agent to answer spends the budget; the second is refused. (Typing a third Message instead would reset the counter and simply get a reply - see the note at step 7.) Watch `T-A`'s console.
12. Click **Leave paused** and read what replaces the panel.
13. Now send an ordinary Message `resuming` and check whether the pause clears.
14. Send `hi @echo` and confirm the agent replies again.
15. Reach the pause once more (two more mentions), then click **Continue** and immediately try to click it a second time; read its label while the grant is in flight.
16. Read `T-A`'s console for the extension line.
17. In `T-B` run `Get-Content "$data\rooms\$room.jsonl" | ForEach-Object { ($_ | ConvertFrom-Json).senderName }` and count the `echo` entries between your Human Messages.

**Pass if — all of these**

- After the first reply, a grey line reads exactly `1 of 2 agent replies since you last spoke.`
- After the second reply, a panel appears reading exactly `Agents have sent 2 replies since you last spoke, and are paused.` with buttons labelled `Continue` and `Leave paused`.
- The panel sits between the last transcript row and the composer.
- The panel is still present after F5.
- At the budget of 1, the second Agent's reply is refused: it never appears as a row, and `T-A` logs `Room '<roomId>' refused a message from '<agent>': its budget of 1 agent messages since a human last spoke is spent.` (The number in that line is the configured budget.) The refused Agent's Draft text stays on screen unrendered - literal `**echo:**` asterisks - because a Draft only clears when its Message posts; that is expected here, not a stuck Draft.
- Clicking `Leave paused` replaces the panel with a grey line reading exactly `Paused — 2 of 2 agent replies since you last spoke.`
- Sending `resuming` clears the pause, and a following `@echo` mention gets a reply.
- While a `Continue` grant is in flight the button is disabled and reads `Continuing…`.
- `T-A` logs `Room '<roomId>' was extended to 4 agent messages.` on Continue.
- The refused reply appears nowhere in the JSONL — only Messages that reached the Transcript count against the Budget.

**Fail if — any of these**

- The panel renders ABOVE the transcript -> the Human is asked to grant more spending without being able to read the exchange that spent the last grant.
- The panel vanishes after F5 -> the Budget is only delivered by a live event and not read on render; a reconnected or prerendered page would show a paused Room as running.
- The counts in the text are wrong (for example `1 of 2` after two replies) -> the counter and the displayed figure disagree.
- `Continue` stays clickable while the first grant is in flight -> a double-click grants twice, and granting budget is what allows real money to be spent.
- The `Leave paused` dismissal survives the next Message -> a Room that is moving again still reads as dismissed and the next pause will be suppressed.
- The refused agent reply appears in the JSONL -> a refused Message reached the Transcript.

**Inconclusive if**

If the agents never reply at all, no Budget can be spent and nothing here is testable — confirm `Demo agent echo connected.` in `T-A` and that the env var took effect (`$env:Team__AgentMessageBudget` should echo `2`). If the pause never appears after several replies, check the env var was set in the SAME terminal that launched the app before recording a FAIL.

> [!NOTE]
> Remember to clear the env var (`Remove-Item Env:Team__AgentMessageBudget`) and relaunch before running any other test, or every later test will pause after two agent replies. A documented NON-bug: a Message declined for Budget is held for re-delivery rather than kept as catch-up, so if you leave a Room paused and then type instead of clicking Continue, the agent's next prompt will not carry the Message it was paused on — though that Message is still in the Transcript and still on screen.

### ROOMMESSAGING-31 — The Room view survives a server stop and restart with the browser open

**Free** · about 8 min

*Catches the repo's worst silent failure — a missing scoped-CSS bundle, which produces no build warning, no startup error and no log line, and is visible only as every reconnect state paragraph showing at once. This is not hypothetical: Stage 2 of the MudBlazor migration tokenised `ReconnectModal.razor.css` and the first cut left its `var()` references pointing at deleted tokens — invisible because the modal renders only at the one moment it exists for, a dropped circuit. The modal itself is a native `<dialog>`, not a `MudDialog`, so its backdrop is the browser's own `::backdrop`, now painted with `--mud-palette-overlay-dark`.*

**Before you start**

- A Room with Messages is open in the browser.
- You are willing to stop and restart the app.

**Steps**

1. With the Room open, press Ctrl+C in `T-A` to stop the server.
2. Watch the browser for a few seconds.
3. Count how many distinct state paragraphs are visible inside the reconnect overlay.
4. Note whether a dimmed backdrop covers the page behind it.
5. While disconnected, type `sent while down` in the composer and press Enter.
6. In `T-A` relaunch: `$env:Team__Acp__Enabled = 'false'` then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
7. Let the browser reconnect on its own; if it does not, reload the page.
8. Read the transcript and confirm it is intact.
9. Send `back online` and confirm it posts.
10. Look at the bottom of the page for a yellow banner.
11. In `T-B` run `curl.exe http://localhost:5100/ -o "$env:TEMP\shell.html"`, then `Select-String -Path "$env:TEMP\shell.html" -Pattern 'styles.css' -SimpleMatch` and read every stylesheet link it emits.
12. For each stylesheet URL found, run `curl.exe -I "http://localhost:5100/<that path>"` and confirm the status line is 200, not 404.
13. In `T-B` confirm `sent while down` is absent: `Select-String -Path "$data\rooms\$room.jsonl" -Pattern 'sent while down' -SimpleMatch`.

**Pass if — all of these**

- Exactly ONE state paragraph is visible in the reconnect overlay at a time.
- A dimmed backdrop covers the page behind the overlay.
- After the server restarts, the browser reconnects (or reloads cleanly) and the transcript is intact.
- `back online` posts successfully.
- No yellow banner reading `An unhandled error has occurred.` is visible at any point.
- Every stylesheet the page shell links resolves with a 200, including the fingerprinted component-styles bundle.
- `sent while down` appears nowhere in the JSONL — a Message typed while disconnected is neither posted nor queued.

**Fail if — any of these**

- SIX mutually exclusive state paragraphs are visible at once in the overlay -> the component-scoped CSS bundle is not loading. This is the highest-priority silent failure in the repo: it produces no build warning, no startup error and no log line, and it disables EVERY scoped stylesheet in the app at once. Confirm with the stylesheet 404 check at step 12 and report immediately.
- Any stylesheet URL the shell links returns 404 -> as above; name the exact URL in the report.
- The yellow `An unhandled error has occurred.` banner is visible at any point -> an exception reached the browser's live connection; capture the console output before reloading.
- `sent while down` is present in the JSONL after reconnect -> a Message sent to a dead server was replayed; the Human has no way to know when that will happen.
- The transcript is empty after reconnect -> the page is not re-reading the store on reconnect.

**Inconclusive if**

If the overlay never appears because the browser closed the connection instantly and reloaded itself, the paragraph count cannot be judged — judge PASS/FAIL on the stylesheet resolution check at steps 11-12 alone, which tests the same underlying failure, and record the overlay sub-case as INCONCLUSIVE.

> [!NOTE]
> Steps 11-12 are the real test here, and they can also be run on their own at any time. The overlay is only the visible symptom.

### ROOMMESSAGING-32 — A real Claude Agent's reply streams and renders through the same path (COSTS MONEY)

**💰 Spends money** · about 15 min

*Proves the real agent path produces the same Draft-then-rendered-Markdown behaviour the free demo agent does, and that tool-activity lines never reach the Transcript.*

**Before you start**

- `node` is on PATH (`node --version` succeeds).
- The agent adapter is installed under `E:\Repos\Huddle\tools\acp\node_modules`.
- You accept a small token charge: ONE prompt, ONE short turn — a few hundred tokens on Haiku at low effort. Roughly the cost of a single short chat message.
- Every other test in this area has already been run for free; do NOT use this test to check Markdown cases the echo agent already proved.

**Steps**

1. In `T-A` press Ctrl+C to stop the app.
2. In `T-A` clear the ACP override so Development config can turn it on: `Remove-Item Env:Team__Acp__Enabled -ErrorAction SilentlyContinue`. Also clear any budget override: `$env:Team__AgentMessageBudget = '4'`.
3. In `T-A` relaunch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`. This launch WILL start real agents and CAN spend money — keep the session short.
4. In the browser click `Teammates` in the sidebar.
5. Click the `New teammate` button.
6. Fill in a Name, then in the `Model` dropdown select **Haiku**, and in the `Effort` dropdown select **low**. These two settings are the standing convention and keep the charge small — do not pick any other model or effort.
7. Click `Add teammate` to save.
8. Wait for the teammate to come online, then open its Room from the sidebar. Set `$room` in `T-B` to that Room's id and run `Lines`.
9. Type ONE prompt and press Enter: `In under 80 words, reply with a short heading, a three-item bullet list, a fenced code block of one line, and a link to https://example.com`.
10. Watch the reply arrive: note whether a streaming row appears with the sender's Name, a `Stop` button, a blinking caret, and plain (unrendered) text.
11. Watch for a muted italic line beneath the streaming text describing tool activity, if any appears.
12. When the reply settles, read the final row: check the heading, bullets, code block and link are all rendered as real formatting rather than literal Markdown characters.
13. Confirm no tool-activity line remains beneath the settled Message.
14. In `T-B` run `Lines` and `Tail`.
15. STOP THE APP (Ctrl+C in `T-A`) as soon as you have judged the result, so no further tokens can be spent.

**Pass if — all of these**

- A streaming Draft row appears with the teammate's Name, a `Stop` button and a blinking caret.
- The Draft text is plain during streaming — Markdown syntax is visible as literal characters.
- The Draft is replaced by a settled Message row in which the heading, bullets, fenced code block and link are all rendered as real formatting.
- No tool-activity line remains visible once the Message has settled.
- `Lines` grew by exactly 2 — your prompt and the reply.
- The last JSONL line carries the teammate's Name as `senderName`, and the tool-activity text appears nowhere in the file.
- `T-A` shows the agent starting with no warning about a model not being in the agent's catalog.

**Fail if — any of these**

- No Draft ever appears and only the finished Message shows up -> the streaming delta path is dead for real agents even though it works for the demo agent.
- The Draft appears but is never replaced by a settled Message -> the completion signal is not arriving; the caret will blink indefinitely.
- The reply renders as literal Markdown characters -> the Markdown path is broken specifically for agent-authored text.
- A tool-activity line persists after the Message settles, or appears in the JSONL -> activity lines are leaking into the durable Transcript, which must only ever record what an agent SAID.
- `T-A` warns that the chosen model is not in the agent's catalog -> the Haiku selection did not reach the adapter and the turn may have run on a different, more expensive model; stop immediately and report.

**Inconclusive if**

If `node` is missing, the adapter is not installed, or the teammate never comes online, NOTHING here is testable and no money should be spent — stop the app and record INCONCLUSIVE with the exact console message. If the model dropdown does not offer Haiku, do NOT substitute another model: stop and record INCONCLUSIVE, because a substitution changes the cost.

> [!NOTE]
> COST CONTROL IS PART OF THIS TEST: one prompt, one short turn, Model = Haiku, Effort = low, then stop the app. Do not re-run it to re-check Markdown cases — every Markdown, safety, ordering, autoscroll and Budget behaviour in this area is fully provable for free with the demo echo agent in ROOMMESSAGING-06 through -20. This test exists only to prove the REAL agent travels the same Draft-and-render path. The Budget is pinned to 4 rather than cleared. Clearing the override restores the default of 40, which is 40 Turns of exposure on a test that needs one.

---

Back to [the manual test script](../manual-tests.md).
