# Settings: the 22 Hooks, editing, per-field reset and Save

Prove that the /settings Hooks editor renders all 22 model-facing hooks correctly, that its three-state badge logic (Next session / Modified / Unsaved) and per-field Reset behave, that Save is the one and only thing that ever writes {DataDir}\hooks.json, that the validator reports without ever refusing, and — most importantly — that this area's documented SILENT behaviours (a malformed hand-edit that says nothing on screen, a NextSession edit that is inert on a running teammate, a "Reset all" that stages but never commits, a destructive Reset-all button shown on the Appearance tab) actually behave as documented rather than as data loss. None of this is reachable by CI: the repo's component tests cannot dispatch a click, so every interaction here is unproven until a human or agent drives a browser.

**40 tests** · 37 free, 3 paid 💰 · about 4.7 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-FREE`](common.md#p-launch-free) from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB`, the four resets, and the
standing conventions. This area adds:

1. Confirm the clean starting state before the first test: `App_Data` should hold `Teams`, `personas`, `work` and `team.db` (plus `team.db-shm` / `team.db-wal`) and NO `hooks.json` and NO `appearance.json`. That absence is correct, not a fault.
2. Browse to `http://localhost:5100/settings`.
3. Keep a File Explorer window on `App_Data` beside the browser with the view set to Details, so the Date modified column is visible. Several tests turn on 'the file did not change'.
4. Keep a plain-text editor that does not reformat files (VS Code, Notepad++, Notepad) ready — many tests hand-edit `App_Data\hooks.json` while the app is running.
5. Open `E:\Repos\Huddle\src\Huddle.App\hooks.default.json` in that editor and leave it open. It is the 22 shipped defaults, pretty-printed, and is the diff source for every 'did Reset restore the exact shipped wording?' check. Nothing in the running app ever reads it.
6. Reset between tests with `P-RESET-SETTINGS` unless a test says otherwise. Tests HOOKSSETTINGS-01 to -36 run on `P-LAUNCH-FREE` and cost nothing; only -37 to -40 use `P-LAUNCH-PAID`.
7. MudBlazor renders every button and tab-panel LABEL in upper case via CSS (`text-transform: uppercase`) — this is a rendering style, not a change to the underlying text. A step below that says to look for `Reset` or `Hooks` means the control whose text (and `textContent` in DevTools) is `Reset` or `Hooks`; what you will actually SEE on screen is `RESET` / `HOOKS`. Steps keep the title-case spelling throughout this document because that is what a DOM/`textContent` check reads and what this document's own prose uses — read every button and tab label below as its upper-case rendering, not as a mismatch to report.

## Tests

### HOOKSSETTINGS-01 — /settings opens on the Hooks tab with a two-button tab rail

**Free** · about 2 min

*Proves the page routes, renders, and defaults to Hooks when no tab segment is given.*

**Before you start**

- The app is running.
- No hooks.json in App_Data.

**Steps**

1. Browse to `http://localhost:5100/`.
2. Click **Settings** in the left sidebar.
3. Read the page heading.
4. Look at the top of the content area, ABOVE the form, for the tab rail — `MudTabs` renders horizontally across the top by default; it is no longer a vertical rail down the left.
5. Count the buttons in the tab rail.
6. Note which button looks selected (a coloured underline/indicator beneath it, and heavier text).
7. Look at the browser address bar.

**Pass if — all of these**

- An `<h1>` reading exactly `Settings` is at the top of the page.
- The tab rail contains exactly two buttons, reading `HOOKS` and `APPEARANCE` (MudBlazor renders tab labels in upper case; the underlying text is `Hooks` and `Appearance`), laid out horizontally ABOVE the content, not down its left edge.
- `HOOKS` is the visually selected tab and `APPEARANCE` is not.
- The content pane below shows hook fields (bold field labels with textareas), not a theme picker.
- The address bar still reads `http://localhost:5100/settings` with no tab segment appended.

**Fail if — any of these**

- A 404 page or a yellow/ASP.NET exception page -> the route `/settings` with no `{Tab}` segment no longer resolves.
- A blank content pane -> the `default:` fallback arm of the tab switch has broken; landing with no tab must still render Hooks.
- Neither tab looks active, or both do -> `MudTabs`' `ActivePanelIndex` is being computed from a stale or duplicated comparison.
- The rail renders as a vertical column down the left, or anywhere other than horizontally above the content -> `MudTabs.Position` was set away from its default (`Position.Top`), which nothing in this area asked for.

**Inconclusive if**

If the browser shows a connection error, the app is not running — redo `P-LAUNCH-FREE` and check `T-A` for a startup exception, then re-run. If the page renders but the styling is obviously absent (no colours at all, unstyled text), CSS failed to load: hard-refresh with Ctrl+F5 and re-judge; if it is still unstyled, stop and report a CSS-loading problem rather than judging the tab rail.

> [!NOTE]
> The tab rail is `MudTabs` (Stage 3 of the MudBlazor migration), rendered at its default `Position.Top` — horizontally, above the content — which is a layout change from the old hand-rolled vertical left rail. That change is expected; do not file it. To confirm the selected-tab styling objectively rather than by eye, open devtools (F12), inspect the `HOOKS` button, and check its class list contains `mud-tab-active`. The old hand-rolled `settings-tab-active` class no longer exists.

### HOOKSSETTINGS-02 — Tab clicks change the URL, and an unknown tab segment falls back to Hooks instead of 404ing

**Free** · about 4 min

*Proves the tab is addressable (bookmarkable) and that an unrecognised segment can never break the page, because the router has no NotFound branch.*

**Before you start**

- The app is running.
- You are on /settings.

**Steps**

1. Click the **Appearance** tab button.
2. Read the address bar and the content pane.
3. Click the **Hooks** tab button.
4. Read the address bar and the content pane.
5. Type `http://localhost:5100/settings/hooks` in the address bar and press Enter.
6. Type `http://localhost:5100/settings/HOOKS` (all capitals) in the address bar and press Enter.
7. Type `http://localhost:5100/settings/Appearance` in the address bar and press Enter.
8. Type `http://localhost:5100/settings/nonsense` in the address bar and press Enter.
9. Type `http://localhost:5100/settings/` (with the trailing slash, nothing after it) in the address bar and press Enter.
10. Press the browser Back button three times, then the Forward button three times.

**Pass if — all of these**

- After step 1 the URL is `http://localhost:5100/settings/appearance` and the pane shows the sentence `Pick a theme, and choose whether it always uses its light or dark palette, or follows your device's own setting.` and a `Theme` select plus a second, `Appearance`-labelled select.
- After step 3 the URL is `http://localhost:5100/settings/hooks` and the 22 hook textareas are back.
- Step 5 renders the Hooks pane.
- Step 6 (`/settings/HOOKS`) renders the Hooks pane — the parse is case-insensitive.
- Step 7 (`/settings/Appearance`) renders the Appearance pane.
- Step 8 (`/settings/nonsense`) renders the HOOKS pane, with `nonsense` still visible in the address bar.
- Step 9 renders a page (Hooks pane) rather than an error.
- Back and Forward move between the tabs you visited without error.

**Fail if — any of these**

- Step 8 producing a 404 or an unhandled-exception page -> the unknown-tab fallback is gone; since the router has no NotFound branch, any future renamed or removed tab would hard-fail a user's bookmark.
- Step 6 rendering Appearance or nothing -> the enum parse lost its `ignoreCase: true`.
- A tab visually changing while the URL stays `/settings` -> the tab is flipping local state instead of navigating, so a bookmark or a shared link can no longer name a tab.

**Inconclusive if**

If step 9 (`/settings/` with a trailing slash) gives a 404 while every other step passes, do NOT fail the test on that alone — trailing-slash handling is web-server routing rather than this page's fallback. Record it as a separate minor observation and mark the rest pass/fail on steps 1-8 and 10.

### HOOKSSETTINGS-03 — The Hooks tab prints the real absolute path of hooks.json, and says the file's absence is expected

**Free** · about 3 min

*Proves the one mitigation the repo ships for a documented user confusion: someone who goes looking for the overrides file before saving finds nothing and reads that as broken.*

**Before you start**

- The app is running.
- You are on /settings (Hooks tab).

**Steps**

1. Read the first paragraph above the form.
2. Read the second, smaller, muted paragraph below it.
3. Select the path inside the `<code>` element in that second paragraph and copy it.
4. Paste the copied path into the File Explorer address bar and press Enter.
5. Look at what Explorer opens, and at what files sit beside it in that folder.

**Pass if — all of these**

- The first paragraph reads: `A hook is one piece of wording this application sends to a model — part of a system prompt, a turn, get_help's output, or a tool's own description. Editing one changes what every teammate is told, not how the application itself behaves, and a hook you have changed can always be restored to the wording it shipped with.`
- The second paragraph reads: `Overrides are stored at <path>, which holds only the hooks you have changed — hand-edit it for fast prototyping if you like. The file does not exist until the first time you save here, so it being absent is expected, not a bug.`
- The `<path>` is a full absolute Windows path ending in `\App_Data\hooks.json` — for a default checkout, `E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json`.
- Explorer opens (or reports 'file not found' for) that exact folder, and that folder is the one containing `team.db`.

**Fail if — any of these**

- A relative path such as `App_Data/hooks.json`, a placeholder, or an empty `<code>` element -> the on-screen path is no longer coming from HookStore.FilePath, and the documented confusion it exists to prevent is back.
- A path under a different folder than the one holding `team.db` -> DataDir absolutisation has drifted, and the file the tester hand-edits in later tests will not be the file the app reads.
- The sentence about the file not existing being missing -> the mitigation the rules file makes binding has been deleted.

**Inconclusive if**

If Explorer says the FILE does not exist but opens the right FOLDER, that is a PASS on this test, not inconclusive — the file is supposed to be absent until the first Save. Only treat it as inconclusive if you have configured a non-default `Team:DataDir`, in which case reset to the default before judging.

> [!NOTE]
> Use the path the page prints in every later test in this suite. Do not use the path written in these notes if the two disagree — the page is the authority.

### HOOKSSETTINGS-04 — Exactly 22 hook fields, in four named groups, in a fixed order

**Free** · about 5 min

*Proves the whole catalog is reachable from the UI — a hook whose key matched no group prefix would simply vanish from the page with no error anywhere.*

**Before you start**

- The app is running.
- No hooks.json (fresh state).
- You are on /settings (Hooks tab).

**Steps**

1. Scroll to the top of the form and find the first group heading.
2. Write down every group heading, top to bottom.
3. Under each heading, write down every bold field label, top to bottom.
4. Count the total number of field labels.
5. Confirm each field has a textarea and a small grey sentence of helper text beneath it.

**Pass if — all of these**

- There are exactly four group headings, in this order: `System prompt`, `Turn`, `Get help`, `Tool descriptions`.
- `System prompt` holds exactly 4 fields in this order: `Orientation`, `Identity`, `Chat rules`, `Tools`.
- `Turn` holds exactly 4 fields in this order: `Room label`, `Message`, `Catch-up header`, `Catch-up line`.
- `Get help` holds exactly 9 fields in this order: `Help: introduction`, `Help: Rooms`, `Help: messages`, `Help: mentions`, `Help: replying`, `Help: budget`, `Help: tools heading`, `Help: tool entry`, `Help: footer`.
- `Tool descriptions` holds exactly 5 fields in this order: `get_help description`, `list_agents description`, `create_room description`, `invite_agent description`, `post_message description`.
- The total is exactly 22.
- Every one of the 22 has a bold label, an editable textarea, and one muted helper sentence.

**Fail if — any of these**

- A total other than 22 -> a hook key exists that matches none of the four group prefixes, so it renders nowhere and can never be edited or reset from the UI; there is NO error for this, only a missing field.
- A field under the wrong heading, or two fields swapped -> the grouping/ordering logic has changed and a user following documentation will look in the wrong place.
- A missing group heading with its fields still present -> the group builder is dropping empty-label groups incorrectly.

**Inconclusive if**

If you cannot tell where one group ends and the next begins because headings are unstyled, open devtools and count the `MudPaper` elements wrapping each group (each renders as a `.mud-paper` carrying the `pa-4 mb-4` classes and one `h2.hooks-group-heading`) and the `hooks-field` divs inside each. If that count matches, pass the test and separately report the styling problem. (The old hand-rolled `<section class="hooks-group">` wrapper no longer exists — Stage 3 of the MudBlazor migration replaced it with `MudPaper`, but `hooks-group-heading` and `hooks-field` are unchanged.)

> [!NOTE]
> Cross-check against `E:\Repos\Huddle\src\Huddle.App\hooks.default.json`, which holds exactly 22 keys. Every label on screen must correspond to one of them. Count, do not skim — this test's entire value is the count.

### HOOKSSETTINGS-05 — The "Next session" badge appears on exactly the 9 hooks whose edits cannot reach a running teammate

**Free** · about 5 min

*Proves the one timing fact a reader cannot infer from a hook's own text is shown — without it, a user edits the system prompt, sees nothing change, and has no way to learn why.*

**Before you start**

- The app is running.
- You are on /settings (Hooks tab).

**Steps**

1. Walk every one of the 22 field headers from top to bottom.
2. For each, note whether a small grey pill reading `Next session` sits beside the field label.
3. List the fields that carry it.
4. Hover the mouse over one `Next session` pill and wait for the tooltip.
5. Confirm that no field anywhere carries a pill reading `Live`.

**Pass if — all of these**

- Exactly nine fields carry a `Next session` pill.
- Those nine are: `Orientation`, `Identity`, `Chat rules`, `Tools` (all four under System prompt) and `get_help description`, `list_agents description`, `create_room description`, `invite_agent description`, `post_message description` (all five under Tool descriptions).
- The thirteen fields under `Turn` and `Get help` carry NO timing pill at all.
- Hovering a `Next session` pill shows the tooltip `Applies to teammates started after the change`.
- No field shows a `Live` pill — there is deliberately no such badge.

**Fail if — any of these**

- A missing badge on any of the nine -> the only warning a user ever gets that their system-prompt edit will not reach a running teammate is gone; the failure that follows is completely silent, at runtime and in the logs.
- A badge on any of the thirteen Turn/Get help fields -> a user is told a restart is needed when it is not, and will pointlessly destroy a teammate's conversation memory to apply an edit that was already live.
- Hovering shows no tooltip -> the `title` attribute has been dropped, which is a silent loss of the explanation.

**Inconclusive if**

If tooltips do not appear at all in your browser (some kiosk/remote setups suppress them), inspect the pill in devtools and read its `title` attribute directly. If the attribute is present with the correct text, pass the tooltip step.

> [!NOTE]
> The authoritative count is 9 — the number of `Timing: HookTiming.NextSession` entries in `src\Huddle.App\Hooks\HookCatalog.cs`.

### HOOKSSETTINGS-06 — Placeholder chips are listed on exactly the 8 hooks that take placeholders, with full braces

**Free** · about 5 min

*Proves a user can see which {{tokens}} a hook accepts, spelled the way the renderer actually matches them.*

**Before you start**

- The app is running.
- You are on /settings (Hooks tab).

**Steps**

1. Walk every one of the 22 fields from top to bottom.
2. For each, look below the textarea for a line beginning `Placeholders:` followed by monospace chips.
3. Write down which fields have that line and exactly which chips each shows.

**Pass if — all of these**

- Exactly eight fields show a `Placeholders:` line.
- `Orientation` shows `{{helpTool}}`.
- `Identity` shows `{{personaName}}`.
- `Tools` shows `{{toolNames}}`.
- `Room label` shows `{{roomName}}` and `{{roomId}}`, in that order.
- `Message` shows `{{roomLabel}}`, `{{sender}}`, `{{text}}`, in that order.
- `Catch-up header` shows `{{roomLabel}}`.
- `Catch-up line` shows `{{sender}}` and `{{text}}`, in that order.
- `Help: tool entry` shows `{{toolName}}` and `{{toolDescription}}`, in that order.
- The other 14 fields show no `Placeholders:` line at all.
- Every chip includes both pairs of braces.

**Fail if — any of these**

- A chip showing a bare name such as `helpTool` instead of `{{helpTool}}` -> the braces are part of the key everywhere in this system; a user who copies the bare name into their text produces something the renderer will never substitute, and nothing warns them.
- A `Placeholders:` label rendered with no chips after it -> the zero-length guard around the list has been lost, which also means a no-placeholder hook now looks like it takes some.
- A field gaining or losing a chip relative to the list above -> the catalog and the renderer may now disagree about what a hook accepts.

**Inconclusive if**

If chips render but are visually indistinguishable from the surrounding text, read them from devtools (`<code class="hooks-field-placeholder">`). Judge content, not styling; report the styling separately.

### HOOKSSETTINGS-07 — No hook's DEFAULT text contains the literal string mcp__team__

**Free** · about 4 min

*Proves the binding rule that a hook's text never hard-codes a tool prefix — code fills the real name in from the live tool roster. A violation is invisible on screen and only shows up later as a model insisting no such tool exists.*

**Before you start**

- No hooks.json (every field must show its shipped default).
- The app is running and you are on /settings (Hooks tab).

**Steps**

1. Scroll through all 22 textareas and read their contents, scrolling inside any box that has its own scrollbar.
2. Specifically read `Orientation`, `Tools`, and `Help: tool entry` in full.
3. Confirm those three show the placeholders `{{helpTool}}`, `{{toolNames}}` and `{{toolName}}` respectively, rather than a literal tool name.
4. Now run this in a terminal at the repo root and read the number it prints: `grep -c mcp__team__ src/Huddle.App/hooks.default.json`
5. Separately, click into the `Orientation` textarea, type `mcp__team__get_help` at the end of the text, and watch the field for any warning or refusal.

**Pass if — all of these**

- No textarea's CONTENT contains the string `mcp__team__`.
- `Orientation` contains `{{helpTool}}`; `Tools` contains `{{toolNames}}`; `Help: tool entry` contains `{{toolName}}`.
- The grep in step 4 prints `0`.
- Step 5 types freely: the app does NOT block, warn about, or refuse the typed `mcp__team__` string. (This is correct — it is deliberately unvalidated.)

**Fail if — any of these**

- Any textarea's default content containing `mcp__team__` -> a binding-rule violation. It looks fine on screen; the damage appears much later as a model reporting the tool does not exist, with nothing in any log explaining why.
- The grep printing anything other than 0 -> same defect, caught at the reference file.
- Step 5 being blocked or warned about -> a new validation rule was added that the rules file does not call for; report it, but as a separate finding from the above.

**Inconclusive if**

Browser Ctrl+F does NOT reliably search inside textarea values, so a 'no hits' result from Ctrl+F proves nothing — read the boxes, or use the grep. Also expect ONE legitimate on-page hit if you do use Ctrl+F: the grey helper sentence under `Help: tool entry` reads `{{toolName}} arrives already prefixed (e.g. mcp__team__get_help)`. That is helper text explaining the rule, not hook text, and it is NOT a failure.

> [!NOTE]
> Undo your step-5 typing (Ctrl+Z) or reset the field before moving on, and do not save it.

### HOOKSSETTINGS-08 — Textarea height tracks the line count and clamps at 14 rows

**Free** · about 5 min

*Proves the per-keystroke re-render is alive and that no one field can take over the page.*

**Before you start**

- The app is running, on /settings (Hooks tab).
- No hooks.json.

**Steps**

1. Find the `Room label` field, whose shipped default is a single line.
2. Note how tall its box is compared to a single line of text.
3. Click at the end of its text and press Enter once. Observe the box height.
4. Press Enter twelve more times (thirteen newlines in total), watching the height after each press.
5. Press Enter five more times, watching the height.
6. Open devtools (F12), inspect the `Room label` field's `<textarea>` element, and read its `rows` attribute.
7. Look at the bottom-right corner of the box for a manual resize grip, and try to drag it.
8. Press Ctrl+Z repeatedly, or reload the page with F5, to discard these edits without saving.

**Pass if — all of these**

- Before any typing, the `Room label` box is about two text rows tall — a box, not a one-line slot.
- Each of the first thirteen Enter presses makes the box one row taller, immediately, without leaving the field.
- After the thirteenth newline the box stops growing; the five further Enter presses add no height and the box scrolls internally instead.
- At that point the inspected `rows` attribute reads `14`.
- There is NO manual resize grip, and dragging the bottom-right corner does nothing — `MudTextField` sets `resize: none` on its `<textarea>` and grows it itself by recomputing `Lines`, so a manual drag handle would only fight that.

**Fail if — any of these**

- The box not growing at all as newlines are added -> the height is recomputed on every keystroke, so a frozen height means the per-input re-render has stopped, which would also break the live badges and live validation in the tests below.
- The box growing past 14 rows and dominating the page -> the clamp is gone; the longest hooks would push Save far off screen.
- A manual resize grip appears and can drag the box independently of `Lines` -> `MudTextField`'s `resize: none` was overridden; a manual resize would drift out of sync with the automatic row count on the very next keystroke.

**Inconclusive if**

A very long SINGLE line with no newlines correctly stays at 2 rows and scrolls sideways — that is not a failure, so do not test the clamp by pasting one long line. NOTE: none of the 22 shipped defaults is long enough to hit the 14-row cap on its own (the tallest, `Tools`, is 7 lines), so the cap can only be reached by typing newlines as above. If you find a shipped default already rendering at 14 rows, that is a change worth reporting separately.

### HOOKSSETTINGS-09 — Typing raises "Modified" and "Unsaved" together and enables Save, Reset and Reset all — while writing nothing

**Free** · about 4 min

*Proves the edit flow is per-keystroke and that nothing touches disk before Save.*

**Before you start**

- No hooks.json.
- The app is running, on /settings (Hooks tab).
- File Explorer is open on App_Data.

**Steps**

1. Confirm in Explorer that `hooks.json` does not exist.
2. Scroll to the `Save` button at the bottom of the form and confirm it is greyed out.
3. Scroll to the header and confirm `Reset all to defaults` is greyed out.
4. Click into the `Room label` textarea and type a single character `X` at the end of the text. Do NOT press Enter, do NOT click elsewhere.
5. Without clicking anything, look at the `Room label` field header.
6. Look at the `Reset` button at the bottom-right of the `Room label` field.
7. Scroll down to the `Save` button, then up to `Reset all to defaults`.
8. Look at the field headers of `Message` and `Chat rules`.
9. Switch to Explorer and press F5 to refresh the folder listing.

**Pass if — all of these**

- Before typing: Save is disabled and `Reset all to defaults` is disabled.
- Immediately on the keystroke — with no blur, no Enter, no click — the `Room label` header gains two pills: an amber `Modified` and a blue `Unsaved`.
- That field's `Reset` button becomes enabled.
- The `Save` button becomes enabled.
- `Reset all to defaults` becomes enabled.
- `Message` and `Chat rules` gain no badges.
- `hooks.json` still does NOT exist in App_Data.

**Fail if — any of these**

- Badges appearing only after you click away -> the handler has moved from input to change, so every live behaviour in this area (validation, row growth, Save enablement) is now one interaction late.
- Save staying disabled while `Unsaved` shows -> the Save enablement and the badge have drifted apart, and a user with pending work cannot commit it.
- `Reset` staying disabled while `Modified` shows -> the same flag drives both, so a mismatch is a real defect.
- Badges leaking onto a neighbouring field -> the pending-edit map is keyed wrongly and a save will write the wrong key.
- `hooks.json` appearing -> a write happened without a Save; the file must not exist until the first Save.

**Inconclusive if**

If you cannot distinguish the amber `Modified` pill from the blue `Unsaved` pill by colour, read their text — both pills carry their word as literal text. Judge by the words.

> [!NOTE]
> Leave the `X` in place if you go straight on to HOOKSSETTINGS-10; otherwise press F5 to discard it.

### HOOKSSETTINGS-10 — "Modified" and "Unsaved" are independent flags — walk all three combinations

**Free** · about 10 min

*Proves the view-model's three values (shipped default, stored value, pending value) are compared pairwise and not conflated — the documented main way this state is misused.*

**Before you start**

- No hooks.json at the start.
- The app is running, on /settings (Hooks tab).
- `src\Huddle.App\hooks.default.json` is open in a text editor.

**Steps**

1. Find the `Chat rules` field. Click at the very end of its text and type ` ZZZ`.
2. Click `Save`.
3. Read the `Chat rules` header badges.
4. Open `App_Data\hooks.json` in the editor and count its top-level keys.
5. Back in the browser, type ` QQQ` at the end of `Chat rules`.
6. Read the `Chat rules` header badges.
7. In `hooks.default.json`, find the `systemPrompt.chatRules` value. Select the `Chat rules` textarea contents (click in it, Ctrl+A) and retype/paste the shipped wording back exactly, character for character, with no trailing space.
8. Read the `Chat rules` header badges, and the enabled state of that field's `Reset` button and of the page `Save` button.
9. Now edit `Chat rules` back to exactly the text you saved in step 2 (the shipped wording plus ` ZZZ`).
10. Read the `Chat rules` header badges and the enabled state of `Save`.

**Pass if — all of these**

- After step 2 (saved edit): `Chat rules` shows `Modified` ONLY — no `Unsaved`.
- After step 4: `hooks.json` holds exactly one top-level key, `systemPrompt.chatRules`.
- After step 5: `Chat rules` shows `Modified` AND `Unsaved`.
- After step 7 (text back to the shipped default): `Modified` DISAPPEARS, `Unsaved` REMAINS, that field's `Reset` button goes DISABLED, and `Save` stays ENABLED.
- After step 9 (text back to the stored override): `Unsaved` DISAPPEARS, `Modified` RETURNS, and `Save` goes DISABLED again (assuming nothing else is pending).

**Fail if — any of these**

- The two badges always appearing and disappearing together -> they have been collapsed into one flag; a user can no longer tell 'differs from shipped' from 'not yet committed', which is exactly the confusion the two-badge design exists to prevent.
- `Save` staying enabled in step 10 when the pending text has returned to the stored text -> the app would write a no-op save and, worse, the enabled button implies unsaved work that does not exist.
- `Reset` staying enabled in step 8 when the pending text already equals the default -> Reset would be a no-op button that still looks actionable.

**Inconclusive if**

Step 7 depends on retyping the default byte-for-byte. If `Modified` refuses to clear, suspect a trailing space, a smart-quote substitution by your editor, or CRLF vs LF line endings rather than a defect — click that field's `Reset` button instead (which stages the exact default) and re-read the badges. If `Modified` clears after Reset but not after your paste, this test is INCONCLUSIVE on step 7 and you have instead reproduced HOOKSSETTINGS-35; note it there.

> [!NOTE]
> Run `P-RESET-SETTINGS` and relaunch before the next test.

### HOOKSSETTINGS-11 — Per-field "Reset" stages the shipped default without writing anything to disk

**Free** · about 6 min

*Proves Reset is an edit, not a commit — a mis-click must be recoverable and must not surprise-write the file.*

**Before you start**

- The app is running, on /settings (Hooks tab).
- File Explorer open on App_Data in Details view.

**Steps**

1. Type ` HELLO` at the end of the `Room label` textarea and click `Save`.
2. In Explorer, note `hooks.json`'s exact Date modified value (right-click > Properties if the column is too coarse).
3. Back in the browser, click the `Reset` button at the bottom-right of the `Room label` field.
4. Read the `Room label` textarea contents.
5. Read the `Room label` header badges and the enabled state of its `Reset` button and of `Save`.
6. Switch to Explorer, press F5, and re-read `hooks.json`'s Date modified and open the file.
7. Look at the header badges of every OTHER field you had not touched.
8. Back in the browser, click `Save`.
9. Reopen `hooks.json`.

**Pass if — all of these**

- After step 3 the textarea text snaps back to `[Room: {{roomName}} (id: {{roomId}})]` — byte-for-byte the `turn.roomLabel` value in `hooks.default.json`.
- After step 3 the `Modified` badge disappears, an `Unsaved` badge appears, `Save` is enabled, and that field's `Reset` button greys out.
- At step 6 `hooks.json` is UNCHANGED: same Date modified, and it still contains the `turn.roomLabel` key with the ` HELLO` text.
- No other field's badges or text changed.
- Only after step 8 does the key disappear: at step 9 `hooks.json` no longer contains `turn.roomLabel`.

**Fail if — any of these**

- `hooks.json`'s timestamp or content changing at step 6 -> Reset is writing immediately, which bypasses the single commit point and means a mis-click permanently destroys a customisation with no undo.
- The restored text differing in any character from the `turn.roomLabel` value in `hooks.default.json` -> Reset is restoring something other than the shipped wording, so 'restore to what it shipped with' is a lie.
- Other fields' pending edits being cleared by the click -> Reset is resetting more than its own key.

**Inconclusive if**

If the Date modified column shows only minutes and both operations happen inside the same minute, do not guess — open the file and compare CONTENT instead. Content is the authoritative oracle here.

> [!NOTE]
> Run `P-RESET-SETTINGS` and relaunch afterwards.

### HOOKSSETTINGS-12 — hooks.json does not exist until the first Save, and then holds ONLY the keys you changed

**Free** · about 6 min

*Proves the binding rule that an absent overrides file is normal and that the app never writes a file of 22 defaults it would then have to keep in step with the code forever.*

**Before you start**

- No hooks.json.
- The app was restarted after the deletion.
- File Explorer open on App_Data.

**Steps**

1. Confirm in Explorer that `hooks.json` is absent.
2. Load `http://localhost:5100/settings`, then press F5 twice. Refresh Explorer and check again.
3. Type ` A` into `Room label`, ` B` into `Help: budget`, and ` C` into `Identity`. Refresh Explorer and check again.
4. Click the `Reset` button on the `Identity` field. Refresh Explorer and check again.
5. Click `Save`.
6. Refresh Explorer.
7. Open `hooks.json` in a text editor and list its top-level keys.

**Pass if — all of these**

- `hooks.json` is absent at steps 1, 2, 3 and 4 — loading the page, reloading it, typing, and clicking Reset all leave it absent.
- `hooks.json` appears only after step 5.
- Its content is pretty-printed (indented, one key per line block) JSON.
- It contains exactly two top-level keys: `turn.roomLabel` and `getHelp.budget`.
- It does NOT contain `systemPrompt.identity` (you reset that one before saving) and does not contain any of the other 19 keys.

**Fail if — any of these**

- The file existing at step 1 or appearing at step 2 -> the app is creating an overrides file at startup or on a page load; the rules file makes this binding, and a file of defaults would silently pin today's wording forever, so a future change to a shipped default would never reach that installation.
- The file appearing at step 3 or 4 -> a write happened without a Save, so Save is no longer the single commit point.
- The file containing all 22 keys after two edits -> the same defect in a different form: every untouched hook has just been frozen at today's text.
- `systemPrompt.identity` being present -> a key whose pending value equals the default is being stored as a redundant copy.

**Inconclusive if**

If the file was already present at step 1, you did not reset state — run `P-RESET-SETTINGS`, relaunch and begin the test again. Do not judge from a dirty starting state.

> [!NOTE]
> This is the highest-value cheap test in the area: it pins the documented 'absent is correct' behaviour that testers most often misreport as a bug.

### HOOKSSETTINGS-13 — The saved file is human-readable: indented, with literal em-dashes and angle brackets, not \uXXXX escapes

**Free** · about 6 min

*Proves the file stays hand-editable — the documented purpose of this file — and that the app does not rewrite a user's literal `<name>` into `<name>` on the next save, which reads as corruption.*

**Before you start**

- The app is running, on /settings (Hooks tab).
- No hooks.json to start.

**Steps**

1. Find the `Help: messages` field. Its default contains the literal text `"[Room: <name> (id: <id>)]"`. Type ` EDITED` at the end of its text.
2. Find the `Tools` field. Its default contains an em-dash in `Your reply to the current message is just your answer text — do not also post it with a tool.` Type ` EDITED` at the end of its text.
3. Click `Save`.
4. Open `App_Data\hooks.json` in a plain-text editor.
5. Search the file for `<name>`.
6. Search the file for the em-dash character `—`.
7. Search the file for the sequence `<`.
8. Search the file for the sequence `—`.
9. Look at the overall shape of the file.

**Pass if — all of these**

- The file is indented — nested lines are offset and it is not one single long line.
- `<name>` and `<id>` appear literally, with real angle brackets.
- The em-dash `—` appears literally.
- No `<`, `>` or `—` sequences appear anywhere.
- The file opens and reads cleanly in a plain text editor without horizontal scrolling to find the keys.

**Fail if — any of these**

- Angle brackets written as `<` / `>`, or em-dashes as `—` -> the HTML-safe JSON encoder has crept back in; a user who typed a literal `<name>` will see the app rewrite it on the very next save, which reads as corruption rather than as a JSON encoding detail.
- Compact single-line JSON -> the file is explicitly meant to be hand-edited and no longer can be comfortably.

**Inconclusive if**

Straight double quotes inside a hook's text WILL legitimately appear as `\"` in the file — that is required JSON escaping, not the defect this test looks for. Do not fail on `\"`. If your editor renders the em-dash as a box or question mark, that is an editor encoding setting: reopen the file as UTF-8 before judging.

> [!NOTE]
> Reset to a clean state afterwards.

### HOOKSSETTINGS-14 — Saving a field back to its default removes the key rather than storing a redundant copy

**Free** · about 5 min

*Proves an unmodified hook does not silently pin today's wording — otherwise a future change to a shipped default would never reach that installation.*

**Before you start**

- No hooks.json.
- The app is running, on /settings (Hooks tab).

**Steps**

1. Type ` ONE` at the end of `Room label` and ` TWO` at the end of `Help: footer`.
2. Click `Save`.
3. Open `App_Data\hooks.json` and confirm it holds exactly two keys.
4. Back in the browser, click the `Reset` button on the `Room label` field.
5. Click `Save`.
6. Reopen `App_Data\hooks.json` and list its keys.

**Pass if — all of these**

- After step 3 the file holds exactly two keys: `turn.roomLabel` and `getHelp.footer`.
- After step 6 the file holds exactly ONE key: `getHelp.footer`.
- `turn.roomLabel` is absent from the file — not present with the default text as its value.
- On screen, `Room label` shows no badges at all after step 5.

**Fail if — any of these**

- The file still containing `turn.roomLabel` with the default text as its value -> not cosmetic: the field looks unmodified on screen while the file quietly pins a copy of today's wording, so when the shipped default changes in a future release this installation silently keeps the old text and nothing reports it.
- The file losing `getHelp.footer` too -> the save is overwriting rather than merging, and unrelated overrides are being destroyed.

**Inconclusive if**

If you reset the wrong field, start over from a clean state rather than reasoning about which key should remain.

> [!NOTE]
> Reset to a clean state afterwards.

### HOOKSSETTINGS-15 — Save is the ONLY writer — and a successful Save gives no confirmation

**Free** · about 8 min

*Proves the single-commit-point design, and records the (correct) absence of any success feedback so a tester does not report it as a broken save.*

**Before you start**

- The app is running, on /settings.
- File Explorer open on App_Data.

**Steps**

1. Type ` SEED` at the end of `Room label` and click `Save`, so `hooks.json` exists. Note its Date modified and copy its content into a scratch file for comparison.
2. Press F5 to reload /settings. Refresh Explorer and compare.
3. Type text into three different fields. Refresh Explorer and compare.
4. Click the `Reset` button on one of those fields. Refresh Explorer and compare.
5. Click `Reset all to defaults` in the header, then click `Yes, reset everything`. Refresh Explorer and compare.
6. Click the `Appearance` tab, then the `Hooks` tab. Refresh Explorer and compare.
7. Now click `Save` once. Watch the page closely for the next three seconds.
8. Refresh Explorer and compare.

**Pass if — all of these**

- `hooks.json` is byte-identical and its timestamp unchanged after steps 2, 3, 4, 5 and 6.
- The file changes only after step 7.
- On a successful Save the page does NOT reload and does NOT navigate — the URL stays `/settings` (or `/settings/hooks`) and the scroll position is preserved.
- The only feedback from the successful Save is that the `Unsaved` badges clear and the `Save` button goes disabled. There is NO toast, NO green banner and NO `Saved` text — this is correct.

**Fail if — any of these**

- The file changing on any of steps 2-6 -> some path other than Save writes to disk; the whole staged-edit model then leaks, and a mis-click or an idle page could permanently change the file.
- The badges clearing while the file did NOT change at step 8 -> the badges are lying about a write that silently failed; the user believes their work is saved and it is not.
- Save reloading the page or issuing a full-page POST -> a regression that would also discard every other field's pending edits on every save.

**Inconclusive if**

If the timestamp granularity is too coarse to distinguish, use content comparison (a diff against the scratch copy from step 1) — content is authoritative. If a toast library or a banner has been added and the save otherwise works correctly, that is not a failure of this behaviour: record it as a deliberate UI change to confirm with the owner.

> [!NOTE]
> The absence of a success confirmation is documented designed behaviour. Do not file it as a defect; if you think it should exist, file it as a UX suggestion.

### HOOKSSETTINGS-16 — "Reset all to defaults" is disabled until something is modified and guards itself with an inline confirm

**Free** · about 4 min

*Proves the single most destructive control on the page cannot fire on one click, and uses the repo's inline-confirm pattern rather than a browser dialog.*

**Before you start**

- No hooks.json.
- The app is running, on /settings (Hooks tab).

**Steps**

1. Look at the header, to the right of the `Settings` heading. Find the button reading `Reset all to defaults`.
2. Try to click it.
3. Type a single character into any hook field.
4. Look at the `Reset all to defaults` button again.
5. Click `Reset all to defaults` once.
6. Look at where the button was.
7. Click `Cancel`.
8. Look at the header and at the field you typed into.
9. Click `Reset all to defaults` again.

**Pass if — all of these**

- Before any edit the `Reset all to defaults` button is visibly DISABLED and clicking it does nothing.
- It is styled as a danger action (distinct from the plain buttons).
- After one keystroke in any field it becomes ENABLED.
- Clicking it replaces it IN PLACE with two buttons: `Yes, reset everything` (danger styling) and `Cancel`.
- No browser `confirm()` dialog and no modal overlay appears at any point.
- Clicking `Cancel` restores the single `Reset all to defaults` button and changes nothing — your typed character and its badges are still there.
- Clicking it again re-shows the confirm pair.

**Fail if — any of these**

- A JavaScript `confirm()` dialog or a modal -> the repo deliberately uses the same inline-confirm pattern as Remove on /teammates; a dialog is a different interaction model and is blocked by some browsers.
- The button being enabled with nothing modified -> a user can 'reset' from an already-default state, which stages 22 no-op edits and makes Save look actionable for nothing.
- The destructive action firing on the FIRST click with no confirm step -> one stray click discards every customisation on the page.

**Inconclusive if**

If you cannot tell whether the button is disabled, inspect it in devtools and check for the `disabled` attribute. Judge from the attribute, not the shade of grey.

### HOOKSSETTINGS-17 — "Yes, reset everything" stages all 22 defaults but writes nothing — and the reset is silently lost if you navigate away

**Free** · about 8 min

*Proves the confirm stages rather than commits, and documents the genuine trap: a tester who confirms, navigates away, and comes back finds every override intact.*

**Before you start**

- The app is running, on /settings (Hooks tab).
- File Explorer open on App_Data.

**Steps**

1. Type ` ONE` into `Room label`, ` TWO` into `Help: budget`, and ` THREE` into `Identity`. Click `Save`.
2. Confirm `hooks.json` holds exactly three keys; note its Date modified and copy its content to a scratch file.
3. Click `Reset all to defaults`, then click `Yes, reset everything`.
4. Scan all 22 field headers and note every badge you can see.
5. Look at the `Save` button and at the header's `Reset all to defaults` button.
6. Refresh Explorer, compare `hooks.json`'s timestamp and content with the scratch copy.
7. WITHOUT clicking Save, click `Teammates` in the left sidebar.
8. Click `Settings` in the left sidebar to come back.
9. Read the `Room label`, `Help: budget` and `Identity` textareas and their badges.

**Pass if — all of these**

- After step 3 every `Modified` badge across all 22 fields has disappeared.
- The three previously-overridden fields (`Room label`, `Help: budget`, `Identity`) each show an `Unsaved` badge.
- The other 19 fields show NO badge at all.
- `Save` is ENABLED and `Reset all to defaults` is now DISABLED (nothing is modified any more).
- The confirm pair has collapsed back to the single `Reset all to defaults` button.
- At step 6 `hooks.json` is unchanged — same timestamp, same three keys, same content.
- After step 8 all three overrides are BACK in their textareas with `Modified` badges, and the reset never happened — with no warning shown at any point.

**Fail if — any of these**

- `hooks.json` being rewritten at step 6 -> the confirm commits directly, so Save is no longer the single commit point and a confirm-then-think-again is unrecoverable.
- All 22 fields showing `Unsaved` at step 4 -> a field whose stored value already equals the default has nothing to commit, so a badge there means the save would write 22 no-op keys.
- The confirm pair not collapsing back to the single button -> the confirm state is not being cleared and a second reset could fire unexpectedly.

**Inconclusive if**

If step 9 shows the overrides gone rather than restored, check `hooks.json` first: if the file lost its keys, you have hit the 'confirm commits directly' failure above, which is a FAIL not an inconclusive. If the file still holds the keys but the page shows defaults, the store's resolution is broken — report that separately.

> [!NOTE]
> The silent-loss half is designed behaviour, not a defect — but it is a real trap. Report it as a UX finding ('a confirmed destructive action is silently discarded by navigating away, with no unsaved-changes prompt'), not as a bug.

### HOOKSSETTINGS-18 — "Reset all" followed by Save leaves hooks.json present and empty ({}), never deleted

**Free** · about 4 min

*Pins the exact end state so a tester does not misreport the surviving empty file as a failed reset.*

**Before you start**

- The app is running, on /settings (Hooks tab).

**Steps**

1. Type ` ONE` into `Room label` and ` TWO` into `Help: footer`. Click `Save`.
2. Confirm `hooks.json` holds two keys.
3. Click `Reset all to defaults`, then `Yes, reset everything`.
4. Click `Save`.
5. Read every field header for badges, and read the `Save` button's state.
6. Open `App_Data\hooks.json` and read its entire contents.

**Pass if — all of these**

- After step 4 no field carries any badge and `Save` is disabled.
- `hooks.json` still EXISTS on disk.
- Its entire content is exactly `{}` (possibly with surrounding whitespace or a newline).
- All 22 textareas show their shipped defaults.

**Fail if — any of these**

- The file still holding keys -> the reset did not reach disk and the user's 'back to factory' did nothing.
- The file holding all 22 keys with default text -> the reset wrote a snapshot of today's defaults, which silently pins this installation's wording forever.

**Inconclusive if**

The file NOT being deleted is correct — the app never deletes it, it only ever writes the current override set. If you expected deletion, that expectation is wrong; do not file it. If the file is genuinely gone, check whether something else (your own cleanup, an editor) removed it before failing the test.

### HOOKSSETTINGS-19 — A failed Save shows a red "Could not save:" line under the heading and keeps every pending edit

**Free** · about 7 min

*Proves a write failure degrades to an inline message rather than killing the Blazor circuit, and that no typed work is lost.*

**Before you start**

- The app is running, on /settings (Hooks tab).
- You can change file attributes in App_Data.

**Steps**

1. Type ` SEED` into `Room label` and click `Save`, so `hooks.json` exists. Copy its content to a scratch file.
2. In `T-B` run: `Set-ItemProperty -Path 'E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json' -Name IsReadOnly -Value $true` (substitute the path the Settings page prints if it differs).
3. Back in the browser, type ` WILLFAIL` into `Help: budget` and into `Identity`.
4. Click `Save`.
5. Read the area directly under the `Settings` heading.
6. Read the `Help: budget` and `Identity` textareas and their badges.
7. Look at the bottom of the browser window for the yellow Blazor error strip.
8. Compare `hooks.json`'s content with the scratch copy.
9. Run: `Set-ItemProperty -Path 'E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json' -Name IsReadOnly -Value $false`
10. Click `Save` again.
11. Read the area under the `Settings` heading, and the field badges.

**Pass if — all of these**

- After step 4 a red paragraph appears directly beneath the `Settings` heading, beginning `Could not save: ` and followed by an operating-system message (on Windows, typically `Access to the path '...\hooks.json' is denied.`).
- Your typed ` WILLFAIL` text is still in both textareas and both still carry `Unsaved` badges — nothing was lost.
- No yellow `An unhandled error has occurred.` strip appears at the bottom of the page.
- `hooks.json`'s content is unchanged from the scratch copy.
- After step 10 the red paragraph DISAPPEARS, the `Unsaved` badges clear, and `hooks.json` now contains the new overrides.

**Fail if — any of these**

- The yellow `An unhandled error has occurred.` strip appearing instead of the inline message -> the write exception escaped the handler and killed the circuit; the user loses the whole page and all pending edits over a read-only file.
- The badges clearing as though the save worked -> the failure is being swallowed and the user believes their work is on disk when it is not.
- The red paragraph never clearing after the successful retry -> stale error state, so a user cannot tell a current failure from a past one.

**Inconclusive if**

If the read-only attribute does not cause a failure on your machine (some environments let the process clear it, or the app runs elevated), try instead holding the file open exclusively from another process — for example, in a second PowerShell window run `$f=[IO.File]::Open('<path>',[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)` and close it afterwards with `$f.Close()`. If neither method produces a failed write, this test is INCONCLUSIVE — say so, and do not claim the error path works.

> [!NOTE]
> Always clear the read-only flag and release any handle before moving on, or later tests will fail for the wrong reason.

### HOOKSSETTINGS-20 — Validation reports and never refuses: an empty field raises an Error and still saves

**Free** · about 5 min

*Proves the binding rule that a validator finding is advisory — a blocking validator is a defect here, not a safety feature.*

**Before you start**

- No hooks.json.
- The app is running, on /settings (Hooks tab).

**Steps**

1. Click into the `Room label` textarea, press Ctrl+A, then press Delete so the box is completely empty.
2. Read the area directly under that field (below the helper sentence and the Placeholders line).
3. Look at the colour/background of that message.
4. Look at the `Save` button.
5. Click `Save`.
6. Look for any dialog, prompt or confirmation.
7. Open `App_Data\hooks.json` and read the value for `turn.roomLabel`.
8. Press F5 to reload /settings and look at the `Room label` field again.

**Pass if — all of these**

- A red issue line appears under the field reading exactly: `The text for 'turn.roomLabel' is empty or whitespace-only; an empty prompt block is rejected downstream.`
- That line is styled as an error (red/danger background), not as an amber warning.
- The `Save` button stays ENABLED.
- Clicking Save produces no dialog and no confirmation prompt — the save simply goes through.
- `hooks.json` contains `"turn.roomLabel": ""`.
- After the reload the field is still empty and still shows the same red error.

**Fail if — any of these**

- `Save` going disabled while the error shows, or the click being swallowed, or a modal asking you to confirm -> the validator is blocking. The rules file makes 'reports and never refuses' binding: an `Error` means 'this will probably not work', never 'this is rejected'. A blocking validator is one users learn to route around.
- The finding styled as a warning (amber) rather than an error (red) -> severity has been downgraded and the two severities can no longer be told apart.
- No finding at all -> the empty-text check is gone.

**Inconclusive if**

If you cannot judge red from amber, inspect the element in devtools: each issue is now a `MudAlert` (Stage 3 of the MudBlazor migration replaced the old `<li class="hooks-field-issue-error">` list), so look for `mud-alert-text-error` versus `mud-alert-text-warning` in its class list. Judge from the class.

> [!NOTE]
> Run `P-RESET-SETTINGS` and relaunch afterwards.

### HOOKSSETTINGS-21 — Removing a required placeholder raises an Error naming that placeholder — and an optional one does not

**Free** · about 8 min

*Catches one of the two findings the ADR calls 'the entire value' of the validator, because the breakage it prevents is completely invisible at runtime.*

**Before you start**

- No hooks.json.
- The app is running, on /settings (Hooks tab).

**Steps**

1. In `Room label`, change the text from `[Room: {{roomName}} (id: {{roomId}})]` to `[Room: {{roomName}}]` — delete only the ` (id: {{roomId}})` part.
2. Read the issue line under that field, without saving.
3. In `Tools`, delete the token `{{toolNames}}` from the text. Read the issue line under that field.
4. In `Message`, delete the token `{{text}}` from the text. Read the issue line under that field.
5. Look at the `Save` button.
6. Now, in `Catch-up header`, delete the token `{{roomLabel}}`. Read under that field.
7. In `Room label` (still edited), delete `{{roomName}}` as well so the text reads `[Room: ]`. Read the issue lines under that field.

**Pass if — all of these**

- Step 2 shows a red error reading exactly: `'turn.roomLabel' is missing the required placeholder {{roomId}}. How each incoming message names the Room it came from. Must keep {{roomId}}: it is the only way an agent learns a Room's id.`
- The finding appears immediately as you type, with no save needed.
- Step 3 shows a red error naming `{{toolNames}}` for `'systemPrompt.tools'`.
- Step 4 shows a red error naming `{{text}}` for `'turn.message'`.
- `Save` remains ENABLED throughout.
- Step 6 produces NO error — `{{roomLabel}}` is optional on `Catch-up header`.
- Step 7 still shows the `{{roomId}}` error and does NOT add a second error for `{{roomName}}`, which is optional on `Room label`.

**Fail if — any of these**

- No finding at step 2, 3 or 4 -> the most valuable check in this feature is gone. An agent that never learns a Room id silently loses post_message and invite_agent for every Room it did not create itself, with no error anywhere, in the app or in the logs.
- An error at step 6 or for `{{roomName}}` at step 7 -> optional placeholders are being treated as required, and users will be nagged into keeping text they legitimately want to remove.
- The error message not naming the specific missing token -> a user cannot tell which token to put back.

**Inconclusive if**

If the message text differs only in the trailing helper sentence (the part after the first full stop), that part is the hook's own helper text and may legitimately have been reworded. Judge on the first sentence — `'<key>' is missing the required placeholder <token>.` — and note the wording difference separately.

> [!NOTE]
> Discard these edits with F5 before the next test; do not save them.

### HOOKSSETTINGS-22 — An unrecognised {{token}} raises a Warning; a malformed one raises nothing at all

**Free** · about 8 min

*Proves the typo catch works and that the deliberate non-findings (empty, spaced, malformed spans) stay silent, because they are far more likely to be prompt text someone wrote on purpose.*

**Before you start**

- No hooks.json.
- The app is running, on /settings (Hooks tab).

**Steps**

1. In `Room label`, change `{{roomId}}` to `{{roomID}}` (capital D). Read the issue line under the field and note its colour.
2. Undo with Ctrl+Z until the text is back to `[Room: {{roomName}} (id: {{roomId}})]` and confirm the warning goes away.
3. Now type ` {{}}` at the end of the `Room label` text. Read under the field.
4. Replace that with ` {{ }}` (two braces, a space, two braces). Read under the field.
5. Replace that with ` {{room Id}}` (a space inside the token). Read under the field.
6. Delete all of that so `Room label` is back to its default.
7. In `Help: introduction` (a hook that declares no placeholders), type ` {{anything}}` at the end. Read under the field.
8. Press F5 to discard everything.

**Pass if — all of these**

- Step 1 shows an AMBER warning reading exactly: `'turn.roomLabel' contains the token {{roomID}}, which is not one of this hook's declared placeholders ({{roomName}}, {{roomId}}) — most likely a typo.`
- Step 1's finding is styled as a warning (amber), NOT as an error (red).
- Steps 3, 4 and 5 produce NO finding of any kind — `{{}}`, `{{ }}` and `{{room Id}}` are not placeholders by design.
- Step 7 produces an amber warning for `'getHelp.intro'` naming `{{anything}}`, with an EMPTY parenthesised list: `...declared placeholders () — most likely a typo.`

**Fail if — any of these**

- No warning at step 1 -> the single most common real mistake (a capitalisation typo in a token) goes unflagged, and the token survives verbatim into the prompt a model reads.
- A finding at step 3, 4 or 5 -> the validator and the renderer now disagree about what a placeholder is; worse, if such a span is also being SUBSTITUTED at render time, prompt text an author wrote on purpose is being silently rewritten.
- Step 1's finding shown in red -> severities have collapsed, so a real Error can no longer be distinguished from a probable typo.

**Inconclusive if**

The empty parenthesised list at step 7 is ugly but correct — it is what an empty list joins to. Do not fail on it; note it as a cosmetic finding if you wish. If your editor/browser auto-pairs braces and you end up with more braces than intended, clear the field and retype carefully rather than judging from an unintended string.

### HOOKSSETTINGS-23 — Validation runs live against the pending value and clears without saving

**Free** · about 5 min

*Proves findings judge what is in the box, not what is on disk — otherwise a user fixing an error would see it stick around and would save again to clear it.*

**Before you start**

- No hooks.json.
- The app is running, on /settings (Hooks tab).
- File Explorer open on App_Data.

**Steps**

1. In `Room label`, delete the `{{roomId}}` token, watching the area under the field as you type.
2. Confirm the red error appears.
3. Press Ctrl+Z (or retype `{{roomId}}` in place) and watch the area under the field.
4. Check Explorer: confirm `hooks.json` is still absent.
5. Now break TWO fields at once: delete `{{roomId}}` from `Room label`, and select-all-and-delete the contents of `Help: footer`.
6. Read the issue lines under each of those two fields, and under a third, untouched field such as `Chat rules`.
7. Press F5 to discard.

**Pass if — all of these**

- The error in step 1 appears while typing, before any blur or save.
- The error disappears immediately in step 3, again with no save.
- `hooks.json` never appears — nothing reached disk at any point.
- In step 6, `Room label` shows only its own missing-placeholder error, `Help: footer` shows only its own empty-text error, and `Chat rules` shows no findings at all.

**Fail if — any of these**

- Findings refreshing only on Save or on reload -> validation is running against the stored text rather than the pending value, so a user cannot see whether their fix worked until after they commit it.
- A stale finding remaining after the text is fixed -> the same defect from the other side; users will learn to ignore the findings.
- A finding from one field appearing under another -> findings are not keyed to their own hook, so the user is sent to fix the wrong box.

**Inconclusive if**

If nothing updates live at all, first re-run HOOKSSETTINGS-08 — if the textarea row count is also frozen, the per-keystroke re-render is broken globally and THAT is the finding to report; this test is then inconclusive rather than a separate validator defect.

### HOOKSSETTINGS-24 — Hook text is rendered as text, never as markup

**Free** · about 6 min

*Hook text is the closest thing on this page to untrusted input reaching the DOM; this proves it cannot execute or break the layout.*

**Before you start**

- No hooks.json.
- The app is running, on /settings (Hooks tab).
- Browser devtools open on the Console tab.

**Steps**

1. Click into the `Help: footer` textarea, press Ctrl+A, and type exactly: `</textarea><script>alert('x')</script>`
2. Watch the page and the devtools Console.
3. On a new line in the same box, type exactly: `<img src=x onerror=alert(1)>`
4. On a new line, type exactly: `# Hi {onclick="alert(1)"}`
5. In the `Room label` field, type exactly `{{<script>}}` at the end of the text and read the resulting warning line under the field.
6. Click `Save`.
7. Press F5 to reload the page.
8. Read the `Help: footer` and `Room label` textareas.
9. Open `App_Data\hooks.json` and read the stored values.

**Pass if — all of these**

- No alert box appears at any point.
- The devtools Console shows no script executing and no injected-script errors.
- The page layout stays intact — no field runs together with another, the form is not truncated, and the Save button is still at the bottom.
- All the typed characters appear literally inside their textareas.
- The warning produced in step 5 renders `{{<script>}}` as visible text with literal angle brackets, not as an element.
- After the reload everything is still literal and still inert.
- `hooks.json` holds the literal characters.

**Fail if — any of these**

- Any script executing (an alert, or a console entry from the injected code) -> stored cross-site scripting in the Settings page, reachable by anyone who can hand-edit hooks.json or use this form.
- The page layout breaking because the textarea closed early -> the value is being written as raw markup rather than as an attribute/text node.
- Angle brackets disappearing from the validation message -> the finding text is being rendered as HTML.

**Inconclusive if**

If your browser blocks alert() dialogs by policy, do not conclude the payload was inert — watch the devtools Console instead, and add `console.log('XSS')` style payloads if needed. If you cannot observe script execution either way, mark INCONCLUSIVE and say the environment suppressed the oracle.

> [!NOTE]
> Run `P-RESET-SETTINGS` and relaunch afterwards — leaving these payloads saved will confuse later tests.

### HOOKSSETTINGS-25 — Typing in a long hook field stays responsive and loses no characters

**Free** · about 6 min

*Every keystroke round-trips to the server and rebuilds the whole 22-field form, which makes this the most likely place in the app for a Blazor Server input regression.*

**Before you start**

- The app is running, on /settings (Hooks tab).
- No hooks.json.

**Steps**

1. Find the `Help: Rooms` field (one of the longest defaults).
2. Click to place the caret in the MIDDLE of its existing text — not at the end — for example just after the word `members` on the second line.
3. Type this sentence quickly, without pausing: `the quick brown fox jumps over the lazy dog`
4. Read back exactly what landed and where the caret ended up.
5. Repeat the same in the `Tools` field, placing the caret mid-text.
6. Now select all of `Help: Rooms`, and paste in a block of a few thousand characters (for example, paste the same paragraph twenty times).
7. Immediately after the paste, keep typing a short sentence and watch for lag or dropped characters.
8. Add several newlines and confirm the box's automatic row growth continues smoothly with no caret jump. (There is no manual resize to fight — see HOOKSSETTINGS-08's note on `MudTextField`'s `resize: none`.)

**Pass if — all of these**

- Every typed character lands, in the order typed.
- The text is inserted where the caret was, not appended at the end of the box.
- The caret stays where you put it after each keystroke.
- After the large paste, typing continues normally with no visible lag beyond a fraction of a second.
- The box grows rows as newlines are added without the caret jumping.

**Fail if — any of these**

- The caret jumping to the end of the textarea after each keystroke -> the value is being re-applied on every render and users cannot edit anywhere but the end of a field; on a 14-row hook this makes the editor unusable.
- Characters arriving out of order or being dropped -> input events are racing the re-render and a user's saved prompt would silently differ from what they typed.
- Multi-second lag per keystroke -> the whole-form rebuild has become too expensive to type through.

**Inconclusive if**

If you are testing over a slow or remote connection, latency is expected and is not this defect. Re-run against `http://localhost:5100` on the machine running the app before reporting lag. Character LOSS and caret jumping are defects regardless of latency.

> [!NOTE]
> Discard with F5; do not save.

### HOOKSSETTINGS-26 — Uncommitted edits survive a tab switch and a theme change, but are silently discarded by reload or leaving the page

**Free** · about 9 min

*Pins which gestures preserve pending work and which destroy it. Before the MudBlazor migration a theme change forced a full page reload and was the gesture most likely to surprise a user by silently discarding work; now that Appearance applies in place with no reload (see `appearance-theme.md`), a theme change is expected to behave exactly like a tab switch — this test now proves that inversion instead of the old reload.*

**Before you start**

- No hooks.json and no appearance.json.
- The app is running, on /settings (Hooks tab).

**Steps**

1. Type ` KEEPME` into `Room label` and ` KEEPME2` into `Help: budget`. Do NOT save.
2. Click the `Appearance` tab.
3. Click the `Hooks` tab.
4. Read both fields and their badges.
5. Press the browser Back button, then Forward. Read both fields again.
6. Now press F5. Read both fields and their badges.
7. Type ` LOSEME` into `Room label` (no save). Click `Teammates` in the sidebar, then click `Settings`. Read the field.
8. Type ` LOSEME2` into `Room label` (no save). Click the `Appearance` tab. In the `Appearance` select, choose `Dark`.
9. Watch what the browser does — in particular, whether it performs a full page load.
10. Click the `Hooks` tab and read `Room label`.
11. Look for any 'you have unsaved changes' prompt at any point in steps 6, 7 or 8.

**Pass if — all of these**

- After steps 2-4 both edits and both `Unsaved` badges are still present — a tab switch preserves pending work.
- Back/Forward between the two tabs behaves the same way.
- After F5 (step 6) both edits are GONE and the fields show their defaults, with no badges.
- After step 7 the edit is GONE — leaving the Settings page for Teammates and back destroys pending work, because it is a genuinely different route with no Hooks component instance to return to.
- After step 8 the page repaints dark IMMEDIATELY with NO page load (see `appearance-theme.md`'s APPEARANCETHEME-04) — the whole point of this step is that the theme change behaves like the harmless tab switch in steps 2-4, not like F5.
- After step 10 the `LOSEME2` edit is STILL PRESENT with its `Unsaved` badge — the theme change did not destroy it.
- No unsaved-changes prompt appears at any point — this is current designed behaviour.

**Fail if — any of these**

- Edits lost on a plain tab switch (steps 2-4) -> a regression: the Settings component instance is being torn down on a route change that only swaps panes, and a user loses work simply by looking at the theme picker.
- The theme change forces a full page reload -> the old reload mechanism is back; per `appearance-theme.md` it is no longer needed and no longer correct.
- The `LOSEME2` edit is gone after step 10 -> the theme change is destroying the Settings component (or its pending-edit state) even though it no longer navigates or reloads; report this alongside whatever caused it, since nothing about the current design should discard state here.

**Inconclusive if**

The absence of an unsaved-changes prompt is documented designed behaviour, not a defect. If you believe there should be one, file it as a UX suggestion, not a bug. If the `Appearance` select is missing or empty, the Appearance tab has its own problem — report that against the Appearance area and mark step 8 inconclusive here.

> [!NOTE]
> Clean up afterwards with `P-RESET-SETTINGS`, which removes both the `appearance.json` this test created and any `hooks.json`, then relaunch.

### HOOKSSETTINGS-27 — "Reset all to defaults" is also rendered on the Appearance tab, where it acts on Hooks

**Free** · about 6 min

*Confirms an existing UX trap: a destructive control labelled only 'Reset all to defaults' appears on a tab about themes and silently targets Hooks. The test is to verify the blast radius, not to assume it is a bug in the code.*

**Before you start**

- The app is running, on /settings.
- File Explorer open on App_Data.

**Steps**

1. Type ` MARKER` into `Room label` and click `Save`, so `hooks.json` holds one key.
2. If `appearance.json` exists, copy its content to a scratch file; if it does not exist, note that it is absent.
3. Click the `Appearance` tab.
4. Look at the page header, to the right of the `Settings` heading.
5. Click `Reset all to defaults`.
6. Look at what replaces it.
7. Click `Yes, reset everything`.
8. Look for a `Save` button anywhere on the Appearance pane.
9. Check `App_Data\appearance.json` — its content, or its continued absence.
10. Check `App_Data\hooks.json` — its content.
11. Click the `Hooks` tab and read `Room label` and its badges.

**Pass if — all of these**

- The `Reset all to defaults` button is present in the header on the Appearance tab and is ENABLED (because a hook is modified), even though the pane shows only theme settings.
- Clicking it shows the same inline `Yes, reset everything` / `Cancel` confirm pair there.
- Confirming does NOT change `appearance.json` (unchanged content, or still absent).
- `hooks.json` is UNCHANGED — still holding the `turn.roomLabel` override — because there is no Save button on this tab to commit with.
- Switching back to the Hooks tab shows `Room label` at its shipped default with an `Unsaved` badge: the reset was staged and survived the tab switch.

**Fail if — any of these**

- `appearance.json` being changed or created by the confirm -> the control's blast radius has widened beyond hooks and a user's theme settings can be destroyed by a button that is about hooks.
- `hooks.json` being rewritten from the Appearance tab -> a destructive write is now reachable from a tab with no Save button and no visible list of what it affects.

**Inconclusive if**

If the header button is NOT rendered on the Appearance tab, that is a change from the described behaviour — verify it is deliberate before filing anything, and record it as an observation rather than a pass or a fail.

> [!NOTE]
> Whatever the outcome, report the shape of this control as a UX finding: a danger-styled button labelled only 'Reset all to defaults', shown on a tab about themes, that silently targets a different feature. That is what the current markup does — the header sits outside the tab switch.

### HOOKSSETTINGS-28 — A Save in one browser tab repaints /settings open in another, without eating that tab's typing

**Free** · about 7 min

*Proves the store's change event reaches every open circuit, and that live repaints never clobber a second user's uncommitted work.*

**Before you start**

- No hooks.json.
- The app is running.

**Steps**

1. Open `http://localhost:5100/settings` in browser tab A.
2. Open `http://localhost:5100/settings` in a second browser tab, tab B.
3. In tab A, type ` FROMA` at the end of `Chat rules` and click `Save`.
4. Switch to tab B WITHOUT reloading it. Wait up to five seconds and read the `Chat rules` textarea and its badges.
5. In tab B, type ` TYPEDINB` into `Room label`. Do not save.
6. Switch to tab A and type ` SECOND` into `Help: footer`, then click `Save`.
7. Switch to tab B without reloading. Read `Room label` and `Help: footer`.
8. Close tab B entirely.
9. Read the server console for the next ten seconds.
10. Open and close `/settings` in a new tab five times in a row, watching the console each time.

**Pass if — all of these**

- At step 4 tab B's `Chat rules` textarea shows tab A's new text and carries a `Modified` badge, with no reload.
- At step 7 tab B still shows ` TYPEDINB` in `Room label` with an `Unsaved` badge — its own uncommitted typing is preserved.
- At step 7 tab B's `Help: footer` has picked up tab A's second save.
- No JavaScript errors appear in either tab's devtools Console.
- The server console logs no exception when tab B is closed, and none across the five open/close cycles.

**Fail if — any of these**

- Tab B never updating without F5 -> the store's change event is not reaching other circuits; two people editing hooks would silently overwrite each other.
- Tab B losing its uncommitted typing when tab A saves -> a repaint is clobbering pending edits, which is the worse of the two failure directions.
- An exception on tab close, or errors accumulating across the five cycles -> a component subscribing to the singleton store is not unsubscribing on dispose; a leaked subscription to a singleton never dies and will grow with every page visit.

**Inconclusive if**

If the repaint takes longer than about five seconds but does arrive, note the delay and treat the behaviour as pass with an observation rather than a failure. If tab B is a different browser profile or a private window, that is fine — the store is server-side, so it should still repaint.

### HOOKSSETTINGS-29 — A hand-edit to hooks.json reaches the open page within about a second, with no restart and no refresh

**Free** · about 6 min

*Proves the documented support for hand-editing the file — 'a save in an editor reaches the next Turn without a restart'.*

**Before you start**

- No hooks.json.
- The app is running and /settings (Hooks tab) is open and visible.

**Steps**

1. Arrange the browser and a text editor side by side so you can see the `Room label` field and the editor at the same time.
2. In the editor, create a new file at `E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json` (use the path the Settings page prints) with exactly this content: `{"turn.roomLabel": "[R {{roomId}}]"}`
3. Save the file and DO NOT touch the browser.
4. Watch the `Room label` textarea for the next five seconds.
5. Read the `Room label` header badges and the header's `Reset all to defaults` button.
6. Now, in the editor, change the value to `[RR {{roomId}}]` and save. Then immediately change it to `[RRR {{roomId}}]` and save, then to `[RRRR {{roomId}}]` and save — three saves in quick succession.
7. Watch the browser and count how many times the field visibly changes.
8. Read the server console.

**Pass if — all of these**

- Within roughly half a second to a second of step 3, the `Room label` textarea's text changes to `[R {{roomId}}]`, with no page reload and no app restart.
- A `Modified` badge appears on `Room label`, and `Reset all to defaults` becomes enabled.
- After step 6 the field settles on `[RRRR {{roomId}}]`; the rapid saves do not produce a storm of repaints (a ~500ms debounce coalesces them).
- No field ever visibly blanks to its default and then flips back.
- The server console shows no warning for these well-formed edits.

**Fail if — any of these**

- Nothing happening until you press F5 -> the file watcher is dead and the whole documented hand-editing workflow is gone; a user's edit would appear to do nothing.
- A visible flicker where every field blanks to defaults and then flips back -> the mid-write race is no longer being retried, so a normal editor save momentarily reverts every hook.
- One repaint per keystroke or per save with visible thrash -> the debounce is gone.

**Inconclusive if**

If nothing updates, before failing, check the server console for the line `HookStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.` If that line is present, the watcher hit a known environmental condition — note it and retry once. If your editor writes via a temp-file-and-rename (some do), the watcher is designed to handle it; if it still does not update, try saving with plain Notepad before concluding.

> [!NOTE]
> Keep hooks.json for the next test.

### HOOKSSETTINGS-30 — Deleting hooks.json while the app runs reverts every field to its shipped default, live

**Free** · about 5 min

*Proves a delete is tolerated as a legitimate 'revert everything' gesture, exactly like an empty file, and that the app never recreates the file.*

**Before you start**

- hooks.json exists with at least two overrides.
- The app is running and /settings (Hooks tab) is open and visible.

**Steps**

1. If you do not already have overrides, type ` ONE` into `Room label` and ` TWO` into `Help: budget` and click `Save`.
2. Note which fields carry `Modified` badges.
3. In File Explorer, delete `App_Data\hooks.json`. Do NOT touch the browser.
4. Watch the browser for the next five seconds.
5. Read the two previously-overridden textareas and every field's badges.
6. Read the header's `Reset all to defaults` button state.
7. Refresh Explorer and check whether `hooks.json` has reappeared.
8. Wait thirty seconds and check Explorer again.
9. Read the server console.

**Pass if — all of these**

- Within about a second of the delete, both textareas revert to their shipped wording, with no reload and no restart.
- Every `Modified` badge disappears across all 22 fields.
- `Reset all to defaults` goes DISABLED.
- `hooks.json` stays ABSENT — the app does not recreate it, then or thirty seconds later.
- No exception page or yellow Blazor error strip appears.
- The server console shows no error for the delete.

**Fail if — any of these**

- The page keeping the deleted overrides until a restart -> the delete gesture is unsupported, and a user who removes the file to start over sees stale text with no explanation.
- The app recreating the file -> it must not; a recreated file of defaults silently pins today's wording.
- An exception banner or a dead circuit -> a file delete must be tolerated exactly like an empty file.

**Inconclusive if**

If Windows refuses the delete because a text editor still holds the file open, close the editor and retry — that is not a defect in the app.

### HOOKSSETTINGS-31 — SILENT: a malformed hooks.json edited while running changes nothing on screen and says nothing — check the log

**Free** · about 7 min

*This is the area's headline silent failure. The correct behaviour is to keep the last-good text and log a warning; a tester who only looks at the screen will conclude nothing happened.*

**Before you start**

- The app is running and /settings (Hooks tab) is open and visible.
- The server console terminal is visible.

**Steps**

1. Type ` GOODVALUE` at the end of `Room label` and click `Save`, so `hooks.json` holds a valid override.
2. Confirm `Room label` shows your text with a `Modified` badge.
3. In the text editor, open `App_Data\hooks.json` and delete the final closing brace `}` so the JSON is invalid. Save the file.
4. Watch the browser for ten full seconds. Do not touch it.
5. Read the `Room label` textarea, its badges, and the whole page for any error message.
6. Look at the bottom of the browser for the yellow Blazor error strip.
7. Now read the server console output.
8. Restore the file: put the closing brace back and save.

**Pass if — all of these**

- Nothing changes on screen: `Room label` still shows the last-good text ending in ` GOODVALUE`, still with its `Modified` badge.
- There is NO error message anywhere on the page — no red line, no banner, no yellow Blazor strip.
- The server console shows a Warning containing: `Could not parse hook overrides file '<path>' after a filesystem change, even after retrying; keeping the previously resolved hook text rather than reverting every hook to its catalog default over what may be a mid-write race.`
- After step 8 (file restored) the page continues to show the override with no further action needed.

**Fail if — any of these**

- Every field flipping to its shipped default -> the wrong tolerance: an editing session's overrides vanish over what may be nothing more than a mid-write save, and the user's customisation appears destroyed.
- An exception page or a dead Blazor circuit -> a hand-edited file must never be able to kill the page.
- The warning line MISSING from the console -> now the failure is genuinely undiagnosable: nothing on screen, nothing in the log. That IS a defect, even though the on-screen silence is not.

**Inconclusive if**

The total absence of on-screen feedback is DOCUMENTED DESIGN, not a bug — do not file it. If you see nothing on screen AND nothing in the console, wait a further ten seconds and re-read the console before concluding (the retry window plus the debounce can delay the warning by about a second, but not by more than a few). If your console log level has been raised above Warning, lower it: `Logging:LogLevel:Agency.Huddle` must be at `Information` or lower for this oracle to work.

> [!NOTE]
> This is exactly the kind of failure CI cannot see. Prefer running it early if time is short.

### HOOKSSETTINGS-32 — SILENT: a malformed hooks.json at STARTUP falls back to defaults wholesale, with no UI clue and the file left intact

**Free** · about 7 min

*Proves a hand-edited file can never prevent startup and can never be destroyed by the app, while confirming the log line is the only diagnosis available.*

**Before you start**

- hooks.json exists with at least one override.
- You can stop and start the app.

**Steps**

1. Stop the app with Ctrl+C in the terminal.
2. In the text editor, replace the entire contents of `App_Data\hooks.json` with: `{"turn.roomLabel": 42}` (a non-string value) and save. Copy that content to a scratch file.
3. Start the app: `dotnet run --project src/Huddle.App` from the repo root.
4. Watch the console output during startup.
5. Browse to `http://localhost:5100/settings`.
6. Read all 22 fields and their badges.
7. Read the whole page for any error, banner or mention of an unreadable file.
8. Open `App_Data\hooks.json` and compare its content to the scratch copy.
9. Repeat steps 1-8 with a second kind of corruption: replace the file contents with `{"turn.roomLabel": ` (truncated, no closing brace or value).

**Pass if — all of these**

- The app STARTS normally both times — no crash, no startup exception.
- The console prints a Warning containing: `Could not parse hook overrides file '<path>'; falling back to defaults for every hook.`
- All 22 fields show their shipped defaults and NO `Modified` badge — exactly as if the file were absent.
- The page shows no error, no banner and no mention of the file being unreadable.
- `hooks.json` is still on disk with its corrupt content, byte-identical to the scratch copy — the app neither overwrote nor deleted it.

**Fail if — any of these**

- The app failing to start -> a hand-edited file must never be able to prevent startup; a user with a typo would have a dead application and only a stack trace to go on.
- The corrupt file being overwritten or deleted at startup -> the user's work (perhaps a typo they were about to fix, or a file they meant to keep) is destroyed with no confirmation.
- The warning line MISSING from the console -> nothing on screen and nothing in the log makes this failure genuinely undiagnosable, and that IS a defect.

**Inconclusive if**

No on-screen warning is DOCUMENTED DESIGN — do not file it. If the console scrolled past the startup warning, stop the app, redirect output to a file (`dotnet run --project src/Huddle.App > C:\Users\user\AppData\Local\Temp\huddle.log 2>&1`) and search the log rather than guessing.

> [!NOTE]
> Beware a follow-on hazard worth reporting if you see it: with a corrupt file in place, a subsequent Save from the browser reads the file as empty and will write only your new edits — the corrupt content is then gone. That is a consequence of the fallback, not a separate bug, but say so in your report if a tester might lose data that way.

### HOOKSSETTINGS-33 — An unknown key in hooks.json is kept forever, ignored for resolution, and logged once

**Free** · about 6 min

*Proves the app never deletes someone else's data from the file — a typo they are about to fix, or a key from a newer build.*

**Before you start**

- The app is running, /settings open.
- The server console is visible.

**Steps**

1. In the text editor, set `App_Data\hooks.json` to exactly: `{"turn.roomLabel": "[R {{roomId}}]", "my.experiment": "hello"}` and save.
2. Watch the browser for five seconds.
3. Read the `Room label` textarea and its badges.
4. Scan the whole Hooks tab for any field, error or mention of `my.experiment`.
5. Read the server console.
6. Now, in the browser, type ` FROMUI` at the end of `Help: footer` and click `Save`.
7. Open `App_Data\hooks.json` and read its full contents.

**Pass if — all of these**

- `Room label` picks up `[R {{roomId}}]` normally and shows a `Modified` badge.
- No field appears for `my.experiment`, and no error about it appears anywhere on the page.
- The server console shows an Information line containing: `Hook overrides file '<path>' contains key 'my.experiment', which is not a hook this application knows about; it is kept in the file but ignored when resolving hook text.`
- After the save at step 6, `hooks.json` still contains `"my.experiment": "hello"`, untouched, alongside `turn.roomLabel` and the new `getHelp.footer` key.

**Fail if — any of these**

- `my.experiment` being dropped from the file after the save -> that is someone's data, silently deleted; a user prototyping a key from a newer build, or fixing a typo, loses it with no warning.
- The unknown key producing an on-page error, or preventing the known overrides from resolving -> one stray key would disable every override in the file.
- The known override `turn.roomLabel` not being applied -> resolution is failing on the whole file rather than skipping the one unknown key.

**Inconclusive if**

If you do not see the Information line, check that the console log level for `Agency.Huddle` is at `Information` (the default) rather than `Warning` — raise it back before failing the test. The absence of the log line alone, with correct file and UI behaviour, is a minor finding, not a failure of the data-preservation rule.

### HOOKSSETTINGS-34 — A pending browser edit beats a concurrent hand-edit to the same key, and Save merges rather than overwrites

**Free** · about 8 min

*Proves the two documented halves of the conflict policy: half-typed work is never wiped by an external edit, and a save never clobbers unrelated keys.*

**Before you start**

- No hooks.json.
- The app is running, /settings (Hooks tab) open.
- A text editor ready on the hooks.json path.

**Steps**

1. In the browser, type ` FROMBROWSER` at the end of `Room label`. Do NOT save.
2. Leave the browser untouched. In the editor, create `App_Data\hooks.json` with exactly: `{"turn.roomLabel": "[FROM-FILE {{roomId}}]", "turn.message": "FILE {{roomLabel}} {{sender}}: {{text}}"}` and save it.
3. Watch the browser for five seconds.
4. Read the `Message` textarea.
5. Read the `Room label` textarea.
6. Now click `Save` in the browser.
7. Open `App_Data\hooks.json` and read both values.

**Pass if — all of these**

- At step 4 the `Message` field HAS updated to `FILE {{roomLabel}} {{sender}}: {{text}}` — a key with no pending edit picks up the hand-edit.
- At step 5 the `Room label` field still shows YOUR typed text ending in ` FROMBROWSER` — the uncommitted edit wins for its own key.
- No conflict warning or error is shown (this is designed, silent behaviour).
- After the Save, `hooks.json` holds your browser text for `turn.roomLabel` AND the hand-edited `FILE {{roomLabel}} {{sender}}: {{text}}` for `turn.message`.

**Fail if — any of these**

- Your half-typed `Room label` text being wiped by the external edit -> losing the user's in-progress work is the worse of the two outcomes; a hand-edit is recoverable from the file, a half-typed edit is not.
- The `Message` field NOT updating -> pending edits are winning for keys the user never touched, so a hand-edit appears to do nothing until a reload.
- `turn.message` missing or reverted in the file after the save -> the save is writing a wholesale snapshot instead of re-reading and merging, so any key edited outside the browser is destroyed by the next Save from the browser.

**Inconclusive if**

If neither field updates at step 3, re-run HOOKSSETTINGS-29 first — if the watcher is not delivering hand-edits at all, this test cannot be judged and the watcher is the finding to report.

> [!NOTE]
> The silent loss of the hand-edit to turn.roomLabel is designed behaviour. Report it as a UX observation if you like ('a concurrent hand-edit is discarded with no conflict notice'), not as a defect.

### HOOKSSETTINGS-35 — A CRLF hand-edit makes a field show "Modified" while looking identical — and Reset fixes it

**Free** · about 8 min

*Pre-empts a misreport ('a field says Modified but nothing is different') and checks that the recovery path works and leaves the file clean.*

**Before you start**

- No hooks.json.
- The app is running, /settings (Hooks tab) open.
- A text editor that can set line endings (VS Code shows LF/CRLF in its status bar; Notepad++ has Edit > EOL Conversion).

**Steps**

1. Open `src\Huddle.App\hooks.default.json` and copy the exact value of `getHelp.rooms` (a multi-line hook).
2. In your editor, create `App_Data\hooks.json` containing just that one key and value, but set the editor's line endings to CRLF (Windows) before saving.
3. Save the file and switch to the browser.
4. Wait a second, then read the `Help: Rooms` field: its text and its badges.
5. Compare the text on screen, line by line, with the shipped default in `hooks.default.json`.
6. Click the `Reset` button on the `Help: Rooms` field.
7. Read the badges again.
8. Click `Save`.
9. Open `App_Data\hooks.json` and list its keys.

**Pass if — all of these**

- `Help: Rooms` gains a `Modified` badge.
- The textarea text looks character-for-character identical to the shipped wording — you cannot see any difference.
- Clicking `Reset` clears the `Modified` badge and raises `Unsaved`.
- After `Save`, `hooks.json` no longer contains `getHelp.rooms` — it is removed, not re-stored with the CRLF text.

**Fail if — any of these**

- After Reset + Save, the file STILL containing `getHelp.rooms` -> the reset-to-default rule is not removing the key, so the installation stays pinned to a copy of today's wording forever and a future change to the shipped default would never reach it.
- Reset not clearing the `Modified` badge -> the recovery path from this situation does not exist, and a user has no way to get the field back to 'unmodified' except by hand-editing the file with the right line endings.

**Inconclusive if**

A `Modified` badge with no visible difference is NOT a defect in itself — the comparison is deliberately ordinal, so a CRLF/LF difference counts. Do not file it as 'Modified badge is wrong'. If you cannot make your editor write CRLF, run this instead in PowerShell to build the file and then re-judge from step 3: read the default value, replace `\n` with `\r\n`, and write the JSON. If you cannot produce the CRLF file at all, mark this test INCONCLUSIVE.

> [!NOTE]
> Verify the line endings you actually wrote with a hex-capable editor, or in PowerShell: `(Get-Content -Raw 'E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json') -match "`r`n"`.

### HOOKSSETTINGS-36 — Loading /settings never starts an adapter (node) process

**Free** · about 5 min

*Proves the page that has nothing to do with models never spawns one — the same rule the repo pins for /teammates.*

**Before you start**

- `Team:Acp:Enabled` is false (the default).
- The app is running.

**Steps**

1. In `T-B` run `O-ADAPTERS-LIST` and note the result (probably nothing).
2. In the browser, load `http://localhost:5100/settings`.
3. Re-run `O-ADAPTERS-LIST`.
4. Press F5 on the page five times, then re-run the command.
5. Click the `Appearance` tab and the `Hooks` tab three times each, then re-run the command.
6. Type into three fields and click `Save`, then re-run the command.
7. Read the server console for any line mentioning starting an adapter or a process.

**Pass if — all of these**

- No `node` process exists after any of steps 2-6 that did not exist before step 1.
- The server console logs no adapter or process start as a result of loading, reloading, tab-switching, typing or saving on /settings.

**Fail if — any of these**

- A `node` process appearing as a result of a /settings page load or save -> a process is being spawned on every render of a page that has nothing to do with models; on a slow machine this also makes the Settings page visibly slower, and with ACP enabled it could start billing paths from an unrelated page.

**Inconclusive if**

If you already have `node` processes running for unrelated reasons (an editor's language server, a dev tool), compare by process Id and StartTime rather than by count — only a NEW process started at the moment you loaded the page counts. If you cannot tell, close unrelated node processes and re-run, or mark INCONCLUSIVE rather than guessing.

> [!NOTE]
> Contrast with /teammates, where opening a New or Edit card legitimately probes for models and may start an adapter. That is expected there and out of scope here.

### HOOKSSETTINGS-37 — Saving a Hook must NOT restart any teammate's session

**Free** · about 12 min

*Proves the binding rule that editing a hook never throws away an agent's conversation memory. A restart here is invisible except as a teammate that suddenly forgot the conversation.*

**Before you start**

- `Team:Acp:Enabled=true` with a working `Team:Acp:AdapterPath`.
- At least one Persona markdown file under `App_Data\Teams\`, configured on /teammates with Model = Haiku and Effort = low.
- The app has been restarted since enabling ACP and the teammate shows Online on /teammates.

**Steps**

1. Open `http://localhost:5100/teammates` in browser tab A and confirm your teammate's status badge reads Online.
2. Open `http://localhost:5100/settings` in browser tab B.
3. Clear or note the current end of the server console output.
4. In tab B, type ` EDIT1` into `Room label` (a Live hook) and click `Save`.
5. Switch to tab A and watch the status badge for ten seconds.
6. In tab B, type ` EDIT2` into `Identity` (a `Next session` hook) and click `Save`.
7. Switch to tab A and watch the status badge for ten seconds.
8. In tab B, type into `Tools`, `Chat rules` and `get_help description` and click `Save` once for all three.
9. Switch to tab A and watch the status badge for ten seconds.
10. Read everything the server console printed since step 3.

**Pass if — all of these**

- The teammate's status badge stays `Online` throughout — it never flickers to `Starting`, never goes `Offline`, and the card never shows a `Restart` button (which only appears when Offline or Degraded).
- The server console shows NO start, stop or restart log lines for any Persona across steps 4-9.
- Each Save otherwise behaves normally (badges clear, Save goes disabled).

**Fail if — any of these**

- Any status change or restart log line after a hook Save -> binding rule violated. A restart silently destroys the agent's conversation memory, and the only symptom a user ever sees is a teammate that suddenly forgot what was being discussed. Saving five edits as one save must produce at most one store event and zero restarts.
- The `Restart` button appearing on the card after a hook save -> the teammate went Offline or Degraded as a consequence of the save.

**Inconclusive if**

If the teammate is not Online before you start (Offline or Degraded), this test cannot distinguish 'a save restarted it' from 'it was already broken'. Fix the teammate first — restart the app and confirm Online — or mark INCONCLUSIVE. If ACP cannot be enabled in your environment (no adapter path), mark the test INCONCLUSIVE and say so; do NOT pass it from the ACP-off configuration, where no session exists to restart.

> [!NOTE]
> This test enables ACP but prompts no Turn, so NO tokens are billed. The only cost is starting the node adapter processes, which happens at boot anyway.

### HOOKSSETTINGS-38 — COSTS MONEY: a Live hook edit reaches the very next Turn with no restart

**💰 Spends money** · about 15 min

*Proves the thirteen unbadged hooks really are live — a Live hook that needed a restart would defeat the whole feature.*

**Before you start**

- `Team:Acp:Enabled=true` with a working `Team:Acp:AdapterPath`.
- One Persona configured on /teammates with Model = Haiku and Effort = low.
- A Room containing that teammate and you.
- The teammate shows Online.

**Steps**

1. Open the Room with your teammate and send one short message: `hi`
2. Confirm the teammate replies. (This is Turn 1.)
3. In another tab open `/settings`, find `Room label`, and replace its entire text with exactly: `((ROOM {{roomName}} :: {{roomId}}))`
4. Click `Save`. Do NOT restart anything.
5. Return to the Room and send exactly this one-line message: `Quote back, verbatim, only the first line of the message you just received. Nothing else.`
6. Read the reply.

**Pass if — all of these**

- The teammate replies to the second message.
- The reply contains the new framing — it quotes something of the form `((ROOM <name> :: <id>))` — and NOT the old `[Room: <name> (id: <id>)]` form.
- No restart of the teammate was needed between the edit and the effect.

**Fail if — any of these**

- The reply still showing the old `[Room: ... (id: ...)]` framing -> a Live hook now needs a restart, which defeats the entire feature: every wording tweak would cost the teammate its conversation memory.
- The teammate not replying at all after the hook edit -> the edited hook broke turn delivery; check the server console for an exception and report that as the primary finding.

**Inconclusive if**

A model may paraphrase rather than quote verbatim, which makes the chat reply a weak oracle. If the reply is ambiguous, do NOT guess — use the wire oracle instead: set `Team:Acp:TraceWire=true` and `Logging:LogLevel:Agency.Huddle=Trace`, restart, redo the edit, send one message, and read the `session/prompt` text in the console. If ACP cannot be enabled at all, mark INCONCLUSIVE.

> [!NOTE]
> COST: two short Turns on Haiku at low effort — a few seconds of model time each, cents at most. Keep both messages to a single line. WARNING: wire tracing dumps the tool-server bearer token to the console; only use it in a throwaway debugging session, and turn it off afterwards.

### HOOKSSETTINGS-39 — COSTS MONEY: a "Next session" hook edit is silently inert on a running teammate until it restarts

**💰 Spends money** · about 20 min

*Proves the badged hooks behave as the badge says, and demonstrates that the badge is the ONLY warning that exists — nothing in the app or the log reports that the edit did not land.*

**Before you start**

- `Team:Acp:Enabled=true` with a working `Team:Acp:AdapterPath`.
- One Persona configured on /teammates with Model = Haiku and Effort = low.
- A Room with that teammate; the teammate shows Online.

**Steps**

1. In the Room, send exactly: `In one short sentence, what is your name and role?` Read the reply. (Turn 1, the baseline.)
2. In another tab open `/settings`, find the `Identity` field (it carries a `Next session` badge), and change its text to exactly: `You are a member of the Team chat application. You speak as "{{personaName}}". Always begin every reply with the word BANANA.`
3. Click `Save`. Confirm the `Unsaved` badge clears.
4. Watch the page and the server console for ten seconds for any message saying the edit will not apply.
5. Return to the Room and send exactly: `In one short sentence, what is your name and role?` Read the reply. (Turn 2.)
6. Now force a new session: go to /teammates, click `Edit` on that teammate, add a space to the end of its Persona text, and click `Save`. Wait for the status to return to Online. (If the card shows Offline or Degraded, the `Restart` button is available and does the same thing.)
7. Return to the Room and send exactly: `In one short sentence, what is your name and role?` Read the reply. (Turn 3.)

**Pass if — all of these**

- Turn 2's reply does NOT begin with `BANANA` — the running teammate's behaviour is unchanged by the edit.
- Nothing on the Settings page, in the Room, or in the server console reports that the edit did not land. The `Next session` badge on the field is the only warning that exists.
- Turn 3's reply, after the forced new session, DOES begin with `BANANA` — the new wording is now in effect.

**Fail if — any of these**

- Turn 2 already beginning with `BANANA` -> sessions are being restarted on a hook edit, which the rules file forbids: it throws away the agent's conversation memory every time someone rewords a sentence.
- Turn 3 still not showing the new wording -> the edit never reaches a new session at all, so the NextSession hooks are effectively uneditable.

**Inconclusive if**

A model may decline or forget a formatting instruction, so one non-BANANA reply is not conclusive on its own. If Turn 3 does not show BANANA, retry Turn 3 once before failing. For a definitive, model-independent oracle, enable `Team:Acp:TraceWire=true` with `Logging:LogLevel:Agency.Huddle=Trace` and compare the `_meta.systemPrompt` in the `session/new` message before and after the restart — the system prompt is sent once per session and never re-sent. If ACP cannot be enabled, mark INCONCLUSIVE.

> [!NOTE]
> COST: about three short Turns on Haiku at low effort — cents at most. The 'no on-screen warning' half is documented design, not a defect: report it only as context. WARNING: wire tracing dumps the tool-server bearer token; throwaway sessions only.

### HOOKSSETTINGS-40 — COSTS MONEY: get_help re-renders on every call, so its nine hooks land on the next call — while a tool DESCRIPTION does not

**💰 Spends money** · about 20 min

*Proves the Live/NextSession split holds across the two surfaces most easily confused: the body get_help returns (rebuilt per call) versus the tool's own description (sent once at session start).*

**Before you start**

- `Team:Acp:Enabled=true` with a working `Team:Acp:AdapterPath`.
- One Persona configured on /teammates with Model = Haiku and Effort = low.
- A Room with that teammate; the teammate shows Online.
- The teammate has been running for at least one Turn.

**Steps**

1. In another tab open `/settings` and find `Help: budget` (under `Get help`; it carries NO timing badge).
2. Add this sentence to the end of its text, on its own line: `PINEAPPLE is the safe word.`
3. Click `Save`. Do NOT restart anything.
4. In the Room, send exactly: `Call your help tool, then quote back only the BUDGET section of what it returned. Nothing else.`
5. Read the reply.
6. Now go back to `/settings` and find `get_help description` (under `Tool descriptions`; it carries a `Next session` badge). Add this to the end of its text: ` MANGO.`
7. Click `Save`. Do NOT restart anything.
8. In the Room, send exactly: `In one short sentence, what does the description of your help tool say? Quote its last few words.`
9. Read the reply.

**Pass if — all of these**

- The reply at step 5 contains `PINEAPPLE is the safe word.` — the get_help body is built fresh on every call, so a `Get help` hook edit lands on the very next call with no restart.
- The reply at step 9 does NOT contain `MANGO` — the running teammate's tool listing still carries the old description, because tool descriptions are sent once per session.

**Fail if — any of these**

- The get_help body coming back without `PINEAPPLE` -> it is being cached rather than rebuilt per call, so every `Get help` hook edit silently does nothing until a restart, while the field carries no `Next session` badge to warn anyone.
- The tool description showing `MANGO` without a restart -> tool descriptions are being re-read somewhere they cannot be, which contradicts the `Next session` badge on those five fields and means the badge is now misleading in the opposite direction.

**Inconclusive if**

Models are unreliable at quoting tool descriptions back, so step 9 is the weaker half. If the step-9 reply is evasive or the model claims it cannot see its tool descriptions, do NOT fail — mark that half INCONCLUSIVE and, if you need certainty, read the wire instead: enable `Team:Acp:TraceWire=true` with `Logging:LogLevel:Agency.Huddle=Trace` and inspect the tool list sent at `session/new` and the tool result text for the help call. If ACP cannot be enabled, mark the whole test INCONCLUSIVE.

> [!NOTE]
> COST: two to three short Turns on Haiku at low effort — cents at most. A get_help call returns a long block, so keep the request scoped to one section as written above, or the reply will be needlessly long. Remember the demo agents `echo` and `alpha` are pipe clients that never see hook-rendered text — never use them to test whether a hook took effect. WARNING: wire tracing dumps the tool-server bearer token; throwaway sessions only.

---

Back to [the manual test script](../manual-tests.md).
