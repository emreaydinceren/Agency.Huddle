# Agent card: view, edit, create, delete

Prove that the one Agent card (`AgentProfileCard.razor`, rendered over `/agents` in View, Edit and Create modes) is the only safe way to create, edit and delete a Persona file on disk — and that it fails loudly, not silently. Almost nothing here is provable by the compiler or the test suite: the create-card open path is untested and calls a helper that throws on a blank name; validation messages arrive from three layers in a fixed precedence; a save rewrites YAML frontmatter in the Persona's own file, and a changed Name moves the whole folder (ADR-0011); and delete-then-recreate is only provable by reading `<data>\Agents\<Name>\<Name>.md` and `<data>\team.db` alongside the screen. Every test below is free unless it says otherwise; exactly one test spends money.

> [!NOTE]
> Stage 4 of the MudBlazor migration (2026-09-14) replaced the card's hand-rolled overlay `<div>`
> with a real `MudDialog`, opened through `IDialogService`. This brought genuine dialog behaviour
> the old overlay never had — a focus trap, Escape-to-close, page-scroll locking while it is open,
> and focus returning to the control that opened it — verified in a browser, not assumed.
> AGENTPROFILECARD-14 is rewritten below to test that real behaviour instead of the old overlay's. Every
> other test in this area still applies unchanged: the identity fields, validation, save/collision
> logic and file I/O this area exists to prove are exactly as they were; only the "card" wording is
> now sometimes "dialog" to match what it actually is.

**53 tests** · 52 free, 1 paid 💰 · about 6.9 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB` / `O-WIRE`, the four resets,
`P-NEW-PERSONA`, `P-ECHO-BOT` and the standing conventions. This area adds:

1. Start from `E-STOPPED`, and take the [§0.4](../manual-tests.md#04-rollback) backup before anything else — tests in this area delete Persona files and rows from `team.db`, and that copy is the only undo.
2. Run `P-RESET-TEAMS`, which deletes every folder under `<data>\Agents`. Do NOT delete `team.db`. The area still never starts from an empty library: the built-in **Chief of Staff** (alias `cos`, no Team) is re-seeded at every start, whatever `Acp:Enabled` is, so "empty" below means "holds only the Chief of Staff". AGENTPROFILECARD-01 is the one test that removes it.
3. Lane is `P-LAUNCH-FREE`, or equivalently `dotnet run --project src/Huddle.App -- --Team:Acp:Enabled=false`. Every Agent then reads Offline and nothing is spawned for it. Part of this page (AGENTPROFILECARD-43 onward) leaves that lane and names `P-LAUNCH-UAT` in its own steps.
4. `O-DB` is REQUIRED from AGENTPROFILECARD-35 onward, not optional: install `sqlite3` and confirm `sqlite3 -version` in a fresh `T-B`. (DB Browser for SQLite is an acceptable substitute.)
5. Confirm `node --version` prints a version. AGENTPROFILECARD-37 onward need a node process spawn to be observable.
6. Open `http://localhost:5100/agents`. Keep a File Explorer window on `<data>\Agents` and a text editor able to show line endings (VS Code, Notepad++). A Persona lives at `<data>\Agents\<Name>\<Name>.md`; a `.md` directly under `Agents` is rejected.
7. **Clean-up.** After each test, remove every Persona it created, unless the next test says to keep it: open its card and use **Remove** (the person-with-a-minus icon, then **Remove** in the prompt), then delete the leftover `<data>\Agents\<Name>` folder in File Explorer. Never remove the Chief of Staff (its card offers **Reset to default**, not Remove). This keeps tests from leaking state into one another.

## Tests

### AGENTPROFILECARD-01 — Empty Agents page and the New agent entry point

**Free** · about 5 min

*Proves the page's zero-Persona state renders the empty message, and hides the Team filter entirely. The Chief of Staff is seeded at every start (`BuiltinAgentSeeder`), so the empty state is only reachable by deleting its file while the app runs.*

**Before you start**

- The app is running with `--Team:Acp:Enabled=false`, freshly started after `P-RESET-TEAMS`.
- `<data>\Agents` holds one folder, `Chief of Staff`, and no other Persona.

**Steps**

1. Navigate to http://localhost:5100/agents and confirm one tile, `Chief of Staff`, and a **Team** select in the toolbar.
2. In File Explorer delete the file `<data>\Agents\Chief of Staff\Chief of Staff.md` while the app keeps running. Do not delete `team.db`.
3. Wait about one second for the page to repaint.
4. Read the top of the page.
5. Look for a `<select>` labelled **Team** anywhere on the page.
6. Read the body text below the intro paragraph.
7. Restore the Chief of Staff: `P-STOP`, then `P-LAUNCH-FREE` again. The seeder writes it back at the start.

**Pass if — all of these**

- A heading reads exactly `Agents`, with a text button labelled `New agent` (an `Add` icon beside it) first in the toolbar, on the left.
- An intro paragraph begins `A Persona is a Markdown file describing how one agent should behave, plus the model it thinks with.`
- The body is an empty state: the title `No Agents yet` with the line `Choose New agent to add the first one.` under it.
- No **Team** filter select is rendered at all.

**Fail if — any of these**

- A **Team** filter select is rendered while the list is empty -> the filter is no longer inside the non-empty branch and will offer an empty choice list.
- The empty-state sentence is missing or replaced by a blank area -> a user with a genuinely empty Agents folder is given no next action and no explanation.

**Inconclusive if**

If the page shows tiles, a Persona file still exists under `Agents` (check each folder; the Chief of Staff may have been re-seeded if the app restarted). Delete them, wait ~1 second for the page to repaint, and repeat. If the page shows a `Files that didn't load` section, a non-Persona `.md` is present — remove it and repeat. If the browser shows nothing at all, the app is not running: restart it with the setup command and retry.

> [!NOTE]
> Run this first. Step 7 matters: every later test assumes the Chief of Staff is back, and it only returns at the next start.

### AGENTPROFILECARD-02 — Clicking New agent opens a card at all (blank-name crash probe)

**Free** · about 6 min

*A smoke test: the Create card opens with a random suggested Name, survives the Name being cleared, and the reset button beside the Name (`New random name`) draws a different one. Opening it passes no status (`BuildCreateParameters`, `Agents.razor:264-267`), and `ResolveStatus` guards a blank name (`Agents.razor:204-207`), so it can only fail on a regression.*

**Before you start**

- The app is running with `--Team:Acp:Enabled=false`.
- The app console is visible.

**Steps**

1. Go to http://localhost:5100/agents .
2. Click `New agent`.
3. Observe immediately whether a dialog headed `New agent` appears, or whether a pale-yellow bar appears at the bottom of the page reading `An unhandled error has occurred.` with a `Reload` link.
4. Read the app console and copy any exception text printed in the last few seconds.
5. Reload the browser page and click `New agent` a second time to confirm the outcome reproduces.
6. If the card DID open: type `Nova` into the **Name** field, press Tab, then select all of it, delete it and press Tab again so the field is empty again. Observe the card and the console.
7. If the card is still alive: type a single space into the **Name** field and press Tab. Observe the card and the console again.
8. Click the circular-arrow reset button at the right end of the **Name** field three times, noting the name after each click.

**Pass if — all of these**

- A dialog headed `New agent` appears on both attempts, with the **Name** field already holding a first name such as `Emily`.
- Each reset click shows a different first name from the one before.
- Clearing the **Name** field back to empty leaves the card on screen and working.
- Typing a single space as the whole **Name** leaves the card on screen and working.
- The console prints no exception during any of the above.

**Fail if — any of these**

- No card appears and the yellow bar `An unhandled error has occurred.` with a `Reload` link is shown -> the Blazor circuit was killed while the card rendered. Record the console's exception text verbatim; creating an agent is impossible.
- The card opens but dies the moment the Name is cleared or set to a single space -> a user who backspaces over their typo loses the whole form. Record the console's exception text verbatim.

**Inconclusive if**

If the yellow bar appears but the console shows an unrelated exception (a database lock, a disposed object), record the real exception and mark inconclusive. If the browser had a stale circuit from before an app restart, reload the page once and retry before judging.

> [!NOTE]
> Cheap, and every Create test (05-11, 18-21, 27, 28, 33) depends on the card opening. If it fails, report the block explicitly and run only the tests that need no new agent.

### AGENTPROFILECARD-03 — Create card: every label, placeholder and hint

**Free** · about 6 min

*Proves the Create card shows the create-specific wording and no status line, and lists every control it renders in its real order, so a user is never told to type frontmatter they must not type.*

**Before you start**

- AGENTPROFILECARD-02 passed, so the Create card can be opened.
- Wait a few seconds after the card opens before judging the Model, Effort and Work mode controls: the model probe fills them in.

**Steps**

1. Click `New agent`.
2. Read the card's header text and hover the small button on its right.
3. Read the four text inputs from top to bottom, noting each label, each placeholder, and the small hint under each.
4. Read the label and placeholder on the large multi-line box below them, and the hint under it.
5. Read every control below the multi-line box, from top to bottom, noting each label and hint. Include the **Avatar** panel at the bottom.
6. Read the two buttons at the bottom of the card, left to right.
7. Scan the whole card for a coloured status dot, or any of the words `Online`, `Offline`, `Starting`, `Degraded`.

**Pass if — all of these**

- The header reads `New agent`; the button on its right is a close icon (an ×-shaped Material icon, not a text glyph) and has the tooltip/aria-label `Close`.
- A circular avatar showing a monogram on a randomly coloured background is present at the top left of the card body. It is a button named `Change avatar` (pressing it opens the **Avatar** window), and a small pencil appears on it when you point at it. A quiet text button `Edit avatar…` sits under it and opens the same window.
- The four inputs are, in order: **Name** (placeholder `Chief of Staff`, hint `Letters, digits, spaces, - and _. Spaces are fine.`), **Title** (placeholder `Chief of Staff`, hint `A short role description, shown alongside the Name.`), **Alias** (placeholder `coo`, hint `A short working handle, for mentions and quick reference.`), **Teams** (placeholder `Business, Household`, hint `Comma-separated. Leave blank for no team.`).
- The multi-line box is labelled `Persona body`, has placeholder `You are the Chief of Staff. You keep the team honest.`, and its hint reads `Markdown. Becomes the agent's system prompt, placed after the Name, Title, Alias and Teams above — no front matter needed here.`
- Below the multi-line box the controls are, in this order (`AgentProfileCard.razor`):
  1. a multi-select labelled `Skills` with the hint `Changing Skills restarts this Agent.` (always rendered, `:340`);
  2. a select labelled `Model` whose blank option reads `Agent default`, then a select labelled `Effort` with the same blank option;
  3. a select labelled `Work mode`, only while the probe runs or once the adapter advertises modes (`:471`, `ShowWorkModePicker`); it may disappear when the probe finishes with no modes, which is not a failure;
  No avatar controls appear on the card itself: the radios `Initials of the name`, `A short label` and `An image`, the `Background` palette lives in the **Avatar** window that the avatar button opens, and none of those words is on the card.
  A select labelled `Adapter` appears between the multi-line box and `Skills` only when more than one adapter profile is configured; on a stock install it is absent.
- The buttons read `Cancel` then `Add agent` (the primary, filled action), left to right.
- No status dot and no status word appears anywhere on the card.

**Fail if — any of these**

- The multi-line box is labelled `Persona text` and/or carries the hint `This is the whole Persona file, front matter included` -> the Edit wording leaked into Create; a user following it would type a `---` block that the Create path composes a second time, producing a file that will not load.
- A status dot or an `Offline` label appears in Create mode -> the status block is no longer guarded to the non-Create branch, and it is reporting the health of an agent that does not exist yet.
- Any of the four hints is missing -> the only on-screen guidance for a field with strict validation is gone.

**Inconclusive if**

If the card does not open at all, this test is blocked by AGENTPROFILECARD-02 — record it as blocked, not failed. If a hint is visually truncated rather than absent, widen the browser window before judging.

> [!NOTE]
> The **Name** hint is genuinely incomplete — it never mentions the single-interior-space rule, the must-start-with-a-letter-or-digit rule, or the 64-character cap. Record that as a documentation gap, not a failure of this test; the validation itself is checked in AGENTPROFILECARD-05.

### AGENTPROFILECARD-04 — Monogram tracks the Name as you type

**Free** · about 3 min

*Proves the Name input round-trips to the page and back, and that the monogram takes first-and-last word rather than the first two. The Name box commits on blur, not on every keystroke (`AgentProfileCard.razor:89`, plain `@bind-Value`), so each step leaves the field with Tab before reading the avatar.*

**Before you start**

- The Create card can be opened.

**Steps**

1. Click `New agent` and look at the circular avatar with the **Name** field empty.
2. Type `ada` into **Name**, press Tab, and read the avatar.
3. Clear the field, type `Emily Lee`, press Tab, and read the avatar.
4. Clear the field, type `Chief of Staff`, press Tab, and read the avatar.

**Pass if — all of these**

- Empty Name shows `?`.
- `ada` shows `AD` (a single word gives its first two letters).
- `Emily Lee` shows `EL`.
- `Chief of Staff` shows `CS`.

**Fail if — any of these**

- The avatar stays on `?` after you press Tab out of a non-empty Name -> the Name binding no longer round-trips from the card to the page; every field on the card is bound the same way, so a save would write the wrong (or empty) identity. (An avatar that has not changed before you press Tab is expected, not a failure.)
- `Chief of Staff` shows `CO` -> the monogram takes the first two words instead of the first and last, giving a middle word like `of` a letter it does not deserve.

**Inconclusive if**

If clearing the Name kills the card (see AGENTPROFILECARD-02), do the three positive cases in separate card openings instead of clearing, and record why.

### AGENTPROFILECARD-05 — Create: the exact Name accept/reject set

**Free** · about 15 min

*Proves the Name validation still rejects everything that would produce two agents a reader cannot tell apart, or a folder or filename that escapes the Agents folder.*

**Before you start**

- The Create card can be opened.
- `<data>\Agents` holds only the Chief of Staff's folder.

**Steps**

1. Click `New agent`. Fill **Title** with `T`, **Alias** with `a1`, and the **Persona body** with `x`. Leave **Model** and **Effort** on their blank option.
2. Set **Name** to each of the following in turn and click `Add agent` after each, reading the red error line at the top of the card body and then correcting the Name for the next attempt. REJECT SET, in order: (1) leave empty, (2) ` Nova` (one leading space), (3) `Nova ` (one trailing space), (4) `Emily  Lee` (two spaces between the words), (5) `Nova.1`, (6) `-nova`, (7) `_nova`, (8) `Nova!`, (9) `Ünal`, (10) `AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGGG` (65 characters).
3. After all ten rejects, check File Explorer: `<data>\Agents` must still hold only the `Chief of Staff` folder.
4. Now the ACCEPT set. For each of these, set **Name** to the value, set **Alias** to the matching unique handle, click `Add agent`, confirm the card switches to View mode, close the card, run the Clean-up from Setup step 7 for it, and click `New agent` again for the next one: `Nova` (alias `nv1`), `Emily Lee` (alias `nv2`), `1Nova` (alias `nv3`), `nova-2` (alias `nv4`), `nova_2` (alias `nv5`), `AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGG` (64 characters, alias `nv6`).
5. Confirm `<data>\Agents` holds only the `Chief of Staff` folder again when you finish.

**Pass if — all of these**

- Every one of the ten reject values produces the single red line `'<exactly what you typed>' is not a valid Persona name.` and the card stays open.
- No folder and no `.md` file appears under `<data>\Agents` for any rejected attempt.
- Every one of the six accept values saves successfully and creates a folder named after it holding one `<Name>.md`.

**Fail if — any of these**

- ` Nova`, `Nova ` or `Emily  Lee` is accepted -> a leading, trailing or doubled space now produces two agents a reader cannot visually distinguish, and Windows silently strips a trailing space from a filename so two names would resolve to one file.
- `Nova.1` is accepted -> a dot reached the Name, which becomes the folder name and the filename `{Name}.md`; this rule is the path-traversal guard, so its loss is a security regression, not a cosmetic one.
- `Ünal` or `Nova!` is accepted -> the Name character set widened beyond what Mention matching and filenames can carry.
- The 65-character name is accepted -> the length cap is gone.
- Any accepted value in step 4 is rejected -> the rule tightened; `Emily Lee` in particular must stay legal, a Name is a display name.

**Inconclusive if**

If the card dies on the empty-Name case (AGENTPROFILECARD-02), skip reject case (1), record it as blocked, and run the other nine. If an accept case fails with `Persona 'X' already exists.` you did not remove the previous Persona — run the Clean-up for it and retry that one value.

> [!NOTE]
> Do the whole reject set before the accept set: rejects write nothing, so they need no cleanup between attempts.

### AGENTPROFILECARD-06 — Create: Alias validation uses the same rules and its own wording

**Free** · about 7 min

*Proves an invalid Alias is reported as an alias, not a name — the wording is the only way a user can tell which field failed.*

**Before you start**

- The Create card can be opened.
- `<data>\Agents` holds only the Chief of Staff's folder.

**Steps**

1. Click `New agent`. Set **Name** to `Nova`, **Title** to `T`, **Persona body** to `x`.
2. Set **Alias** to each of these in turn and click `Add agent` after each, reading the red line: leave empty, then ` jar` (leading space), then `jar jar ` (trailing space), then `jar.1`, then `-jar`.
3. Confirm `<data>\Agents` still holds only the `Chief of Staff` folder.
4. Set **Alias** to `jar` and click `Add agent`. Confirm it saves, then close the card and run the Clean-up (Setup step 7).
5. Repeat step 4 for **Alias** `Jar Jar`, then for **Alias** `jar-1`, running the Clean-up each time.

**Pass if — all of these**

- Each of the five invalid aliases produces the single red line `'<exactly what you typed>' is not a valid Persona alias.` — the word is `alias`, not `name`.
- No folder or `.md` file appears under `<data>\Agents` for any rejected attempt.
- `jar`, `Jar Jar` and `jar-1` each save successfully.

**Fail if — any of these**

- The message says `is not a valid Persona name.` while the Name is valid and the Alias is not -> the two guards' wording has been merged; a user is sent to fix the wrong field.
- An empty Alias is accepted -> an Alias is required, and an Alias is accepted anywhere a Name is, so a blank one leaves an agent unmentionable and the frontmatter incomplete.
- A dotted or space-padded alias is accepted -> the Alias no longer shares the Name rules, and `@jar.1` becomes unresolvable at Mention time with no error.

**Inconclusive if**

If the empty-Alias case shows the Name error instead, first re-read the Name field — the Name guard runs first and wins (see AGENTPROFILECARD-07). Only judge this test when the Name is known-valid.

### AGENTPROFILECARD-07 — Create: validation precedence — which error wins

**Free** · about 6 min

*Proves the fixed error order (Name, then Alias, then body, then frontmatter) so a user fixing one problem is never sent to the wrong field.*

**Before you start**

- The Create card can be opened.
- `<data>\Agents` holds only the Chief of Staff's folder.

**Steps**

1. Click `New agent`. Leave **Name**, **Title**, **Alias**, **Teams** and **Persona body** all empty.
2. Click `Add agent` and read the red line.
3. Set **Name** to `Nova`. Click `Add agent` and read the red line.
4. Set **Alias** to `nov`. Click `Add agent` and read the red line.
5. Set **Persona body** to `You are Nova.`, leave **Title** still empty. Click `Add agent` and read the red line.
6. Confirm at every step that only ONE red line is visible at a time, and that `<data>\Agents` still holds only the `Chief of Staff` folder.

**Pass if — all of these**

- Step 2 shows `'' is not a valid Persona name.` and nothing else.
- Step 3 shows `'' is not a valid Persona alias.` and nothing else.
- Step 4 shows `Persona text must not be blank.` and nothing else.
- Step 5 shows `Persona frontmatter is missing required field 'Title'.` and nothing else.
- No file is written at any point.

**Fail if — any of these**

- A later error wins over an earlier one — for example the Title complaint appearing while the Name is still empty -> the guard order moved; the user is asked to fix a field that is not the one blocking the save, and fixing it changes nothing.
- Two or more errors appear stacked -> not a defect by itself (see notes) but record the change; the card was designed to show one.
- An attempt writes a file despite an error -> every guard is supposed to run before anything touches disk.

**Inconclusive if**

If step 2 kills the circuit rather than showing an error, that is AGENTPROFILECARD-02's failure, not this one — start this test from step 3 with a valid Name already typed and note the reduced coverage.

> [!NOTE]
> One error at a time is by design. A tester expecting a per-field error list should NOT file that as a bug — only a wrong error winning is a bug.

### AGENTPROFILECARD-08 — Create: the Title is prose — blank rejected, commas preserved

**Free** · about 6 min

*Proves the Title is validated only for being non-blank and is never split on commas, unlike the Teams field.*

**Before you start**

- The Create card can be opened.
- `<data>\Agents` holds only the Chief of Staff's folder.

**Steps**

1. Click `New agent`. Set **Name** `Nova`, **Alias** `nov`, **Persona body** `You are Nova.`, **Title** empty.
2. Click `Add agent` and read the red line.
3. Set **Title** to three spaces and click `Add agent` again. Read the red line.
4. Set **Title** to exactly `Router, triage, and cross-workstation continuity` and click `Add agent`.
5. Read the second line of the resulting View card (immediately under the name heading).
6. Open `<data>\Agents\Nova\Nova.md` in a text editor and read line 3.
7. Close the card and run the Clean-up for `Nova` (Setup step 7).

**Pass if — all of these**

- Steps 2 and 3 both show `Persona frontmatter is missing required field 'Title'.`
- Step 4 saves successfully.
- The View card's second line reads exactly `Router, triage, and cross-workstation continuity`, commas and all.
- Line 3 of `Nova.md` is exactly `title: 'Router, triage, and cross-workstation continuity'` — one line, single-quoted, commas intact.

**Fail if — any of these**

- A whitespace-only Title is accepted -> a required field has nothing meaningful in it and the agent presents with a blank role line everywhere.
- The Title is split at the commas, rendered as a list, or truncated at the first comma -> the generic frontmatter reader started guessing lists from comma content; only the `teams` key may ever be comma-split, precisely so a prose field survives.
- The Title appears on the card but the file's `title:` line is broken across two lines -> the composed scalar is not quoted and the file will fail to reload.

**Inconclusive if**

If the save is refused with a Name or Alias error, the precedence rule is masking this test — fix those fields first (AGENTPROFILECARD-07) and repeat.

### AGENTPROFILECARD-09 — Create: a failed save never throws away what you typed

**Free** · about 5 min

*Proves an error keeps the card open with every field, both selects and the whole body intact.*

**Before you start**

- The Create card can be opened.

**Steps**

1. Click `New agent`.
2. Set **Name** `Atlas`, **Title** `Ops Lead`, **Teams** `Business, Household`.
3. Type this exact multi-line text into **Persona body**: `You are Atlas.` then a new line, then `You keep the team honest.` then a new line, then `MARKER-9F3A.`
4. If the **Model** select offers options, choose Haiku; if it offers only the blank option, leave it. Do the same for **Effort** with low.
5. Set **Alias** to `jar.1`.
6. Click `Add agent`.
7. Without touching anything else, read every field on the card and note the selected option in both selects.
8. Change **Alias** to `atl` and click `Add agent`.
9. Read the resulting View card's `Persona` block.
10. Close the card and run the Clean-up for `Atlas` (Setup step 7).

**Pass if — all of these**

- After step 6 the card is still open and a red line reads `'jar.1' is not a valid Persona alias.`
- Name, Title, Teams, the full three-line body including `MARKER-9F3A.`, and both select choices are all exactly as left.
- After step 8 the save succeeds and the View card's `Persona` block still contains `MARKER-9F3A.`

**Fail if — any of these**

- The card closes on the error -> the user loses everything typed and has no way to see what was wrong.
- Any field is blanked or the body is truncated -> the failure path is resetting card state instead of returning early; the longer the Persona body, the more expensive this is.
- The Model or Effort select snaps back to the blank option after the error -> a chosen model is silently lost on a validation failure.

**Inconclusive if**

If the Model/Effort selects offer no options at all (no node, or an unauthenticated adapter), skip the select part of the assertion and record that half as unverified — the text fields alone still judge the test.

### AGENTPROFILECARD-10 — Enter in an identity field does not submit, and saving never reloads the page

**Free** · about 4 min

*Proves the identity inputs do not submit on Enter and that saving is a Blazor event, not a page navigation. The card is a `MudDialog` with no `<form>` or `EditForm` in it (`AgentProfileCard.razor:70-564`), so there is nothing for Enter to submit.*

**Before you start**

- The Create card can be opened.

**Steps**

1. Click `New agent`. Fill **Name** `Nova`, **Title** `T`, **Alias** `nov`, **Persona body** `x`.
2. Click into the **Name** input and press Enter. Observe.
3. Repeat in the **Title**, **Alias** and **Teams** inputs.
4. Click into the **Persona body** box, press Enter, and observe.
5. Watch the browser's tab spinner / reload indicator, then click `Add agent`.
6. Close the card and run the Clean-up for `Nova` (Setup step 7).

**Pass if — all of these**

- Pressing Enter in any of the four identity inputs does nothing at all: no save, no error, no navigation.
- Pressing Enter in **Persona body** inserts a newline.
- Clicking `Add agent` saves without the browser showing a page reload.

**Fail if — any of these**

- Enter in the **Name** field submits the form -> a half-filled card can be saved by a stray keypress.
- Clicking `Add agent` reloads the page (spinner, flash, the card gone) -> the form is doing a native POST; the card and everything typed in it are lost on every save.

**Inconclusive if**

If the browser reload indicator is too fast to see, open DevTools > Network, check `Preserve log`, and confirm no document-type request appears when you click `Add agent`.

> [!NOTE]
> Enter not submitting is expected: the card has no form element at all, so Enter in an identity input has nothing to submit. Record it as an ergonomic gap if you like, but it is not a defect.

### AGENTPROFILECARD-11 — A successful create lands on the new agent's View card

**Free** · about 5 min

*Proves a save does not close the card but switches it to View for the agent just written, so the save is visibly confirmed.*

**Before you start**

- The Create card can be opened.
- `<data>\Agents` holds only the Chief of Staff's folder.

**Steps**

1. Click `New agent`. Set **Name** `Nova`, **Title** `Ops Lead`, **Alias** `nov`, **Teams** `Business`, **Persona body** `You are Nova.`
2. Click `Add agent`.
3. Read the card header, the identity block and every section heading in the card body.
4. Look behind the dialog at the list on the page.
5. Confirm `<data>\Agents\Nova\Nova.md` exists in File Explorer.
6. Leave the card and the file in place — AGENTPROFILECARD-12 continues from here.

**Pass if — all of these**

- The card stays open and its header now reads `Agent`.
- The card body shows `Nova` as a heading, `Ops Lead` beneath it, a status dot with a status word, `Alias: nov`, `Teams: Business`, and the sections `Persona`, `Model`, `Effort`, `Work mode`, `Persona file`, in that order (`AgentProfileCard.razor:239-264`).
- Behind the dialog a new tile for `Nova` has appeared under a `Business` heading.
- `<data>\Agents\Nova\Nova.md` exists.

**Fail if — any of these**

- The card closes outright on save -> you cannot confirm what was written without reopening; the create flow gives no receipt.
- The View card shows blank Name/Title/Alias -> the page re-opened the card before refreshing its index, so it could not find the entry the save had just written.
- The file exists but no tile appears -> the list did not refresh off the write; a user would retry and hit `Persona 'Nova' already exists.`

**Inconclusive if**

If the tile appears but the Teams heading reads `No team`, check the Teams input actually contained `Business` — an empty Teams field legitimately groups under `No team` (AGENTPROFILECARD-17).

### AGENTPROFILECARD-12 — View card: every section and action, in order

**Free** · about 7 min

*Proves the View card shows the whole raw file, the real discovered path, and exactly the actions that apply to the agent's current state.*

**Before you start**

- `Nova` exists from AGENTPROFILECARD-11.
- The app is running with `--Team:Acp:Enabled=false`, so every agent reads Offline.

**Steps**

1. If no card is open, click the `Nova` tile.
2. Read the card header.
3. Read the identity block top to bottom: monogram, name, title, status dot and word, any reason line, `Alias:` line, `Teams:` line.
4. Read the row of actions under the identity block, left to right.
5. Read any hint text directly under that row.
6. Read the five section headings and their contents, especially the block under `Persona` and the paragraph under `Work mode`.
7. Compare the text under `Persona` character for character with the contents of `<data>\Agents\Nova\Nova.md` opened in a text editor.
8. Read the path shown under `Persona file`.

**Pass if — all of these**

- The header reads exactly `Agent` — not the agent's name.
- The identity block shows `Nova`, `Ops Lead`, a dot plus the word `Offline`, `Alias: nov`, `Teams: Business`.
- The actions are, in order: `Edit`, `Open`, `Restart`, and **Remove**, which is an icon button (a person with a minus sign, red) with the tooltip and accessible name `Remove`, not a text button. (`Message` is absent — see AGENTPROFILECARD-44.)
- Under the actions the hint reads `Like saving an edit, this restarts the agent, which clears what it remembers.`
- The sections are `Persona`, `Model`, `Effort`, `Work mode`, `Persona file`, in that order; `Work mode` reads `Agent default`.
- The `Persona` block contains the WHOLE file including both `---` delimiter lines and the `name:`/`title:`/`alias:`/`teams:` lines.
- `Persona file` shows the absolute path `<data>\Agents\Nova\Nova.md`, with `<data>` written out in full.

**Fail if — any of these**

- The `Persona` block shows only the body, without the `---` block -> the Edit textarea is fed from the same value, so an Edit-then-Save would strip the frontmatter and the agent would vanish into `Files that didn't load`.
- The header shows `Nova` instead of `Agent` -> the card title binding changed mode.
- Any field renders the literal text `this.cardName` or similar -> a component attribute lost its leading `@` and is being passed as a string literal; the compiler cannot catch this and it has shipped in this file before.
- `Restart` is absent while the status reads `Offline` -> the one action that can fix a stopped agent is unreachable.

**Inconclusive if**

If the status reads `Online`, `Degraded` or `Starting`, the app was started without `--Team:Acp:Enabled=false`; restart it with the setup command and repeat. `Restart` and its hint are shown in every state except `Starting` (`AgentProfileCard.razor:182,234`; AGENTPROFILECARD-45), so an `Online` card still lists them, but a `Starting` card legitimately lacks both.

### AGENTPROFILECARD-13 — View card: 'Agent default' and 'Model default' when nothing is stored

**Free** · about 3 min

*Proves the two unset states use two different, correct words rather than an empty paragraph.*

**Before you start**

- `Nova` exists and was created with both selects left on their blank option.

**Steps**

1. Open `Nova`'s card by clicking its tile.
2. Read the paragraph under the `Model` heading.
3. Read the paragraph under the `Effort` heading.
4. Read the paragraph under the `Work mode` heading.

**Pass if — all of these**

- `Model` reads exactly `Agent default`.
- `Effort` reads exactly `Model default`.
- Neither paragraph is blank.
- `Work mode`, the section below `Effort`, also reads `Agent default`.

**Fail if — any of these**

- Either section is empty -> an unset choice renders as nothing and a user cannot tell 'no choice made' from 'the card failed to load it'.
- Both read the same words -> the distinction is lost; a model is resolved by the agent and an effort by the model, and the two defaults are genuinely different things.

**Inconclusive if**

If `Nova` was created with a Model chosen, delete it and re-create it leaving both selects blank, then repeat.

> [!NOTE]
> The Effort SELECT's blank option is labelled `Agent default` while this View section says `Model default`. That inconsistency is real and worth recording once, but it is a known wording gap, not a failure of this test.

### AGENTPROFILECARD-14 — Closing the dialog: focus trap on open, Escape, the close icon, Cancel, scroll locking, and focus return

**Free** · about 10 min

*The card is now a real `MudDialog` opened through `IDialogService` (Stage 4 of the MudBlazor migration), which brought genuine dialog behaviour the old hand-rolled overlay `<div>` never had. Proves all four: focus moves into the dialog on open, Escape closes it, the page behind does not scroll while it is open, and focus returns to the control that opened it — plus that the close icon and Cancel both still work and a click inside the dialog does not close it.*

**Before you start**

- `Nova` exists.
- The page has enough rooms/agents listed that the page behind the dialog CAN scroll (or narrow the browser window/zoom in until it can).

**Steps**

1. Click the `Nova` tile to open the View dialog. Immediately read `document.activeElement` in DevTools -> Console (or just note which element visibly shows a focus ring).
2. Press Tab repeatedly and watch where the focus ring goes: does it ever land on something BEHIND the dialog (a sidebar link, another tile)?
3. Press Shift+Tab from the first focusable element in the dialog and confirm focus wraps to the LAST focusable element in the dialog, not out of it.
4. With the dialog open, try to scroll the page behind it with the mouse wheel over the dimmed area. Observe whether the page behind moves.
5. Click on the dialog's header text, then on the avatar, then on a hint line — each a click INSIDE the panel. Observe after each: the dialog must stay open.
6. Click the close icon button (the small `×`-style icon at the top right of the header) and observe. Read where keyboard focus lands afterwards — is it back on the `Nova` tile you clicked to open the dialog?
7. Reopen the dialog by clicking the `Nova` tile again, click `Edit`, then click `Cancel`. Observe: does the dialog close and discard, and does focus return to the tile?
8. Reopen the dialog and press the Escape key. Observe whether it closes, and whether focus returns to the `Nova` tile.
9. Reopen the dialog and click the dimmed backdrop area well outside the panel. Observe whether that also closes it.

**Pass if — all of these**

- On open, focus moves INTO the dialog (onto the header, the close icon, or the first field) — never left sitting on the page behind it.
- Tab never lets focus escape the dialog to the page behind while it is open; Shift+Tab from the first item wraps to the last.
- The page behind the dialog does NOT scroll while the dialog is open.
- A click anywhere INSIDE the panel does not close it.
- The close icon closes the dialog; `Escape` closes it; the dimmed backdrop closes it; `Cancel` in Edit closes it and discards.
- After EVERY way of closing the dialog, keyboard focus returns to the `Nova` tile — the control that opened it.

**Fail if — any of these**

- Focus is not moved into the dialog on open, or Tab lets it leak onto the page behind -> the focus trap is not working, which for a screen-reader or keyboard user means the dialog might as well not be modal.
- The page behind visibly scrolls while the dialog is open -> the scroll lock regressed.
- A click inside the panel closes the dialog -> in Edit or Create this destroys everything typed with one misplaced click.
- Escape does not close the dialog -> a MudBlazor default (`CloseOnEscapeKey`) has been turned off or overridden.
- Closing the dialog by any route does not return focus to the tile -> a keyboard user is dropped at the top of the page and has to re-navigate to where they were.
- `Cancel` in Edit saves instead of discarding -> a user backing out writes to disk.

**Inconclusive if**

If your browser or OS has a screen-magnifier or accessibility tool intercepting Tab, verify the trap using DevTools -> Console's `document.activeElement` instead of eyeballing the ring. If focus return is inconsistent only for the backdrop-click close (as opposed to the icon, Escape, and Cancel), record that distinction rather than averaging it into one result.

> [!NOTE]
> All four dialog behaviours above (focus trap, Escape, scroll lock, focus return) were verified working in a browser for this migration — see the "Make the Agent card a real dialog" commit. If any regresses, it is a real defect, not a documented limit; the old area's "Escape not closing and focus not being trapped are BOTH deliberate" note no longer applies and must not be cited.

### AGENTPROFILECARD-15 — Closing a card clears every bit of its state

**Free** · about 7 min

*Proves no field, no error and no pending Remove confirmation survives a close — a leftover Name would be used to create the wrong agent, and a leftover Remove prompt is a one-click accidental delete.*

**Before you start**

- `Nova` exists.
- The Create card can be opened.

**Steps**

1. Click `New agent`. Fill **Name** `Atlas`, **Title** `Ops`, **Alias** `atl`, **Teams** `Business`, **Persona body** `You are Atlas.`; if the selects offer options, choose Haiku and low.
2. Click `Cancel`.
3. Click `New agent` again and inspect all four inputs, the body box, both selects and the top of the card body.
4. Close the card. Click the `Nova` tile, then click the red `Remove` icon button so the prompt `Remove Nova?` with the buttons `Remove` and `Cancel` is showing.
5. Close the card by clicking the dimmed backdrop — do NOT click `Remove` or `Cancel` in the prompt.
6. Click the `Nova` tile again and read the action row.
7. With `Nova`'s card open, click `Edit`, clear the whole text box, click `Save` so a red error appears, then close the card with `×`.
8. Click the `Nova` tile again and look for the red error line.

**Pass if — all of these**

- On reopening Create, all four inputs and the body box are empty, both selects show `Agent default`, and no red error line is showing.
- After reopening `Nova`, the action row shows the single `Remove` icon button — not the `Remove Nova?` prompt.
- After reopening `Nova` following the errored Edit, no red error line is showing.

**Fail if — any of these**

- Any typed value carries over into the next card -> most dangerously a leftover Name, which a Create would then use for a different agent entirely.
- The reopened card still shows the `Remove Nova?` prompt -> one stray click now deletes a Persona file with no further warning.
- A stale error line persists on a freshly opened card -> the user is shown a failure that belongs to a different card.

**Inconclusive if**

If step 7's Save succeeds instead of erroring, you did not fully clear the text box — clear it completely (Ctrl+A, Delete) and retry; an empty Persona text must be refused.

### AGENTPROFILECARD-16 — Remove is a two-step confirm and Cancel backs out cleanly

**Free** · about 5 min

*Proves deleting a Persona file always takes two deliberate clicks and that backing out changes nothing.*

**Before you start**

- `Nova` exists at `<data>\Agents\Nova\Nova.md`.

**Steps**

1. Click the `Nova` tile.
2. Click the red `Remove` icon button (tooltip `Remove`) and read the action row.
3. Click `Cancel` and read the action row again.
4. Confirm in File Explorer that `Nova.md` is still present and its modified time is unchanged.
5. Click the `Remove` icon button, then click `Remove` in the prompt.
6. Observe the card and the list behind it.
7. Confirm in File Explorer that `Nova.md` is gone. The `Nova` folder itself may remain; it is not a Persona and the page ignores it.

**Pass if — all of these**

- Clicking the `Remove` icon replaces it with the prompt `Remove Nova?` and two text buttons, `Remove` (styled as the danger action) and `Cancel`.
- Clicking `Cancel` collapses them back to the single `Remove` icon and the card stays open.
- `Nova.md` is untouched after the Cancel.
- Clicking the prompt's `Remove` closes the card and the `Nova` tile disappears from the list.
- `Nova.md` no longer exists.

**Fail if — any of these**

- The `Remove` icon deletes on a single click -> there is no confirmation step at all on the only destructive action in the app.
- `Cancel` closes the card instead of only cancelling the removal -> a user loses their place and may not realise nothing was deleted.
- The file is gone after the Cancel -> the delete ran before the confirmation.

**Inconclusive if**

If the prompt's `Remove` produces a red line `Persona 'Nova' does not exist.`, the file was removed outside the app between steps — recreate `Nova` and repeat.

> [!NOTE]
> Recreate `Nova` (Name `Nova`, Title `Ops Lead`, Alias `nov`, Teams `Business`, body `You are Nova.`) before continuing to later tests that assume it exists.

### AGENTPROFILECARD-17 — Team headings and the Team filter come from the teams field, not the folder

**Free** · about 10 min

*Proves Team membership is read only from frontmatter, that a two-team agent appears twice, and that selecting a team never spawns an adapter.*

**Before you start**

- The Create card can be opened.
- `<data>\Agents` holds only the Chief of Staff's folder. It has no Team, so it sits under `No team` throughout.
- `T-B` is open for `O-ADAPTERS`.

**Steps**

1. Create three agents from the card, deleting nothing in between: (a) Name `Nova`, Title `T`, Alias `nov`, Teams `Business`, body `x`; (b) Name `Atlas`, Title `T`, Alias `atl`, Teams left EMPTY, body `x`; (c) Name `Iris`, Title `T`, Alias `iri`, Teams `Business, Household`, body `x`.
2. Close the card and read the group headings and which tiles sit under each.
3. Run `O-ADAPTERS` in `T-B` and note the result.
4. Open the **Team** select and read its options.
5. Select `Business`, then run `O-ADAPTERS` again.
6. Select `Household`, then `All teams`, reading the list after each.
7. In a text editor open `<data>\Agents\Nova\Nova.md`, change the line `teams: ['Business']` to `teams: ['Household']`, and save. Leave the folder and the filename alone.
8. Wait about one second, then read the group headings again.
9. Change the line back to `teams: ['Business']` and save. Then, in File Explorer, create the folder `<data>\Agents\Household` and MOVE `Nova.md` into it. Wait about one second and read the page, including any `Files that didn't load` section.

**Pass if — all of these**

- Headings show `Business` containing `Nova` and `Iris`, `Household` containing `Iris`, and `No team` containing `Atlas` and `Chief of Staff`. `Iris` appears once under each of its two teams.
- The **Team** select lists `All teams` plus `Business` and `Household`.
- Selecting a team narrows the page to that one heading; `All teams` restores all of them.
- `O-ADAPTERS` prints the same count before and after changing the filter — no Adapter was spawned.
- After step 7, `Nova` sits under `Household` (with `Iris`) and no longer under `Business`, although its folder is still `Nova`: the heading follows the `teams:` line, nothing else.
- After step 9 (the move), `Nova` disappears from every heading and `Files that didn't load` lists `<data>\Agents\Household\Nova.md` with the reason `An agent folder holds only its definition; use work/.` The file's stem no longer matches its folder, so it is no longer a Persona (`PersonaStore.cs:686-693`, `:773`).

**Fail if — any of these**

- Editing `teams:` in step 7 did not move `Nova` -> the page is not reading Team membership from the frontmatter.
- `Nova` stays on the page after the move in step 9, or is filed under a heading named after its new folder -> grouping is following the folder instead of the `teams:` frontmatter, and a file in the wrong folder is loading when it must be rejected.
- `Iris` appears under only one of its two teams -> a multi-team Persona is being collapsed.
- `Atlas` is missing entirely rather than under `No team` -> an agent with no team has become invisible.
- A node process appears when you change the filter -> the filter is touching the model catalog, spawning an adapter for a pure display change.

**Inconclusive if**

If a team with members shows `No agents in this team`, first confirm the frontmatter really carries that team (open the `.md`); a typo in the Teams input produces a genuinely empty team. `O-ADAPTERS` prints `0` when nothing is running. The Create card's model probe may start a short-lived adapter while the card is open; read `O-ADAPTERS` with every card closed.

> [!NOTE]
> Move `Nova.md` back to `<data>\Agents\Nova\` and delete the `Household` folder before going on. Then run the Clean-up (Setup step 7) for `Nova`, `Atlas` and `Iris`: AGENTPROFILECARD-18 creates `Nova` itself and would fail with `Persona 'Nova' already exists.` if the agent were still there.

### AGENTPROFILECARD-18 — Create: exactly what lands on disk

**Free** · about 8 min

*Proves a created file is written inside its own agent folder as `Agents\{Name}\{Name}.md`, with canonical lowercase single-quoted frontmatter and the body unchanged — and that the Model and Effort never reach the file.*

**Before you start**

- The Create card can be opened.
- `<data>\Agents` holds only the Chief of Staff's folder (no `Nova` folder).

**Steps**

1. Click `New agent`.
2. Set **Name** `Nova`, **Title** `Ops Lead`, **Alias** `nov`, **Teams** `Business`.
3. Type into **Persona body** exactly: `You are Nova.` then a newline then `You keep the team honest.`
4. If the **Model** select offers options, choose Haiku and set **Effort** to low. If it offers only the blank option, leave both blank and note it.
5. Click `Add agent`.
6. In File Explorer, confirm the new file is at `<data>\Agents\Nova\Nova.md` — inside a folder named `Nova`, NOT directly under `Agents`.
7. Open `Nova.md` in a text editor configured to show line endings (in VS Code, the indicator at the bottom right).
8. Read all eight lines and the line-ending indicator.
9. Search the file for the words `haiku`, `model` and `effort`.

**Pass if — all of these**

- The file is at `<data>\Agents\Nova\Nova.md`, and no `Nova.md` sits directly under `Agents` (`AgentPaths.cs:44,57`; `PersonaStore.cs:367,377`).
- Its contents are exactly, in order: `---`, `name: 'Nova'`, `title: 'Ops Lead'`, `alias: 'nov'`, `teams: ['Business']`, `---`, `You are Nova.`, `You keep the team honest.`
- Every frontmatter key is lowercase; every scalar is single-quoted; `teams` is a bracketed flow list.
- The editor reports LF line endings.
- The file contains no `model:` or `effort:` line and no model id.

**Fail if — any of these**

- The file is named anything other than `Nova.md`, or sits anywhere but `Agents\Nova\` -> the app guessed a location for a new file; the path must always be `Agents\{Name}\{Name}.md`.
- A frontmatter key is capitalised or a scalar is unquoted -> the composed file no longer matches the shape the reader accepts; the next edit may fail to reload it.
- The chosen Model or Effort appears in the file -> those belong only in `team.db` (`persona_models`, `persona_efforts`); a file carrying them will drift from the database and the two will disagree.
- The body is reordered, re-indented or missing a line -> the body is supposed to be written back verbatim.

**Inconclusive if**

If the Model select offers no options (no node, or the adapter needs authentication), the model-not-in-the-file assertion is untestable this run — record it as unverified and check AGENTPROFILECARD-40 instead once a catalog is available.

> [!NOTE]
> CRLF here would be a real difference: the create path joins its lines with LF (`PersonaFrontmatter.cs:246`). Remove `Nova` (Setup step 7) before AGENTPROFILECARD-19.

### AGENTPROFILECARD-19 — Create: an apostrophe in the Title round-trips through YAML quoting

**Free** · about 5 min

*Proves the YAML escape is written on disk and stripped on screen, so a natural Title neither breaks the file nor leaks its escaping into the UI.*

**Before you start**

- The Create card can be opened.
- `<data>\Agents` has no `Nova` folder.

**Steps**

1. Click `New agent`.
2. Set **Name** `Nova`, **Alias** `nov`, **Persona body** `x`.
3. Set **Title** to exactly `Chief of Staff's deputy` (one apostrophe).
4. Click `Add agent`.
5. Read the line directly under the name heading on the resulting View card.
6. Open `<data>\Agents\Nova\Nova.md` and read line 3 exactly.
7. Reload the browser page, click the `Nova` tile and re-read the title line.
8. Close the card and remove `Nova` (Setup step 7).

**Pass if — all of these**

- The save succeeds with no error.
- The View card's second line reads exactly `Chief of Staff's deputy` — one apostrophe.
- Line 3 of the file is exactly `title: 'Chief of Staff''s deputy'` — TWO apostrophes on disk.
- After a full page reload the card still shows one apostrophe.

**Fail if — any of these**

- The card shows `Chief of Staff''s deputy` -> the YAML escape is leaking into the UI; the reader is not un-doubling what the writer doubled.
- The save fails with a frontmatter error -> the escape was not written, so the quoted scalar terminated early and the rest of the line was lost.
- The file shows a single apostrophe inside the quotes -> the file is invalid YAML and will be rejected on the next reload, dropping the agent into `Files that didn't load`.

**Inconclusive if**

If your text editor auto-converts the apostrophe to a typographic quote (’), that is a different character and this test proves nothing — type it into the browser field, not into the file, and verify the field contains U+0027.

### AGENTPROFILECARD-20 — Create: the Teams field trims, drops blanks and collapses duplicates on read-back

**Free** · about 8 min

*Proves the comma parsing, and records the deliberate asymmetry where the file keeps a duplicate team but the screen shows one.*

**Before you start**

- The Create card can be opened.
- `<data>\Agents` has no `Nova`, `Atlas` or `Iris` folder.

**Steps**

1. Create an agent: Name `Nova`, Title `T`, Alias `nov`, body `x`, **Teams** set to exactly ` Business ,, Household , ` (note leading, doubled and trailing commas and spaces).
2. Read the `Teams:` line on the resulting View card, then open `<data>\Agents\Nova\Nova.md` and read its `teams:` line.
3. Close the card. Create a second agent: Name `Atlas`, Title `T`, Alias `atl`, body `x`, **Teams** set to exactly `Ops, ops`.
4. Read the `Teams:` line on Atlas's View card, then read `Atlas\Atlas.md`'s `teams:` line.
5. Close the card. Create a third agent: Name `Iris`, Title `T`, Alias `iri`, body `x`, **Teams** left EMPTY.
6. Read Iris's View card and look for a `Teams:` line, then open `Iris\Iris.md` and look for a `teams:` line.
7. Remove all three agents and delete their three folders (Setup step 7).

**Pass if — all of these**

- Nova's card reads `Teams: Business, Household`; `Nova.md` contains `teams: ['Business', 'Household']`.
- Atlas's card reads `Teams: Ops` (one entry); `Atlas.md` contains `teams: ['Ops', 'ops']` (both).
- Iris's card shows NO `Teams:` line at all, and `Iris.md` contains no `teams:` line whatsoever.

**Fail if — any of these**

- Nova's file or card keeps an empty entry, a leading space or a trailing blank -> the trim/drop-blank parsing regressed and a phantom team name will appear in the filter.
- Iris's card shows an empty `Teams:` line, or her file contains `teams: []` -> an empty list is being written and rendered instead of omitted; that is not how a person writes 'no teams' by hand, and the reader treats the two differently.
- Atlas's card shows `Ops, ops` twice -> the read-side de-duplication is gone and the same team is listed twice on screen.

**Inconclusive if**

If a team name you typed contains a comma, this test cannot judge it — a Team name can never contain a comma by design. Retype without one.

> [!NOTE]
> The `Ops, ops` asymmetry is deliberate: the write side does not de-duplicate, the read side does, case-insensitively. File and screen legitimately disagree here. Do NOT file it.

### AGENTPROFILECARD-21 — Create: a duplicate filename is caught before anything is written

**Free** · about 6 min

*Proves an existing Persona file can never be silently overwritten, including by a name differing only in case.*

**Before you start**

- `<data>\Agents\Nova\Nova.md` exists (create it if needed: Name `Nova`, Title `Ops Lead`, Alias `nov`, body `You are Nova.`).

**Steps**

1. Note `Nova.md`'s exact modified timestamp and its contents in File Explorer / a text editor.
2. Click `New agent`. Set **Name** `Nova`, **Title** `Different`, **Alias** `zzz`, body `Different body.`
3. Click `Add agent` and read the red line.
4. Without closing the card, change **Name** to `nova` (all lowercase) and click `Add agent` again. Read the red line.
5. Confirm the card is still open and every field still holds what you typed.
6. Check `Nova.md`'s modified timestamp and contents again.
7. Check whether any second Persona appeared under `<data>\Agents` (a `nova` folder or a second `Nova.md`).

**Pass if — all of these**

- Both attempts show `Persona 'Nova' already exists.` / `Persona 'nova' already exists.` naming exactly the name you typed (`PersonaStore.cs:367-370`).
- The card stays open with all fields intact.
- `Nova.md`'s modified timestamp and contents are unchanged.
- No second folder or file appeared.

**Fail if — any of these**

- `Nova.md`'s contents changed -> an existing agent's Persona was silently overwritten; this is catastrophic, unrecoverable data loss with no warning.
- The lowercase `nova` attempt succeeds and a second file appears -> on Windows those two paths are the same file, so one of them will win non-deterministically.
- The message names a different name than you typed -> the error is reporting the wrong thing and a user cannot tell which attempt collided.

**Inconclusive if**

On a case-sensitive filesystem the lowercase attempt would legitimately produce the collision message from the index instead (`Persona Name 'nova' is also used by ...`). This test runs on Windows, so treat any non-`already exists` message as a finding and record the exact text.

### AGENTPROFILECARD-22 — Edit card: what is editable, and what a rename announces

**Free** · about 6 min

*Proves the Edit card pre-fills a raw whole-file editor, correctly labelled, beside Name, Title and Alias boxes that write through to it, and that changing the Name announces what a rename carries.*

**Before you start**

- `Nova` exists at `<data>\Agents\Nova\Nova.md`, with Title `Ops Lead` and Alias `nov`.

**Steps**

1. Click the `Nova` tile, note the path under `Persona file`, then click `Edit`.
2. Read the card header.
3. Read the identity area: which boxes there are, their labels and their helper text.
4. Read the label of the large text box, and the hint under it.
5. Select all the text in the large box and compare it character for character with the file at the noted path (paste into a diff tool if needed).
6. Scroll down and read every control below the large box, in order, and the buttons at the bottom.
7. Change the **Title** box to `Ops Chief` and press Tab. Read the `title:` line in the large box.
8. Change the **Name** box to `Aria` and press Tab. Read the header, the large box's `name:` line, and whether a note appears under the identity boxes.
9. Change the **Name** box back to `Nova` and press Tab. Read whether the note is still there.
10. Click `Cancel`, and check that `Nova.md` on disk is unchanged.

**Pass if — all of these**

- The header reads `Edit Nova`.
- The identity area shows three text boxes labelled `Name`, `Title` and `Alias`, holding `Nova`, `Ops Lead` and `nov`, with the helper texts `Letters, digits, spaces, - and _. Spaces are fine.`, `A short role description, shown alongside the Name.` and `A short working handle, for mentions and quick reference.`
- The large box is labelled `Persona text`, and its hint reads `Markdown. This is the whole Persona file, front matter included, and becomes the agent's system prompt. Saving restarts it, which clears what it remembers.`
- The box contains the WHOLE file — both `---` lines and every frontmatter key — identical to the file on disk.
- Below the large box, in order: **Skills**, **Model**, **Effort**, **Work mode** (once its list has loaded), the avatar panel, then the buttons `Cancel` and `Save`. An **Adapter** select sits between the large box and Skills only when more than one adapter profile exists.
- After step 7 the large box's `title:` line reads `Ops Chief`.
- After step 8 the header reads `Edit Aria`, the large box's `name:` line reads `Aria`, and a note appears reading `Renaming brings this agent's chats, its history and its work folder with it. Messages already posted keep the old name — the transcript records what was said at the time.`
- After step 9 the note is gone.
- After Cancel, `Nova.md` on disk is unchanged.

**Fail if — any of these**

- The text box contains only the body without the `---` block -> saving would strip the frontmatter and the agent would immediately drop into `Files that didn't load`, recoverable only in a text editor.
- Editing a box leaves the large box unchanged -> the two can disagree, and one of them has to lose silently on Save.
- The rename note is missing after step 8, or says the Agent, Rooms or Transcripts stay behind -> the card is describing the pre-ADR-0011 behaviour.
- The box is labelled `Persona body` with the Create hint -> the user is told not to include front matter while editing a file that consists largely of it.

**Inconclusive if**

If the text box is empty, check the agent still exists on disk — a file deleted underneath an open card produces an empty Edit (see AGENTPROFILECARD-31). Recreate it and repeat.

> [!NOTE]
> The Name box is a rename control (ADR-0011); AGENTPROFILECARD-26 saves one. A box shows only a helper line saying it "can only be edited in the Persona text below" when its value is a multi-line YAML block; that is deliberate and does not arise for a card created here.

### AGENTPROFILECARD-23 — Edit save writes back to the SAME file path — never a rename, never a move

**Free** · about 10 min

*Proves an edit rewrites the file at the path it was discovered at, even when the Name inside does not match the folder it sits in — and records the LF normalisation.*

**Before you start**

- `<data>\Agents` holds no `Nova` or `Ops` folder.
- You can create files and folders under `<data>\Agents`.

**Steps**

1. Stop nothing — the app stays running. In File Explorer create the folder `<data>\Agents\Ops` and, inside it, a file `Ops.md`.
2. Put exactly this in `Ops.md`, saved with CRLF line endings: `---`, `name: 'Nova'`, `title: 'Ops Lead'`, `alias: 'nov'`, `teams: ['Business']`, `---`, `You are Nova. ORIGINALWORD.`
3. Wait about one second, then confirm a `Nova` tile appears under the `Business` heading at http://localhost:5100/agents , and that `Files that didn't load` does not list `Ops.md`.
4. Click the tile and confirm `Persona file` shows `...\Agents\Ops\Ops.md`.
5. Click `Edit`, change `ORIGINALWORD` to `CHANGEDWORD`, and click `Save`.
6. Confirm the card lands in View mode showing `CHANGEDWORD` in the `Persona` block.
7. In File Explorer, list every `.md` file under `<data>\Agents` at every depth, and every folder directly under it.
8. Open `Ops\Ops.md` and confirm the new word and the file's line endings.

**Pass if — all of these**

- `Ops\Ops.md` now contains `CHANGEDWORD` and has a fresh modified time.
- No `Nova` folder and no file named `Nova.md` was created anywhere (`PersonaStore.cs:436-438` writes to the discovered path).
- The set of folders and `.md` files is exactly what it was before the save: the Chief of Staff's, and `Ops\Ops.md`. Nothing was renamed, moved or duplicated, because the Name did not change.
- The card's `Persona file` still shows the `Agents\Ops\Ops.md` path.

**Fail if — any of these**

- A new `<data>\Agents\Nova\Nova.md` appeared -> the save reconstructed a path from the Name instead of using the discovered one; the original file is now an orphan and the two will collide on the next scan, sending BOTH into `Files that didn't load`.
- The `Ops` folder or `Ops.md` was moved or renamed -> an edit that did not change the Name rearranged a user's own filing system.
- Both the original and a new file exist with the same `name:` -> the next index rebuild rejects both and the agent disappears from the list entirely.

**Inconclusive if**

If the tile never appears in step 3, read `Files that didn't load` for the reason and recheck that the file is named `Ops.md` inside `Ops` (the stem must match the folder); the watcher itself is AGENTPROFILECARD-30's subject.

> [!NOTE]
> EXPECTED, NOT A DEFECT: after the save the file's line endings are LF, not the CRLF you wrote. The browser hands the textarea back with LF and the save writes it verbatim. Record it so nobody files it. Content loss or doubled blank lines WOULD be a defect. A Persona whose folder is named differently from its `name:` loads silently — nothing on the page flags it (`PersonaStore.FolderWarnings` has no reader in `Huddle.App`). Remove `Nova`, then delete the `Ops` folder, before going on.

### AGENTPROFILECARD-24 — Edit: breaking the frontmatter is refused with the file untouched

**Free** · about 12 min

*Proves every frontmatter failure is named precisely and none of them reaches disk — a written-through bad edit would drop the agent out of the app entirely.*

**Before you start**

- An agent exists (for example `Nova` at `<data>\Agents\Nova\Nova.md`).
- You can see the file's modified timestamp.

**Steps**

1. Note the Persona file's exact modified timestamp.
2. Open the agent's card and click `Edit`.
3. Delete the whole `title:` line from the textarea and click `Save`. Read the red line, then restore the line.
4. Delete both `---` lines and every frontmatter line, leaving only the body. Click `Save`, read the red line, then undo (Ctrl+Z) back to the full file.
5. Duplicate the `alias:` line so it appears twice. Click `Save`, read the red line, then remove the duplicate.
6. Change the `name:` value to `'Emily  Lee'` (two spaces between the words). Click `Save`, read the red line, then restore the original name.
7. Select all the text and delete it so the box is empty. Click `Save` and read the red line.
8. After each of the five attempts, confirm the card is still open with your text still in it.
9. Check the file's modified timestamp and contents on disk.

**Pass if — all of these**

- Missing title -> `Persona frontmatter is missing required field 'Title'.`
- No frontmatter at all -> `Persona frontmatter is missing required field 'Name'.`
- Duplicate alias -> `Persona frontmatter has a duplicate 'Alias' field.`
- `name: 'Emily  Lee'` -> `Persona frontmatter field 'Name' has an invalid value: 'Emily  Lee'.`
- Empty textarea -> `Persona text must not be blank.`
- The card stays open with the typed text after every attempt.
- The file's modified timestamp and contents are unchanged throughout.

**Fail if — any of these**

- Any rejected save changed the file on disk -> the agent now fails to load, disappears from the list into `Files that didn't load`, and is only recoverable by hand in a text editor.
- A generic message such as `Save failed` that does not name the offending field -> the user has a whole file to search and no clue where.
- A duplicate key is accepted with a silent last-one-wins -> a repeat can only be an authoring mistake and must never resolve quietly.

**Inconclusive if**

If the modified timestamp changes but the contents are identical, your editor or an antivirus scanner may have touched the file — compare contents, which is the real oracle, and note the timestamp discrepancy.

> [!NOTE]
> Restore the file to its original content before moving on.

### AGENTPROFILECARD-25 — Edit: renaming into a collision is refused with the file untouched

**Free** · about 8 min

*Proves a Name or Alias edited into another file's identity is rejected before the write — a write-through would knock BOTH agents out of the app at once.*

**Before you start**

- Two agents exist besides the Chief of Staff: `Nova` (alias `nov`) and `Atlas` (alias `atl`). Create them from the card if needed.
- You know both files' paths (`<data>\Agents\Nova\Nova.md`, `<data>\Agents\Atlas\Atlas.md`) and modified timestamps.

**Steps**

1. Note `Atlas.md`'s modified timestamp and contents.
2. Open `Atlas`'s card and click `Edit`.
3. Change the `name:` line to `name: 'Nova'`. Click `Save` and read the red line.
4. Change it to `name: 'nova'` (lowercase). Click `Save` and read the red line.
5. Restore `name: 'Atlas'`. Change the `alias:` line to `alias: 'nov'`. Click `Save` and read the red line.
6. Change it to `alias: 'Nova'` (the other file's NAME). Click `Save` and read the red line.
7. Confirm the card is still open with your text intact after every attempt.
8. Check `Atlas.md` on disk, and confirm both `Nova` and `Atlas` tiles are still in the list behind the card.

**Pass if — all of these**

- Steps 3 and 4 both show `Persona Name 'Nova' is also used by '<data>\Agents\Nova\Nova.md'.` / `Persona Name 'nova' is also used by '<same path>'.` — the comparison is case-insensitive.
- Step 5 shows `Persona Alias 'nov' is also used by '<data>\Agents\Nova\Nova.md'.`
- Step 6 shows `Persona Alias 'Nova' matches the Name used by '<data>\Agents\Nova\Nova.md'.`
- `Atlas.md` is unchanged on disk, no folder was renamed, and both tiles remain in the list.

**Fail if — any of these**

- Any attempt writes to disk -> BOTH files now claim the same identity, so the index rejects BOTH; two agents vanish from the list into `Files that didn't load` and the only recovery is a text editor.
- The save succeeds and the list shows two tiles both named `Nova` -> the pre-write validation stopped running; from here `@nov` is ambiguous with no error at Mention time.
- The message names no path -> the user cannot find the other file that is causing the collision.

**Inconclusive if**

If the red line names a path you do not recognise, a third Persona file exists somewhere under `Agents` — list every `.md` at every depth before judging.

### AGENTPROFILECARD-26 — Edit: renaming moves the folder and keeps the agent whole (ADR-0011)

**Free** · about 12 min

*Proves a rename saved from the card carries the whole agent with it: the card lands on the new Name, the folder, the file and the Work Dir move, the stored Model and Effort follow the Name, and the `users` row keeps its id.*

**Before you start**

- An agent `Nova` exists with a stored Model and Effort. If no catalog is available, seed them directly: `sqlite3 "<data>\team.db" "INSERT OR REPLACE INTO persona_models(persona_name,model) VALUES('Nova','claude-haiku-test'); INSERT OR REPLACE INTO persona_efforts(persona_name,effort) VALUES('Nova','low');"`
- `sqlite3` is available (`O-DB`).
- `<data>\Agents` has no `Aria` folder.

**Steps**

1. Run `sqlite3 "<data>\team.db" "SELECT id,name FROM users WHERE kind='agent'; SELECT * FROM persona_models; SELECT * FROM persona_efforts;"` and record the rows, in particular `Nova`'s `users` id.
2. Note `Nova`'s file path. In File Explorer create the file `<data>\Agents\Nova\work\marker.txt` (create `work` if it is missing) with any text.
3. Open `Nova`'s card, click `Edit`, change the **Name** box to `Aria`, press Tab, and click `Save`.
4. WITHOUT closing the card, write down what it now shows: its header, whether the `Persona` block has content, the Title, whether an `Alias:` line is present, what `Model` and `Effort` read, and the `Persona file` path.
5. Wait about five seconds. Close the card and look at the list behind it.
6. Click the `Aria` tile. Read every section, including the `Persona file` path.
7. In File Explorer, list the folders under `<data>\Agents`, then open `Aria\Aria.md` and read its `name:` line, and check `Aria\work\marker.txt`.
8. Re-run the sqlite query from step 1.

**Pass if — all of these**

- Right after the save the card is in View mode with the header `Aria`, the full `Persona` block, the Title and Alias, and the same `Model` and `Effort` as before — a complete card, not a blanked one (`AgentProfileCard.razor:1864-1885`).
- The list shows a tile named `Aria` and no `Nova`, and no `Files that didn't load` section.
- The folder is `<data>\Agents\Aria`, there is no `Nova` folder, the file inside is `Aria.md` and its `name:` line reads `'Aria'` (`PersonaRenameCascade.cs:353-402`).
- `Aria\work\marker.txt` exists: the Work Dir moved with the folder.
- The `users` row keeps its id and now has the name `Aria`.
- The `persona_models` and `persona_efforts` rows have MOVED from `Nova` to `Aria` — same values, new key, and no row left under `Nova` (`PersonaStore.cs:440-449`).
- The reopened `Aria` card shows the path `...\Agents\Aria\Aria.md`.

**Fail if — any of these**

- The `persona_models` or `persona_efforts` row for `Nova` is gone with nothing written under `Aria` -> the rename silently DROPPED the agent's Model and Effort; next start it thinks with a model nobody chose.
- A row survives under `Nova` as well as `Aria` -> a future agent named `Nova` would silently inherit a setting nobody chose for it.
- The `users` row has a new id, or a second agent row named `Aria` exists -> the Rooms and Transcripts were left with a stranger.
- The folder is still `Nova`, or the file is still `Nova.md` -> the cascade did not run, and the next scan files `Aria` under a mismatched folder.
- No `Aria` tile appears at all -> the save did not take; check the file before concluding.

**Inconclusive if**

If `sqlite3` is unavailable, the Model/Effort/`users` half cannot be judged — run only the on-screen and folder half and record the database half as unverified. Note that `team.db` is in WAL mode, so read it while the app runs rather than copying the file. If `<data>\Agents\Aria` already existed, the move is skipped and the log reads `Did not move the Agent folder for 'Nova' to 'Aria': a directory already exists at the target. Both were left in place.` (`PersonaRenameCascade.cs:451`); delete the stray folder and repeat.

> [!NOTE]
> The folder move runs after the save, detached, and retries while the old session still holds the folder (`PersonaRenameCascade.cs:209-210,390-410`). So the `Persona file` line on the card the save lands on may still show the old `Nova` path for a moment; judge the path after reopening. If `Nova` had a direct Room, its auto-derived name follows the rename too (`PersonaRenameCascade.cs:342`). Messages already posted keep the old name by design, so do NOT file the old name in an old Transcript. Remove `Aria` afterwards.

### AGENTPROFILECARD-27 — Create: a Name colliding with a Persona whose folder has another name

**Free** · about 8 min

*Proves the collision check compares the `name:` inside every loaded file, not folder names, and reports the colliding file's real path, instead of writing a second file that would knock both out.*

**Before you start**

- `<data>\Agents` holds no `Nova` or `Ops` folder.
- You can create folders under `<data>\Agents`.

**Steps**

1. Create `<data>\Agents\Ops\Ops.md` containing: `---`, `name: 'Nova'`, `title: 'Ops Lead'`, `alias: 'nov'`, `teams: ['Business']`, `---`, `You are Nova.`
2. Wait about one second and confirm a `Nova` tile appears under the `Business` heading.
3. Click `New agent`. Set **Name** `Nova`, **Title** `T`, **Alias** `zzz`, body `x`. Click `Add agent` and read the red line.
4. Change **Name** to `nova` (lowercase) and click `Add agent` again. Read the red line.
5. Check whether a `Nova` or `nova` folder exists under `<data>\Agents`.
6. Close the card and confirm exactly one `Nova` tile is in the list, with no `Files that didn't load` section.

**Pass if — all of these**

- Both attempts show `Persona Name 'Nova' is also used by '<data>\Agents\Ops\Ops.md'.` (or the same message with `'nova'`), naming the real absolute path.
- No `Nova` folder or file was created (the file-exists check at `PersonaStore.cs:367-370` does not fire, because `Agents\Nova\Nova.md` is absent; the index check does).
- Exactly one `Nova` tile remains and no rejected-files section appears.

**Fail if — any of these**

- No error and the save succeeds -> the pre-write validation compares only folder names; two files now claim `Nova`, so the next index rebuild rejects BOTH and the agent disappears entirely.
- The lowercase `nova` attempt succeeds -> the comparison became case-sensitive; `Jarvis` and `jarvis` must collide, matching the case-insensitive key the Model/Effort tables use.
- The message names a reconstructed path such as `...\Agents\Nova\Nova.md` rather than the real discovered one -> the user is sent to a file that does not exist.

**Inconclusive if**

If no `Nova` tile appears in step 2, the file did not load — read `Files that didn't load` for the reason and check that the file is `Ops\Ops.md` before judging this test; without the existing Persona there is nothing to collide with.

> [!NOTE]
> Delete the `Ops` folder afterwards (remove `Nova` from its card first) unless AGENTPROFILECARD-28 reuses it.

### AGENTPROFILECARD-28 — Create: Alias collisions, and the one that is legal

**Free** · about 8 min

*Proves a duplicate Alias and an Alias equal to another file's Name are both refused, while an Alias equal to its OWN Name is accepted.*

**Before you start**

- `Nova` (alias `nov`) exists besides the Chief of Staff (alias `cos`), at `<data>\Agents\Nova\Nova.md`. No other agent exists.

**Steps**

1. Click `New agent`. Set **Name** `Atlas`, **Title** `T`, body `x`, **Alias** `nov`. Click `Add agent` and read the red line.
2. Change **Alias** to `NOV` (uppercase) and click `Add agent` again. Read the red line.
3. Change **Alias** to `Nova` (the other file's NAME) and click `Add agent`. Read the red line.
4. Change **Alias** to `Atlas` (its own Name) and click `Add agent`.
5. Confirm what happened, then open `<data>\Agents\Atlas\Atlas.md` and read its `alias:` line.
6. Confirm `Nova.md` is untouched throughout.
7. Remove `Atlas` afterwards (Setup step 7).

**Pass if — all of these**

- Steps 1 and 2 both show `Persona Alias 'nov' is also used by '<data>\Agents\Nova\Nova.md'.` / `... 'NOV' ...` — case-insensitive.
- Step 3 shows `Persona Alias 'Nova' matches the Name used by '<data>\Agents\Nova\Nova.md'.`
- Step 4 SUCCEEDS with no error, and `Atlas.md` contains `alias: 'Atlas'`.
- `Nova`'s file is unchanged after steps 1-3 and no folder was created for them.

**Fail if — any of these**

- Step 4 is rejected -> an Alias equal to its own Name is an explicitly supported authoring choice and has been broken.
- Steps 1-3 are accepted -> an Alias is accepted anywhere a Name is, so `@nov` becomes ambiguous with no error at Mention time and the wrong agent may answer.
- The uppercase `NOV` is accepted while `nov` is rejected -> the comparison became case-sensitive.

**Inconclusive if**

If an agent other than `Nova` and the Chief of Staff exists at the start, the error may name a different file than expected — remove the extra agents and repeat.

### AGENTPROFILECARD-29 — 'Files that didn't load' names the path and the reason, above the list

**Free** · about 8 min

*Proves a file that fails to become a Persona is impossible to miss, names itself and says why — and that BOTH sides of a collision are rejected, never one silently winning.*

**Before you start**

- `<data>\Agents` holds only the Chief of Staff's folder.
- The Agents page is open.

**Steps**

1. Create `<data>\Agents\Broken\Broken.md` containing exactly: `---`, `name: 'X'`, `---`, `Some body text.` (no title, no alias).
2. Wait about one second and read the top of the Agents page.
3. Note whether the failing section appears ABOVE or BELOW the agent list, and whether any tile was rendered for `Broken.md`.
4. Now create TWO files, `<data>\Agents\TwinA\TwinA.md` and `<data>\Agents\TwinB\TwinB.md`, each containing: `---`, `name: 'Twin'`, `title: 'T'`, plus a DIFFERENT alias line (`alias: 'ta'` in one, `alias: 'tb'` in the other), `---`, `body`.
5. Wait about one second and read the page again.
6. Create `<data>\Agents\stray.md` (any text) directly under `Agents`, and `<data>\Agents\Broken\second.md` (any text). Wait about one second and read the page again.
7. Delete the `Broken`, `TwinA` and `TwinB` folders and `stray.md`.

**Pass if — all of these**

- A section headed `Files that didn't load` appears ABOVE the agent list.
- It lists `Broken\Broken.md`'s real absolute path with the reason `Persona frontmatter is missing required field 'Title'.`
- No tile is rendered for `Broken.md`.
- Both `TwinA\TwinA.md` and `TwinB\TwinB.md` appear in the section, each naming the OTHER's absolute path (`Persona Name 'Twin' is also used by '<other path>'.`), and NEITHER gets a tile.
- After step 6, `stray.md` is listed with the reason `A definition must be inside its agent's folder.` and `Broken\second.md` with `An agent folder holds only its definition; use work/.` (`PersonaStore.cs:719,773`).

**Fail if — any of these**

- One of the twins gets a tile and the other is rejected -> a winner is being picked by enumeration order; editing the loser then does nothing at all, with no feedback anywhere.
- A bad file is silently absent with nothing in this section -> a user's file has vanished from the app with no explanation.
- The section renders below the list -> its whole purpose is to be impossible to miss.
- The listed path is reconstructed or relative rather than the real absolute path -> the user cannot find the file to fix it.

**Inconclusive if**

If nothing appears after about two seconds, press F5 once. If the section only appears after a manual reload, that is a watcher finding — record it under AGENTPROFILECARD-30, and judge this test on the post-reload content.

### AGENTPROFILECARD-30 — An external file edit repaints the page — including inside agent folders

**Free** · about 12 min

*Proves the file watcher sees the whole tree, including edits, folder renames, moves and deletes one level down, which fail silently when broken; and records that an open View card does not follow.*

**Before you start**

- The Agents page is open with NO card open.
- `Nova` (`<data>\Agents\Nova\Nova.md`) and `Atlas` (`<data>\Agents\Atlas\Atlas.md`) exist, besides the Chief of Staff.

**Steps**

1. With no card open, edit `Nova.md`'s `title:` line in a text editor to `title: 'Nova Changed'` and save. Watch the tile's second line, without touching the browser. Time roughly how long it takes.
2. Edit the Chief of Staff's file the same way (`<data>\Agents\Chief of Staff\Chief of Staff.md`, `title: 'CoS Changed'`). Watch its tile, then change it back.
3. Rename the folder `<data>\Agents\Atlas` to `<data>\Agents\AtlasOps`. Watch the page.
4. Rename it back to `Atlas`. Watch the page.
5. Move `Nova.md` out of its folder into `<data>\Agents` itself. Watch the page, then move it back.
6. Delete the `Atlas` folder outright. Watch the page.
7. Now open an agent's View card and LEAVE IT OPEN. In the editor, change that same agent's `title:` and save. Watch both the tile behind the dialog and the card itself.
8. Read the app console for any line mentioning `FileSystemWatcher`.

**Pass if — all of these**

- Both title edits repaint the tile within roughly half a second to a second (the debounce is 500 ms, `PersonaStore.cs:67`), with no browser reload.
- Step 3: the `Atlas` tile disappears and `Files that didn't load` lists `...\AtlasOps\Atlas.md` with the reason `An agent folder holds only its definition; use work/.` Step 4: the tile returns and the section goes. No agent is lost.
- Step 5: `Nova` disappears and `Files that didn't load` lists the file under `Agents` with the reason `A definition must be inside its agent's folder.`; moving it back restores the tile.
- Deleting the folder removes the `Atlas` tile within about a second.
- With a card open, the TILE behind updates.

**Fail if — any of these**

- A title edit inside an agent folder produces no repaint at all -> the watcher is not including subdirectories (`PersonaStore.cs:164-173`); this fails silently, with no error and no log line, and every Persona serves stale prompt text forever.
- A folder rename, move or delete produces no repaint -> the watcher event is named after the directory and never matches `*.md`; every Persona path under it is now stale.
- Renaming the folder leaves the `Atlas` tile on the page -> the old index is still being served after the file's stem stopped matching its folder.
- A `PersonaStore's FileSystemWatcher reported an error` line appears with NO repaint following it -> the overflow backstop did not schedule its refresh.

**Inconclusive if**

Some editors write via a temp file plus a rename, which can produce a different event shape. If a repaint does not happen, repeat the edit with a plain `Add-Content` from PowerShell before concluding the watcher is broken.

> [!NOTE]
> EXPECTED ROUGH EDGE, record don't file: with a View card open, the card shows the OLD Persona block, Title, Alias, Teams and Room link until you close and reopen it. It never subscribes to the store's change event, and it reads the Room once when it opens (`AgentProfileCard.razor:785`). Judge it as a quirk, not data loss.

### AGENTPROFILECARD-31 — A card whose file vanished underneath it reports it instead of crashing

**Free** · about 6 min

*Proves every card action on a stale card produces a clear message rather than an unhandled exception or a silent no-op.*

**Before you start**

- An agent `Nova` exists, at `<data>\Agents\Nova\Nova.md`.
- The app console is visible.

**Steps**

1. Click the `Nova` tile to open its View card. Leave it open.
2. In File Explorer, delete `Nova`'s `.md` file (the `Nova` folder may stay).
3. Wait about one second (the list behind will repaint) but do NOT close the card.
4. On the still-open card, click the `Remove` icon button, then `Remove` in the prompt. Read the card and the action row.
5. Click `Open`. Read the card.
6. Click `Edit`, change a word, then click `Save`. Read the card.
7. Read the app console for any unhandled exception.

**Pass if — all of these**

- Confirm produces the red line `Persona 'Nova' does not exist.` and the button pair collapses back to the single `Remove` icon (`AgentProfileCard.razor:1936-1947`).
- `Open` produces the same message.
- Edit-then-Save produces the same message.
- The console shows no unhandled exception and the page never shows the yellow `An unhandled error has occurred.` bar.

**Fail if — any of these**

- The yellow error bar appears -> an unhandled exception killed the circuit; the user loses the whole page because a file moved.
- Nothing at all happens on click -> a silent no-op leaves the user believing the action worked.
- The `Remove` prompt stays showing after the failure -> the pending-delete state survives an error and can be triggered again by one click.

**Inconclusive if**

If the card auto-closed when the file was deleted, this scenario cannot be produced through the UI — record that the card closes on external delete (a behaviour change) and mark the rest unverified.

> [!NOTE]
> Delete the leftover `Nova` folder afterwards.

### AGENTPROFILECARD-32 — 'Open' launches the OS editor on the server

**Free** · about 4 min

*Proves the Open action either opens the file or reports why, never failing silently.*

**Before you start**

- An agent exists whose file is present on disk.
- The machine has some application registered for `.md`.

**Steps**

1. Click the agent's tile.
2. Click `Open`.
3. Observe whether an application window opens showing the Persona file, and note which application.
4. Return to the browser and check whether a red line appeared on the card.

**Pass if — all of these**

- The Persona file opens in whatever the machine has registered for `.md`, OR a red line reads `Could not open '<Name>': <message>` (or `Could not open '<Name>': no application is registered for .md on this machine.`) when no handler exists (`AgentProfileCard.razor:2043-2054`).
- One of those two things always happens — never neither.

**Fail if — any of these**

- Nothing opens AND no error line appears -> a silent failure; the user clicks repeatedly with no feedback at all.
- An unhandled exception / the yellow error bar appears -> the failure path is not catching what the OS threw.

**Inconclusive if**

If the file opens but in an unexpected application, that is the OS file association, not this app — not a finding. If the app is running on a different machine from the browser, this action opens an editor you cannot see; run it locally or mark inconclusive.

> [!NOTE]
> This action launches a process on the SERVER, not in the browser, ungated in every environment. That is a recorded, deliberate decision for a single-user proof of concept. Do NOT file the absence of a permission prompt as a security defect here.

### AGENTPROFILECARD-33 — A Windows reserved device name as a Name (documented limit — record the outcome)

**Free** · about 5 min

*Determines WHICH of the known outcomes a reserved device name produces, because an unhandled exception that kills the page is materially worse than a silent no-op and the docs do not say which happens.*

**Before you start**

- Windows.
- The Create card can be opened.
- The app console is visible.

**Steps**

1. Note the current contents of `<data>\Agents`.
2. Click `New agent`. Set **Name** `CON`, **Title** `T`, **Alias** `cn1`, body `x`.
3. Click `Add agent`.
4. Record precisely what happens: does a red validation line appear, does the card switch to View mode as if saved, or does the yellow `An unhandled error has occurred.` bar appear?
5. Read the app console and copy any exception verbatim.
6. List `<data>\Agents` again and note whether any new folder (`CON`) or file (`CON\CON.md`) exists.
7. Repeat the whole test with **Name** `NUL` (alias `nl1`), then `COM1` (alias `cm1`).

**Pass if — all of these**

- Whatever happens is recorded exactly, with the console text and the directory listing, for each of the three names.
- No data loss occurs: no OTHER Persona file is changed or removed.

**Fail if — any of these**

- An existing Persona file is modified or deleted as a side effect -> a genuine new defect, independent of the known limit.
- A different, unrelated exception appears -> record it as a new finding.

**Inconclusive if**

If the Name is rejected with `'CON' is not a valid Persona name.`, the rule has been tightened since the limit was recorded — that is an improvement, not a failure. Record it as a documentation update and move on.

> [!NOTE]
> THIS IS A RECORDED KNOWN LIMIT: reserved device names pass the Name rules, and Create would write `Agents\CON\CON.md` after `Directory.CreateDirectory` (`PersonaStore.cs:377-378`), which Windows may refuse. The card's save catches only `ChatException` (`AgentProfileCard.razor:1851`), so an `IOException` or access error from that write is not caught there. The whole value of this test is deciding WHICH outcome occurs — an unhandled exception that kills the circuit, a red line, or an apparently successful save — and reporting that precisely. If File Explorer cannot delete a `CON` folder it created, remove it from PowerShell with a `\\?\` path.

### AGENTPROFILECARD-34 — Two browser tabs on /agents stay in step

**Free** · about 8 min

*Proves both circuits observe the same change event and that closing a tab with a card open leaves no disposed-object exception behind.*

**Before you start**

- Two browser tabs can be opened on the same machine.
- The app console is visible.
- `node` is installed so a probe spawn would be visible.

**Steps**

1. Open http://localhost:5100/agents in tab A and again in tab B. Arrange them so both are visible.
2. In tab A, create an agent: Name `Sync`, Title `T`, Alias `syn`, body `x`. Do not touch tab B.
3. Look at tab B's list without reloading it.
4. In tab A, close the card, click the `Sync` tile, click the `Remove` icon button, then `Remove` in the prompt.
5. Look at tab B's list again without reloading it.
6. Recreate `Sync`. In tab A click its tile then `Edit`; in tab B click the same tile then `Edit`, so both cards are open at once.
7. Run `O-ADAPTERS` in `T-B` and count the node processes.
8. Close tab B while its Edit card is still open. Read the app console.
9. Remove `Sync` and delete the leftover `<data>\Agents\Sync` folder.

**Pass if — all of these**

- Tab B gains the `Sync` tile without a reload.
- Tab B loses the tile when tab A removes it, without a reload.
- With both Edit cards open, at most one node process is running for the model probe at any moment.
- Closing tab B leaves no `ObjectDisposedException` or `InvalidOperationException` in the console.

**Fail if — any of these**

- Tab B never updates -> a component subscription is missing or leaked; two windows on the same app show contradictory agent lists.
- Two node processes spawn simultaneously for the two card opens -> the probe's serialising gate regressed; every extra adapter process is a real process cost.
- An `ObjectDisposedException` appears when a tab closes -> a singleton-event subscription outlived its component and will keep firing into a dead circuit.

**Inconclusive if**

If no node process ever appears, the adapter is missing or unauthenticated — the concurrency half of this test is unverifiable; record it as unverified and judge only the list-sync half.

### AGENTPROFILECARD-52 — The Chief of Staff offers Reset to default and no Remove

**Free** · about 6 min

*Proves the built-in agent cannot be removed from its card, and that Reset to default confirms first, restores the shipped Title and keeps the card open.*

**Before you start**

- Lane is `P-LAUNCH-FREE`; the Chief of Staff's tile is on the Agents page.
- You know the Chief of Staff's file: `<data>\Agents\Chief of Staff\Chief of Staff.md`.

**Steps**

1. Click the `Chief of Staff` tile. Note its Title (the line under the name) and read the action row; hover over each icon button there.
2. Click `Reset to default`. Read the action row and any caption or sentence that appears.
3. Click `Cancel`. Read the action row, and confirm the file is untouched.
4. Click `Edit`, change the **Title** box to `Edited title`, press Tab, and click `Save`. Confirm the View card shows `Edited title`.
5. Click `Reset to default`, then `Reset` in the `Reset to default?` prompt. Read the card.
6. Open the Chief of Staff's file and read the `name:`, `alias:` and `title:` lines.

**Pass if — all of these**

- The action row holds `Edit`, `Open`, `Restart` and an outlined red button `Reset to default` (a `Message` button leads the row when a direct Room exists). There is NO `Remove` icon button and no `Remove` tooltip (`AgentProfileCard.razor:198-209`).
- Clicking `Reset to default` replaces it with the prompt `Reset to default?`, `Reset` and `Cancel`, and shows the sentence `Restore the Chief of Staff's instructions, Title, Teams, Skills, Model and Effort to their defaults? Its Name, Alias, Rooms and history are kept.` (`AgentProfileCard.razor:229-232`).
- `Cancel` brings back the single `Reset to default` button and changes nothing.
- `Reset` does NOT close the card: it stays in View mode, and the Title is back to the value noted in step 1, not `Edited title` (`AgentProfileCard.razor:1963-1994`).
- Afterwards `name:` still reads the Chief of Staff's Name and `alias:` reads `cos`; the card's `Model` reads `Agent default`, `Effort` reads `Model default` and `Work mode` reads `Agent default`.

**Fail if — any of these**

- A `Remove` icon appears on this card -> the built-in agent can be deleted from the UI, and the next start silently re-creates a fresh one.
- `Reset to default` acts on the first click -> a destructive overwrite of the Persona text has no confirmation.
- `Reset` closes the card, or the Title is still `Edited title` -> the reset did not write the defaults back.
- The Name or Alias changed -> Reset overwrote an identity it promises to keep.

**Inconclusive if**

If the Chief of Staff's tile is missing, run AGENTPROFILECARD-01's restore (`P-STOP`, then `P-LAUNCH-FREE`) and repeat. If a previous test renamed the Chief of Staff, the Name in step 6 is that new Name; the Alias is still the one it had.

### AGENTPROFILECARD-35 — Remove deletes the file and ALL THREE stored settings

**Free** · about 9 min

*Proves the Model, Effort and Work mode rows go with the Persona file, so a later agent of the same name cannot silently inherit a setting nobody chose.*

**Before you start**

- `sqlite3` is on PATH.
- An agent `Nova` exists at `<data>\Agents\Nova\Nova.md`.

**Steps**

1. Seed all three settings directly so this test needs no model catalog: run `sqlite3 "<data>\team.db" "INSERT OR REPLACE INTO persona_models(persona_name,model) VALUES('Nova','claude-haiku-test'); INSERT OR REPLACE INTO persona_efforts(persona_name,effort) VALUES('Nova','low'); INSERT OR REPLACE INTO persona_work_modes(persona_name,work_mode) VALUES('Nova','ghost-mode');"`
2. Verify: `sqlite3 "<data>\team.db" "SELECT * FROM persona_models WHERE persona_name='Nova'; SELECT * FROM persona_efforts WHERE persona_name='Nova'; SELECT * FROM persona_work_modes WHERE persona_name='Nova';"` — all three must return a row.
3. In the browser, click the `Nova` tile and confirm `Model` shows `claude-haiku-test` and `Effort` shows `low`.
4. Click the `Remove` icon button (the person-with-a-minus icon), then `Remove` in the prompt.
5. Confirm `<data>\Agents\Nova\Nova.md` is gone. The `Nova` folder itself stays behind (`Remove` deletes only the file and the rows, `PersonaStore.cs:472-483`).
6. Re-run the sqlite query from step 2.
7. Delete the leftover `<data>\Agents\Nova` folder.

**Pass if — all of these**

- Before the remove, the three tables hold a row for `Nova` and the card shows the Model and the Effort.
- After the remove, `Nova.md` no longer exists and the tile is gone.
- The query returns nothing at all for all three tables — no row, not a blank row.

**Fail if — any of these**

- A `persona_models`, `persona_efforts` or `persona_work_modes` row survives -> an agent created later under the same Name silently inherits a Model, Effort or Work mode nobody chose for it, and there is no UI anywhere that would show why (`PersonaStore.cs:477-479` deletes all three).
- A row survives holding an empty string -> worse than a stale value: a row that exists but never resolves warns on every single session start, forever.
- The file is deleted but the card reports success while the rows remain -> the two halves of the delete are no longer one operation.

**Inconclusive if**

If `sqlite3` is missing, seed the values through the UI instead (pick Haiku and low in an Edit card — requires node and an authenticated adapter) and read them back from the View card; that proves less but is not nothing, and the Work mode half is then unverified. Record which route you used.

> [!NOTE]
> `team.db` is in WAL mode, so `sqlite3` can read it while the app is running. Do not copy `team.db` on its own — recent writes live in `team.db-wal`.

### AGENTPROFILECARD-36 — A stored Model or Effort the catalog does not advertise survives an edit

**Free** · about 10 min

*Proves the card synthesises an option for a stored value it cannot find in the catalog, so opening Edit and saving does not silently wipe the agent's model.*

**Before you start**

- `sqlite3` is on PATH.
- An agent `Nova` exists.

**Steps**

1. Run `sqlite3 "<data>\team.db" "INSERT OR REPLACE INTO persona_models(persona_name,model) VALUES('Nova','claude-ghost-9'); INSERT OR REPLACE INTO persona_efforts(persona_name,effort) VALUES('Nova','ghost-effort');"`
2. In the browser, reload the page, then click the `Nova` tile.
3. Read the `Model` and `Effort` sections of the View card.
4. Click `Edit`. Read the selected option in the **Model** select and in the **Effort** select, and check whether each list also contains the real catalog options (when a catalog is available).
5. Without touching either select, change one word of the body text and click `Save`.
6. Re-run `sqlite3 "<data>\team.db" "SELECT * FROM persona_models WHERE persona_name='Nova'; SELECT * FROM persona_efforts WHERE persona_name='Nova';"`
7. Clean up: delete both rows with `sqlite3 "<data>\team.db" "DELETE FROM persona_models WHERE persona_name='Nova'; DELETE FROM persona_efforts WHERE persona_name='Nova';"`

**Pass if — all of these**

- The View card's `Model` section shows the raw id `claude-ghost-9` and `Effort` shows `ghost-effort`.
- In Edit, the **Model** select has `claude-ghost-9` SELECTED (as a synthesised first entry when the real catalog is present), and the **Effort** select has `ghost-effort` selected.
- After the save, `persona_models` still holds `claude-ghost-9` and `persona_efforts` still holds `ghost-effort`.

**Fail if — any of these**

- In Edit, the select falls back to `Agent default` -> and after the save the stored value is GONE from the table; opening Edit for any unrelated reason silently wipes an agent's model. This is the exact failure the synthesised option exists to prevent.
- The View card shows `Agent default` / `Model default` despite the rows existing -> the stored value is not being read at all.
- The card refuses to open, or an error appears -> an unrecognised stored value is bricking the Persona; it must be at most a warning at session start, never a failure.

**Inconclusive if**

If the app was started before the rows were inserted, reload the browser page and reopen the card — the value is read fresh on each card open, so a stale card is not a failure. If `sqlite3` is missing, this test cannot run; mark it blocked.

> [!NOTE]
> This is the strongest silent-data-loss test in the area that needs no adapter at all.

### AGENTPROFILECARD-37 — The model probe never runs on a plain page load

**Free** · about 8 min

*Proves the throwaway adapter process is spawned only when a Create or Edit card opens — never from browsing, filtering, or opening a View card.*

**Before you start**

- Lane is `P-LAUNCH-FREE`, freshly launched: the model list is cached for the life of the app after the first probe (`ModelCatalogProbe.cs:109-112`), so the `Edit` half below only shows a spawn on the first Edit or Create after a launch.
- `node` is on PATH so a spawn would be visible.
- At least two agents exist, in at least two different Teams. Create them from the Create card with the **Teams** field (for example `Nova` in `Business` and `Iris` in `Household`).
- A PowerShell window is available.

**Steps**

1. In `T-B` run `O-ADAPTERS` and record the count (it prints `0` when nothing is running).
2. In the browser, load http://localhost:5100/agents and reload it three more times.
3. Run `O-ADAPTERS` again and read the app console for any line containing `Model catalog probe`.
4. Change the **Team** filter to a team, then back to `All teams`.
5. Run `O-ADAPTERS` again.
6. Click an agent tile to open the VIEW card.
7. Run `O-ADAPTERS` again.
8. Now click `Edit` on that card.
9. Run `O-ADAPTERS` again within a couple of seconds, and watch the **Model** hint text on the card.

**Pass if — all of these**

- The node process count is unchanged after every page load, after every filter change, and after opening the View card.
- No `Model catalog probe` line is written to the console during any of those.
- Only after clicking `Edit` does a short-lived node process appear and/or the Model hint change from `Reading the models this agent offers…`.

**Fail if — any of these**

- A node process appears on page load -> browsing to a page spawns an adapter process; this is the exact regression the page is designed and unit-tested against.
- A node process appears when you change the Team filter -> a pure display change is touching the model catalog.
- A node process appears when the VIEW card opens -> the tile-click path is no longer synchronous and instant.

**Inconclusive if**

If node never appears even after `Edit`, the adapter is missing or needs authentication — the console will say which (`No ACP adapter is installed; the model catalog is empty.` or `Model catalog probe skipped: the adapter needs authentication.`). The 'never on page load' half is still verified; record the `Edit` half as unverified.

> [!NOTE]
> The probe deliberately runs even with `Team:Acp:Enabled=false`. It spends no tokens — it never sends a prompt. Do not file that as ignoring the money guard.

### AGENTPROFILECARD-38 — Model and Effort pickers: labels, loading text and the empty-catalog text

**Free** · about 8 min

*Proves both selects offer a blank default, announce their loading state, and resolve into either a real list or the correct empty-catalog sentence — never a permanent spinner.*

**Before you start**

- Lane is `P-LAUNCH-FREE`, freshly launched, and this is the first Edit or Create of the run: the model list is cached after the first probe (`ModelCatalogProbe.cs:109-112`), so a later open skips the loading text.
- `node` is on PATH.
- At least one agent exists.
- The app console is visible.

**Steps**

1. Click an agent tile, then click `Edit`. Immediately read the hint under **Model** and the hint under **Effort**.
2. Wait up to 25 seconds, re-reading both hints until they stop changing.
3. Open the **Model** select and read its first option and the rest of the list.
4. Open the **Effort** select and read its first option and the rest of the list. Look specifically for any option whose value or label is `default`.
5. If a real list came back, set **Model** to Haiku and **Effort** to low. If only the blank option is offered, note that and skip.
6. Read the console for any line beginning `Model catalog probe` or `No ACP adapter is installed`.
7. Click `Cancel` to close without saving.

**Pass if — all of these**

- While loading, the Model hint reads `Reading the models this agent offers…` and the Effort hint reads `Reading the effort levels this model offers…`.
- Both hints resolve within 25 seconds to either the real-list wording (`Which model this agent thinks with. Changing it restarts the agent, which clears what it remembers.` / `How hard this agent thinks. Changing it restarts the agent, which clears what it remembers.`) or the empty wording (`This agent advertises no models, so it will use its own default.` / `This model offers no effort choice, so it will think as it normally does.`).
- Both selects' first option is labelled `Agent default`.
- No option labelled or valued `default` appears in the Effort list.

**Fail if — any of these**

- A loading hint never clears -> the probe is hung; it should time out at 20 seconds and log `Model catalog probe timed out after 00:00:20.` before falling back to the empty wording.
- An option with the id `default` appears in the Effort list -> the adapter's own sentinel is no longer filtered out; there are now two options meaning the same thing and the literal string `default` can reach the database.
- Either select has no blank first option -> an agent can no longer be set back to the agent's own default.

**Inconclusive if**

An empty catalog is a LEGITIMATE result when node is missing, the adapter is not installed, or its authentication has lapsed. The picker cannot tell you which; the console can. Read it and record the cause rather than calling the test failed.

> [!NOTE]
> Steps 3 and 4 open the selects only AFTER step 2 has waited for both hints to settle, which is why this test is unaffected by issue #39's fix. What each select does DURING its loading window - refuse to open, visibly dimmed - is [MODELEFFORT-28](model-effort.md#modeleffort-28--both-pickers-are-inert-while-their-own-catalog-is-being-probed-and-the-model-list-lands-without-waiting-on-the-effort-one)'s job, not this one's.

### AGENTPROFILECARD-39 — Changing the Model clears the Effort and re-probes the ladder (Haiku -> Sonnet)

**Free** · about 8 min

*Proves an effort chosen against the old model is never carried across to a new one, and that a superseded probe cannot land on the newer choice.*

**Before you start**

- A real model catalog is available (AGENTPROFILECARD-38 returned a populated list).
- An agent exists.
- `T-B` is open for `O-ADAPTERS`.

**Steps**

1. Open the agent's card and click `Edit`. Wait for both hints to settle.
2. Set **Model** to Haiku and **Effort** to low.
3. Change **Model** to Sonnet.
4. IMMEDIATELY read the **Effort** select's selected option and its hint text.
5. Run `O-ADAPTERS` and note whether a new process appeared.
6. Confirm the **Model** select still shows Sonnet.
7. Now change **Model** to Haiku, and within a second change it back to Sonnet.
8. Wait for the Effort hint to settle, then open the **Effort** select and compare its options against what Sonnet offered in step 3-4.
9. Click `Cancel` to close without saving.

**Pass if — all of these**

- Immediately after the model change the **Effort** select shows the blank `Agent default` option.
- The Effort hint shows `Reading the effort levels this model offers…` and then settles.
- A node process appears for the per-model effort probe the first time each model id is chosen.
- The **Model** select stays on Sonnet throughout.
- After the quick Haiku -> Sonnet switch, the effort list that finally lands matches the LAST model chosen (Sonnet).

**Fail if — any of these**

- The **Effort** select stays on `low` after the model switch -> a level chosen for the old model is being carried across; the adapter silently clamps an unsupported level back to its default with no error, so this presents later as 'the effort just didn't take' with nothing in the UI to explain it.
- The effort list that lands after the rapid double switch belongs to the ABANDONED model -> a superseded probe answer is overwriting the current one.
- Changing the model also resets the **Model** select -> the change is not sticking at all.

**Inconclusive if**

If the catalog offers only one model, this test cannot run — record it as blocked. If the effort list is empty for both models, the clearing behaviour is unobservable; record the node-spawn half only.

> [!NOTE]
> Free: the probe never sends a prompt. Never switch to Opus; Haiku -> Sonnet is the only model switch any test in this area asks for.

### AGENTPROFILECARD-40 — Model is stored as an id, in team.db only

**Free** · about 7 min

*Proves the chosen model is persisted as the catalog id rather than the display name, and never reaches the Persona file.*

**Before you start**

- `sqlite3` is on PATH.
- A real model catalog is available.
- The Create card can be opened.

**Steps**

1. Click `New agent`. Set **Name** `Modelcheck`, **Title** `T`, **Alias** `mdc`, body `x`.
2. Set **Model** to Haiku. Leave **Effort** blank.
3. Click `Add agent`.
4. Read the `Model` section of the resulting View card and write down exactly what it says.
5. Run `sqlite3 "<data>\team.db" "SELECT * FROM persona_models WHERE persona_name='Modelcheck';"` and write down the stored value.
6. Open `<data>\Agents\Modelcheck\Modelcheck.md` and search it for the stored value, for the display name, and for the word `model`.
7. Delete the agent through the card afterwards (Setup step 7, including the leftover `Modelcheck` folder).

**Pass if — all of these**

- The View card's `Model` section shows a human display name (for example `Haiku 4.5`).
- The database row holds a machine id (for example `claude-haiku-4-5-...`), lowercase and hyphenated — NOT the display name.
- `Modelcheck.md` contains no `model:` line and neither the id nor the display name.

**Fail if — any of these**

- The database holds the display name (for example `Haiku 4.5`) -> it will never resolve against a catalog id, so it logs a warning at every single session start, forever, and the agent silently falls back to the agent default.
- A row exists with an empty string -> the blank option wrote a row instead of deleting one; that row exists but never resolves.
- The model appears in the `.md` file -> file and database can now disagree about the same fact.

**Inconclusive if**

If the Model select offers no options, the test cannot run — record it as blocked on the adapter and check the console for the reason line.

### AGENTPROFILECARD-41 — Effort low -> medium is stored as an id, and the blank option deletes the row

**Free** · about 9 min

*Proves the effort is persisted as its lowercase id, appears as its display name on the card, stays out of the Persona file, and that clearing it removes the row rather than blanking it.*

**Before you start**

- `sqlite3` is on PATH.
- A real effort catalog is available.
- An agent exists whose Model is set to Haiku.

**Steps**

1. Open the agent's card, click `Edit`, wait for the hints to settle, set **Effort** to low, and click `Save`.
2. Read the `Effort` section on the resulting View card.
3. Run `sqlite3 "<data>\team.db" "SELECT * FROM persona_efforts WHERE persona_name='<Name>';"` and record the value.
4. Click `Edit` again, change **Effort** from low to medium, and click `Save`.
5. Read the `Effort` section again and re-run the sqlite query.
6. Open the Persona file (`<data>\Agents\<Name>\<Name>.md`, or the card's `Open` button) and search it for `effort`, `low` and `medium`.
7. Click `Edit` once more, set **Effort** back to the blank `Agent default` option, and click `Save`.
8. Re-run the sqlite query one last time.

**Pass if — all of these**

- After step 1 the card's `Effort` section shows the display name (for example `Low`) and the row holds the lowercase id `low`.
- After step 4 the card shows `Medium` and the row holds `medium` — the id, never the display name.
- The `.md` file contains no effort at all.
- After step 7 the query returns NOTHING — the row is deleted, not blank — and the card's `Effort` section reads `Model default`.

**Fail if — any of these**

- The database holds a display name (`Medium`) -> it will never resolve and will warn at every session start forever.
- After choosing the blank option the row still exists, holding an empty string -> a row that exists but never resolves; the warning is permanent and invisible in the UI.
- The effort landed in the `.md` file -> file and database now carry the same fact in two places.

**Inconclusive if**

If the Effort select offers only the blank option for Haiku, this model advertises no effort ladder; record that and mark the test blocked rather than failed. Never switch to high, xhigh or max to find a populated ladder.

> [!NOTE]
> low -> medium is the only effort switch any test in this area asks for.

### AGENTPROFILECARD-42 — Restart with Team:Acp:Enabled=false reports Offline and starts nothing

**Free** · about 8 min

*Proves the card's Restart button cannot route around the flag that exists to stop the app spending money: with the flag off it reports a fixed reason and spawns no adapter.*

**Before you start**

- Lane is `P-LAUNCH-FREE` (`Acp:Enabled=false`).
- `node` is on PATH and the adapter is installed.
- At least one agent exists and reads `Offline`. The Chief of Staff is enough.
- A PowerShell window and the app console are both visible.

**Steps**

1. Confirm every tile on /agents reads `Offline`.
2. Run `O-ADAPTERS` and record the count (it must be `0`).
3. Click an agent tile and confirm the card offers a `Restart` button, with the caption `Like saving an edit, this restarts the agent, which clears what it remembers.` beneath the row.
4. Click `Restart` once and immediately read the button's label.
5. Within five seconds run `O-ADAPTERS` again and record the count.
6. Read the card's status line and the red line under it, and the tile behind the dialog, for 30 seconds; record every state they pass through.
7. Read the app console for any line naming this Persona.

**Pass if — all of these**

- The button label changes to `Restarting…` and the button is disabled while the restart is in flight, then returns to `Restart`.
- The status stays `Offline` and the card shows the red reason `Agents are switched off in this configuration, so nothing was started.` (`PersonaSupervisor.cs:508-512`; the card shows `status.Reason`, `AgentProfileCard.razor:114-116`).
- `O-ADAPTERS` prints `0` both before and after the click: no adapter was started.
- The page never shows the yellow unhandled-error bar.

**Fail if — any of these**

- The badge reaches `Starting` or `Online`, or `O-ADAPTERS` is above `0` -> Restart bypasses the money guard; the agent would take billed Turns the moment anyone messaged it. Headline defect.
- The badge stays `Offline` with a different reason, or none, and nothing in the console -> the failure is invisible; the exact reason line is the whole feature here.
- The yellow error bar appears -> the restart path let an exception reach the circuit instead of surfacing it on the card.

**Inconclusive if**

If `O-ADAPTERS` was above `0` before the click, an earlier test left an adapter running; stop it, relaunch the lane and repeat.

> [!NOTE]
> The guard is in `StartHostIfMissingAsync` (`PersonaSupervisor.cs:496-512`), the one place a host is created, so startup, a file change and the Restart button all reach it. With the flag off nothing is subscribed to persona changes either, so an edit does not auto-restart the agent afterwards.

### AGENTPROFILECARD-43 — A newly created agent comes online by itself when ACP is on

**Free** · about 10 min

*Proves a create is picked up by the supervisor exactly once, with the status following live on both the tile and the open card.*

**Before you start**

- Lane is `P-LAUNCH-UAT`: ACP is on and the adapter is the free mock, so nothing here spends money. The Chief of Staff, Ada and Bo are already running. This test does not set a Model or an Effort: the mock advertises no models.
- The mock adapter is counted by `O-MOCK-ADAPTERS`, never by `O-ADAPTERS`.
- In this lane `<data>` is `C:\seeds\huddle\uat\data`.

**Steps**

1. Run `O-MOCK-ADAPTERS` and record the count.
2. Create an agent from the card: Name `Live`, Title `T`, Alias `lvo`, body `You are Live.`. Leave Model and Effort blank.
3. Leave the resulting View card OPEN and watch its status line, without reloading, for up to 60 seconds. Record every state it shows.
4. Watch the tile behind the dialog at the same time.
5. Run `O-MOCK-ADAPTERS` again and count how many NEW processes appeared for this one agent.
6. Read the app console for any line naming `Live`.
7. Close the card, click the `Remove` icon button then `Remove` in the prompt, and run `O-MOCK-ADAPTERS` once more after 10 seconds. Then delete the leftover `<data>\Agents\Live` folder.

**Pass if — all of these**

- The status moves `Offline` (or `Starting`) -> `Starting` -> `Online` within a few seconds, on both the open card and the tile, with no page reload.
- Exactly ONE new mock adapter process appears for the one agent.
- After the remove, that process goes away.

**Fail if — any of these**

- Two or three processes appear for a single save -> one save is raising several change events, so one agent spawns several adapters.
- The badge sticks on `Offline` with NO reason line and nothing in the console -> a start failure with no diagnosis anywhere; a reason line IS the feature.
- The open card's status never moves while the tile's does -> the card is not following live health, so a user watching the card sees a permanently starting agent.
- The process survives the remove -> an orphaned adapter keeps running with no agent attached.

**Inconclusive if**

If the badge sticks on `Offline` WITH a reason line, record the reason text and mark the online path unverified; it usually means the mock adapter path in `P-LAUNCH-UAT` is wrong.

> [!NOTE]
> Starting a session spends no tokens by itself — no prompt is sent — and the mock never bills. Stay in this lane for AGENTPROFILECARD-44 through 49 and 53.

### AGENTPROFILECARD-44 — The Message action is read once, when the card opens

**Free** · about 8 min

*Proves the Message link is absent rather than broken on a card opened before the agent has a Room, and that the card reads the Room once: a card that is already open does not grow the link, but the next open does.*

**Before you start**

- Lane is `P-LAUNCH-UAT` (see AGENTPROFILECARD-43). `<data>` is `C:\seeds\huddle\uat\data`.
- A name that has never connected: `Freshone`.
- `sqlite3` is on PATH.

**Steps**

1. Click `New agent`. Set **Name** `Freshone`, **Title** `T`, **Alias** `frn`, body `x`, and click `Add agent`.
2. On the resulting View card, read the action row.
3. Leave the card OPEN and wait until its status reads `Online`.
4. Without closing or reopening the card, read the action row again.
5. Run `sqlite3 "<data>\team.db" "SELECT name FROM users WHERE name='Freshone'; SELECT id, name FROM rooms;"`.
6. Close the card and click the `Freshone` tile again. Read the action row.
7. If a `Message` button is there, hover it and read its target, then click it.

**Pass if — all of these**

- Right after the create, the action row is `Edit`, `Open`, `Restart` and the `Remove` icon — NO `Message` (the card's Room is null at that point).
- Step 4: the action row is unchanged. The open card read its Room once, when it opened (`AgentProfileCard.razor:785`), and `RefreshFromStore` does not re-read it (`:1903-1917`).
- Step 5: a `users` row for `Freshone` and a Room that contains it now exist.
- Step 6: a `Message` button leads the row, its target is `/rooms/<id>` with the id from step 5, and clicking it opens that Room.

**Fail if — any of these**

- A `Message` button is offered on the card before any Room exists -> it leads to a Room that does not exist; the link 404s or lands on an empty page.
- After a reopen the button is still absent although step 5 shows the Room -> the card is not reading the Room at all.
- Clicking `Message` lands on an empty or missing Room -> the id being rendered is not the Room's.
- The button DOES appear on the still-open card in step 4 -> the card now refreshes its Room link; that is a behaviour change to record against this test's wording, not a defect in the link.

**Inconclusive if**

If the agent never reaches `Online`, or step 5 shows no Room, the second half cannot run — record the first half (Message correctly absent) as passed and the rest as unverified.

> [!NOTE]
> An agent card opened before the agent has a Room having no Message action is DELIBERATE — absent rather than broken. Do not file the absence itself, and do not file "I had to close and reopen the card".

### AGENTPROFILECARD-45 — Restart is offered in every state except Starting

**Free** · about 8 min

*Proves the Restart button and its caption are shown for Online, Offline and Degraded agents and hidden only while a session is mid-creation, so a manual restart (the only way to apply a next-session Prompt) is always reachable without racing a start.*

**Before you start**

- Lane is `P-LAUNCH-UAT`; Ada is `Online`.
- A PowerShell window is available.

**Steps**

1. Open Ada's card (Online). Read the whole action row and the area directly beneath it.
2. Click `Restart` and open the card again at once, or open Bo's card straight after clicking `Restart` on it, to catch the `Starting` state. Read its action row and the area beneath it. If you miss it, say so.
3. Wait for `Online` and read the row again.
4. `P-STOP`, then launch `P-LAUNCH-FREE`, reload the page and open the Chief of Staff's card, which now reads `Offline`.
5. Read the action row and the area beneath it again.

**Pass if — all of these**

- While `Online`, the actions are `Message`, `Edit`, `Open`, `Restart` and the `Remove` icon, and the caption `Like saving an edit, this restarts the agent, which clears what it remembers.` is shown (`AgentProfileCard.razor:182-185, 234-237`).
- While `Starting`, `Restart` and the caption are both absent.
- Once `Offline`, both `Restart` and the caption are present.

**Fail if — any of these**

- `Restart` is missing for an `Online` or `Offline` agent -> the only way to apply a next-session Prompt to a healthy agent is gone (the code comment at `AgentProfileCard.razor:167-181` records that this was fixed).
- `Restart` is offered while `Starting` -> a Restart there races the start it would cancel.
- The caption appears without the button, or vice versa -> the two are supposed to move together.

**Inconclusive if**

Catching `Starting` is timing-dependent; if you miss it, record the Online and Offline halves and say the Starting half is unverified rather than guessing.

### AGENTPROFILECARD-46 — A save that changes nothing does not restart the agent

**Free** · about 8 min

*Proves a no-op Save does not destroy a live session's conversation memory, while a real change does.*

**Before you start**

- Lane is `P-LAUNCH-UAT`; Ada is `Online`.
- The app console and a PowerShell window are visible.

**Steps**

1. Snapshot the adapter processes: `Get-CimInstance Win32_Process -Filter "Name='mock-acp.exe'" | Select-Object ProcessId, CreationDate | Sort-Object ProcessId`. Write down every ProcessId.
2. Open Ada's card and confirm the badge reads `Online`.
3. Click `Edit`, touch nothing at all, and click `Save`.
4. Watch the badge continuously for 15 seconds and record every state it shows.
5. Take the snapshot again and compare the set of ProcessIds.
6. Read the console for a fresh adapter launch sequence.
7. Now click `Edit` again, change one word of the Persona body, and click `Save`.
8. Watch the badge again for 15 seconds, and take the snapshot once more.

**Pass if — all of these**

- After the no-op save the badge stays `Online` and never shows `Starting`.
- The set of ProcessIds is identical after the no-op save.
- After the word change the badge DOES pass through `Starting` and back to `Online`, and exactly one ProcessId is replaced by a new one.

**Fail if — any of these**

- The badge flickers `Starting` -> `Online` after the no-op save -> the session was restarted and that agent's entire conversation memory was destroyed for nothing; the comparison that should have short-circuited is whole-record equality over text, Model and Effort.
- The word change does NOT restart -> a real edit did not take effect, and the card claims otherwise; the agent keeps thinking with the old prompt.
- A ProcessId changes but the badge never moves -> the status is not following the actual session.

**Inconclusive if**

If the badge is never `Online` to begin with, this test cannot distinguish a restart from a failed start — get Ada genuinely `Online` first or mark blocked.

> [!NOTE]
> Free: a restart sends no prompt, and the mock never bills. Only the later memory test (AGENTPROFILECARD-50) spends anything.

### AGENTPROFILECARD-47 — Changing only the Model or only the Effort restarts the session

**Free** · about 12 min

*Proves a Model or Effort change actually takes effect, which is only possible by stopping and restarting the session. The mock adapter advertises no models, so the change is from a seeded stored value to the blank default; that is still a Model-only (then Effort-only) change.*

**Before you start**

- Lane is `P-LAUNCH-UAT`; Ada is `Online`. `<data>` is `C:\seeds\huddle\uat\data`.
- `sqlite3` is on PATH.
- A PowerShell window is visible.

**Steps**

1. Snapshot the adapter processes with the command from AGENTPROFILECARD-46 step 1.
2. Seed a Model: `sqlite3 "<data>\team.db" "INSERT OR REPLACE INTO persona_models(persona_name,model) VALUES('Ada','ghost-model-1');"`
3. Open Ada's card and click `Restart`, so the running session starts with the seeded Model. Wait for `Online` and take the snapshot again (one ProcessId is replaced).
4. Click `Edit`. Confirm the **Model** select shows `ghost-model-1` selected. Change ONLY the **Model** to `Agent default` (do not touch the text) and click `Save`.
5. Watch the badge for 20 seconds, take the snapshot again, and run `sqlite3 "<data>\team.db" "SELECT * FROM persona_models WHERE persona_name='Ada';"`.
6. Seed an Effort: `sqlite3 "<data>\team.db" "INSERT OR REPLACE INTO persona_efforts(persona_name,effort) VALUES('Ada','ghost-effort-1');"`, click `Restart` on the card, wait for `Online` and snapshot.
7. Click `Edit`, confirm **Effort** shows `ghost-effort-1`, change ONLY the **Effort** to `Agent default` and click `Save`.
8. Watch the badge for 20 seconds, snapshot, and run `sqlite3 "<data>\team.db" "SELECT * FROM persona_efforts WHERE persona_name='Ada';"`.

**Pass if — all of these**

- After step 4 and again after step 7 the badge passes through `Starting` and back to `Online`.
- Exactly one ProcessId is replaced by a new one each time.
- `persona_models` returns nothing after step 5 and `persona_efforts` returns nothing after step 8 (the blank option deletes the row).

**Fail if — any of these**

- No restart happens after a Model or Effort change -> a model and an effort are both fixed when a session is created and cannot be swapped into a running one, so the agent keeps thinking with the OLD value while the card claims the new one. This is silent and permanent until something else restarts it.
- The database updates but the session does not restart -> the card and the running session now disagree with no way to tell from the UI.
- The session restarts but the database is unchanged -> the change is lost at the next app start.

**Inconclusive if**

If Ada does not reach `Online` after a `Restart` with a seeded ghost value, record the red reason line and mark this test blocked: the mock rejected the value, so the "running session started with it" precondition does not hold. A real Haiku -> Sonnet restart is then covered only by the paid lane and is not asked for here.

> [!NOTE]
> Free: no prompt is sent by a restart. Seeded values are cleaned up by the test itself; re-running `P-LAUNCH-UAT` resets the whole data folder anyway.

### AGENTPROFILECARD-48 — Remove does NOT cascade: the Agent and its Room survive

**Free** · about 9 min

*Proves that removing a Persona takes it offline without deleting the chat facts it created — a deliberate decision that a tester must not file as a bug.*

**Before you start**

- Lane is `P-LAUNCH-UAT`; Ada has her one-to-one Room. `<data>` is `C:\seeds\huddle\uat\data`.
- `sqlite3` is on PATH.

**Steps**

1. Open Ada's card, click `Message`, send `Hello Ada` and wait for the mock's echo. Note the full `/rooms/<id>` URL from the address bar, and Ada's **Title** and **Alias** from her card.
2. Seed her settings: `sqlite3 "<data>\team.db" "INSERT OR REPLACE INTO persona_models(persona_name,model) VALUES('Ada','ghost-model-1'); INSERT OR REPLACE INTO persona_efforts(persona_name,effort) VALUES('Ada','ghost-effort-1');"`
3. Run `sqlite3 "<data>\team.db" "SELECT id,name FROM users WHERE name='Ada'; SELECT id,name FROM rooms;"` and record the rows.
4. Go back to /agents, open Ada's card, click the `Remove` icon button, then `Remove` in the prompt.
5. Confirm the tile is gone from /agents and `<data>\Agents\Ada\Ada.md` is gone from disk (the `Ada` folder stays).
6. Look at the Rooms list in the left sidebar.
7. Navigate to the `/rooms/<id>` URL you noted and read the page.
8. Re-run the sqlite query from step 3.
9. Confirm the transcript `<data>\rooms\<id>.jsonl` still exists (`O-TRANSCRIPT`).

**Pass if — all of these**

- The agent's tile and its `.md` file are gone.
- The Room is STILL listed in the sidebar and STILL opens at its URL.
- Every message that was already in that Room is still shown.
- The `users` row and the `rooms` row are both still present.
- The removed agent shows as `Offline` inside the Room.
- The transcript file still exists.

**Fail if — any of these**

- The Room disappears from the sidebar or 404s at its URL -> removing a Persona deleted an Agent's chat history; 'removing a Persona' is explicitly not 'deleting an Agent'.
- The transcript file is gone -> conversation history was destroyed by a Persona delete.
- The `users` row is gone -> the Agent identity was cascaded away, which would also break the re-create case (AGENTPROFILECARD-49).

**Inconclusive if**

If the Room never opened or Ada never answered, there is no content for the Room to keep and this test proves less — bring the mock adapter up (`P-LAUNCH-UAT`) and repeat.

> [!NOTE]
> THIS IS A RECORDED REPO-OWNER DECISION. A tester expecting a full cleanup will want to file it — do not. Only the Model, Effort and Work mode rows go with the file (`PersonaStore.cs:472-483`); Agents, Rooms and Transcripts stay. Leave the app running: AGENTPROFILECARD-49 follows.

### AGENTPROFILECARD-49 — Delete then recreate under the same Name: fresh settings, old Room

**Free** · about 10 min

*The sharpest test in the area: proves settings must NOT survive a delete while chat facts MUST. It catches both opposite regressions at once.*

**Before you start**

- AGENTPROFILECARD-48 has just completed, so Ada was removed but her Agent row and Room remain. You noted her Title and Alias and the Room URL there.
- Lane is `P-LAUNCH-UAT`. `sqlite3` is on PATH.

**Steps**

1. Run `sqlite3 "<data>\team.db" "SELECT * FROM persona_models WHERE persona_name='Ada'; SELECT * FROM persona_efforts WHERE persona_name='Ada'; SELECT id,name FROM users WHERE name='Ada';"` and record all three results.
2. Click `New agent` and recreate the SAME Name `Ada`, with the same Title and Alias, body `x`, leaving the Model and Effort selects on their blank option.
3. Click `Add agent` and read the `Model` and `Effort` sections on the resulting View card.
4. Re-run the two settings queries.
5. Wait for the agent to come `Online`, close the card, reopen it and click `Message`.
6. Compare the `/rooms/<id>` URL with the one recorded in AGENTPROFILECARD-48, and read what is already in the Room.

**Pass if — all of these**

- Before the recreate, `persona_models` and `persona_efforts` hold NO row for `Ada`, while the `users` row is still present.
- The recreated agent's card reads `Agent default` and `Model default` — the `ghost-model-1` and `ghost-effort-1` seeded before the delete have NOT come back.
- Both settings queries still return nothing.
- `Message` leads to the SAME Room id as before, with its earlier content intact.

**Fail if — any of these**

- The old Model or Effort reappears on the new agent -> a stored setting was resurrected from before the delete; the user is now running a model they never chose, with nothing in the UI to explain it.
- A brand-new empty Room appears instead of the old one, or the `users` id has changed -> the Agent row WAS cascaded away on the delete, which destroys chat history and breaks every existing link to that Room.
- The card shows `Agent default` but the database holds a row -> the card is not reading what is stored.

**Inconclusive if**

If the agent cannot come online, the Room half is unverifiable — the settings half is still decisive on its own. Record which half you verified.

> [!NOTE]
> Settings must not survive; chat facts must. Either direction being wrong is a defect, and they are opposite regressions. Re-run `P-LAUNCH-UAT` afterwards to restore the cast.

### AGENTPROFILECARD-50 — Editing an agent really does lose its conversation memory (COSTS MONEY)

**💰 Spends money** · about 12 min

*Confirms once, end to end, that the card's own warning is true: saving an edit restarts the session and the agent forgets what it was told.*

**Before you start**

- Lane is `P-LAUNCH-PAID`, including its first step: the Chief of Staff set to Haiku / low on its card before the paid launch, so its Greeting Turn is cheap. The adapter is authenticated.
- An agent is `Online` with Model = Haiku and Effort = low. Verify BOTH on its card before starting.
- The app console is visible.

**Steps**

1. CONFIRM COST FIRST: check the agent's card shows Model Haiku and Effort low. Do not run this test against any other model.
2. Open the agent's Room via the card's `Message` action.
3. Type exactly `Remember the codeword is PELICAN.` and send it. Wait for the reply and confirm the agent acknowledged.
4. Type exactly `What is the codeword?` and send it. Confirm it answers `PELICAN` — this proves the session remembers before the edit.
5. Go to /agents, open the agent's card, click `Edit`, change ONE word of the Persona body, and click `Save`.
6. Watch the badge until it returns to `Online`, and confirm with `O-ADAPTERS-LIST` that the adapter process id was replaced.
7. Return to the Room and send exactly `What is the codeword?` again.
8. Read the reply.

**Pass if — all of these**

- Before the edit, the agent answers `PELICAN`.
- After the edit and restart, the agent does NOT know the codeword (it says it does not know, or asks what codeword).
- The adapter process id is replaced between the two questions.

**Fail if — any of these**

- After the edit the agent still answers `PELICAN` -> the session was not actually restarted, so the edited Persona text has NOT taken effect; the card's own promise (`Saving restarts it, which clears what it remembers`) is false and every edit is silently a no-op on the running session.
- The agent never answers the first question -> this test cannot judge memory; treat as inconclusive, not failed.

**Inconclusive if**

If the agent never reaches `Online`, or the adapter reports an authentication failure, stop — no Turn was spent and nothing was proven. If the reply is ambiguous (the agent guesses or refuses), ask once more with `Repeat the codeword I gave you earlier.` and judge that; do not keep asking, each question costs.

> [!NOTE]
> COST: roughly three short agent Turns on Haiku at low effort — a few hundred tokens in total, well under a cent. Run this ONCE for the whole area, not per edit. It is the only money-spending test here. Do not raise the model or the effort to make the answer 'better'.

---

### AGENTPROFILECARD-51 — The Edit card's Name, Title and Alias boxes write into the frontmatter and preserve every other field

**Free** · about 10 min

*The boxes are helpers over the raw text, not a second source of truth. The field they must not damage is any OTHER frontmatter key: `PersonaFrontmatter.Compose` rebuilds the frontmatter from the keys it knows (identity, teams, adapter, skills, watches, consult_when and the underscore-prefixed built-in keys), so a rewrite built on it would silently delete a plain `role:` line, which `mcp__team__list_agents` sends to the model. Only a file with extra keys can catch that.*

**Before you start**

- `P-LAUNCH-FREE`.
- Create a Persona file by hand at `<data>\Agents\Probe\Probe.md` (create the `Probe` folder first) with extra frontmatter keys, so there is something to destroy:

```markdown
---
name: 'Probe'
title: 'Test Subject'
alias: 'prb'
role: 'Router, triage, and cross-workstation continuity'
consult_when: 'Never - this is a test fixture'
---
You are Probe. Answer in one short sentence.
```

**Steps**

1. Open `/agents`, click the `Probe` tile, then **Edit**. Note whether Name, Title and Alias appear as separate boxes above the Persona text.
2. Change **Title** to `Changed Title` and press Tab. WITHOUT saving, read the Persona text field below. Note the `title:` line and note whether `role:` and `consult_when:` are still there.
3. Change **Alias** to `prb2` and press Tab. Note the `alias:` line in the text below.
4. In the Persona text field, hand-edit `title:` to `'Edited By Hand'` and click outside the field. Note what the Title box now shows.
5. Change **Name** to `Probe2` and press Tab. Note whether a note appears, and write down its wording.
6. Change **Name** back to `Probe` and press Tab. Note whether the note disappears.
7. Change **Name** to `Probe2` again and click **Save**. Note the name the card now shows, the Persona file path it reports, and whether the card is on the details view.
8. Open `<data>\Agents\Probe2\Probe2.md` in an editor. Note every frontmatter key present and the body.
9. Click **Edit**, type an apostrophe into **Title**: `Sam's Probe`, press Tab and save, then re-open the file and note the `title:` line.

**Pass if — all of these**

- Step 1: three separate boxes, above the Persona text field.
- Step 2: the `title:` line in the text below changed to match, and `role:` AND `consult_when:` are both still present and unchanged. This is the assertion that matters most in this test.
- Step 3: the `alias:` line changed to match.
- Step 4: the Title box re-seeded itself to `Edited By Hand` — the sync runs both ways.
- Step 5: a note appears reading `Renaming brings this agent's chats, its history and its work folder with it. Messages already posted keep the old name — the transcript records what was said at the time.` (`AgentProfileCard.razor:290-292`). It does NOT block editing or saving.
- Step 6: the note disappears when the Name matches again.
- Step 7: the card lands on the details view for `Probe2`, not on an error and not on a blank card, and the file path reads `<data>\Agents\Probe2\Probe2.md` (the folder and file both moved, ADR-0011).
- Step 8: the file still holds `role:` and `consult_when:` with their original values, and the body is unchanged.
- Step 9: the apostrophe round-trips — the file reads `title: 'Sam''s Probe'` and the card shows `Sam's Probe` when reopened.

**Fail if — any of these**

- Step 2 or step 8 shows `role:` or `consult_when:` MISSING -> the rewrite is recomposing the frontmatter instead of editing one line. This is the exact failure the design exists to prevent. Defect, and a serious one: it silently removes what the model is told about an Agent.
- Step 7 shows an error like `Persona 'Probe2' does not exist.` -> the save is passing the newly typed Name as the lookup key instead of the Name the Persona is currently filed under. Defect.
- Step 7 leaves the card on the OLD name or on a blank card -> the post-save re-read is using the stale name. Defect.
- Step 5 shows no note -> the rename consequence is now invisible at exactly the moment it is one keystroke away. Defect.
- Step 5 shows wording saying the chats or history "stay behind" -> the note describes the pre-ADR-0011 behaviour and contradicts what the rename now does.
- Step 9 corrupts the file or the Persona vanishes from `/agents` into the rejected-file list -> the value is not being YAML-escaped. Defect.

**Inconclusive if**

`Probe` does not appear on `/agents` at all after you create the file — check the rejected-files block first; a typo in the frontmatter, or the file sitting directly under `Agents` instead of in `Agents\Probe`, makes it a rejected file rather than a Persona, and that is a fixture problem, not a result.

**Afterwards**

Delete the `<data>\Agents\Probe` and `<data>\Agents\Probe2` folders if either remains.

> [!NOTE]
> Nothing in this test writes to disk until **Save**. That is deliberate and worth not "fixing": `PersonaStore.Update` raises `PersonasChanged`, which restarts that Agent's session and loses its conversation memory, so a save-as-you-type box would be a session restart per keystroke.

### AGENTPROFILECARD-53 — An avatar-only Save does not restart the agent

**Free** · about 8 min

*Proves a Save that changed only the avatar is written to the avatar store, not the Persona, so the running session (and its memory) is left alone (ADR-0019).*

**Before you start**

- Lane is `P-LAUNCH-UAT`; Ada is `Online`.
- A PowerShell window is available.

**Steps**

1. Snapshot the adapter processes with the command from AGENTPROFILECARD-46 step 1. Write down every ProcessId.
2. Open Ada's card and click `Edit`. Click the avatar to open the **Avatar** window, choose `A short label`, type `AB` into the **Label** box, click `Apply`, and touch nothing else.
3. Click `Save`.
4. Watch the badge for 15 seconds, record every state, and read the console for a fresh adapter launch sequence.
5. Take the snapshot again and compare the set of ProcessIds.
6. Close the card and read Ada's tile avatar.
7. Reopen the card, click `Edit`, click the avatar, choose `Initials of the name`, click `Apply`, then click `Save` on the card to put the avatar back.

**Pass if — all of these**

- After the save the card is on its View mode (`AgentProfileCard.razor:1885`) and the badge stays `Online`, never `Starting`.
- The set of ProcessIds is identical before and after.
- Ada's tile avatar shows `AB` after step 3.

**Fail if — any of these**

- The badge flickers `Starting` -> `Online`, or a ProcessId changes -> an avatar change restarted the session and wiped that agent's conversation memory; Avatar is kept off the Persona record exactly so this cannot happen (`AgentProfileCard.razor:1841-1848`).
- The tile still shows the monogram after the save -> the avatar was not stored.
- An error appears on the card -> an avatar-only save is being treated as a failed update.

**Inconclusive if**

If the badge is never `Online` to begin with, this test cannot distinguish a restart from a failed start — get Ada genuinely `Online` first or mark blocked.

> [!NOTE]
> Free: a restart sends no prompt, and the mock never bills. AGENTPROFILECARD-46 covers the text no-op; this one covers the avatar half.

---

Back to [the manual test script](../manual-tests.md).
