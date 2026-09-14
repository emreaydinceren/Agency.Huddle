# Teammate card: view, edit, create, delete

Prove that the one Teammate card (`TeammateCard.razor`, rendered over `/teammates` in View, Edit and Create modes) is the only safe way to create, edit and delete a Persona file on disk — and that it fails loudly, not silently. Almost nothing here is provable by the compiler or the test suite: the create-card open path is untested and calls a helper that throws on a blank name; validation messages arrive from three layers in a fixed precedence; a save rewrites YAML frontmatter in a file whose NAME never changes; and delete-then-recreate is only provable by reading `App_Data\Teams\*.md` and `App_Data\team.db` alongside the screen. Every test below is free unless it says otherwise; exactly one test spends money.

**50 tests** · 49 free, 1 paid 💰 · about 6.3 hours.

Read [the manual test script](../manual-tests.md) first — it carries the cost guard, the Model
and Effort convention, and the rules for concluding a result. This page assumes all three.

## Setup

1. Stop any running Agency.Huddle instance (Ctrl+C in its console) and confirm nothing answers at http://localhost:5100.
2. BACK UP `E:\Repos\Huddle\src\Huddle.App\App_Data` to a safe folder before anything else. Tests in this area delete Persona files and rows from `team.db`; this copy is the only undo.
3. Delete every `.md` file under `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams`, including any sub-folders, so the area starts from the shipped empty state. Do NOT delete `team.db`.
4. From `E:\Repos\Huddle`, run `dotnet build Huddle.slnx`. It must succeed before any test runs. If it fails, stop and report the build failure — every result below would be inconclusive.
5. Install a SQLite command-line client: `sqlite3` is NOT on PATH on this machine. Run `winget install --id SQLite.SQLite`, open a NEW terminal, and confirm `sqlite3 -version` prints a version. (DB Browser for SQLite is an acceptable substitute.) Tests TEAMMATECARD-35 onward need it.
6. Confirm `node --version` prints a version. Tests TEAMMATECARD-37 onward need a node process spawn to be observable.
7. Start the app with ACP explicitly OFF. This is NOT the default — `launchSettings.json` sets `ASPNETCORE_ENVIRONMENT=Development` and `appsettings.Development.json` sets `Team:Acp:Enabled=true`, so a plain `dotnet run` turns on the money-spending path. Run exactly: `dotnet run --project E:\Repos\Huddle\src\Huddle.App -- --Team:Acp:Enabled=false`
8. Keep that console window visible for the whole session. Its stdout is a stated oracle in many tests below.
9. Open http://localhost:5100/teammates in a browser. Keep a second window open on `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams` in File Explorer, and a text editor able to show line endings (VS Code, Notepad++).
10. STANDING MODEL/EFFORT CONVENTION: whenever a test says to choose a Model, choose Haiku. Whenever it says to choose an Effort, choose low. The only model switch any test asks for is Haiku -> Sonnet; the only effort switch is low -> medium. Never select Opus, high, xhigh or max.
11. Two identifiers in this app deliberately keep an old code name: configuration keys begin `Team:` and tool names begin `mcp__team__`. That is correct — do not report it as a typo or a branding bug.
12. The demo agents `echo` and `alpha` are pipe clients, not Personas. They appear in the Rooms sidebar and must NEVER appear on /teammates. Do not report their absence from the Teammates page as a defect.
13. After each test, delete any Persona file it created from `App_Data\Teams` (and its sub-folders) unless the next test says to keep it, so tests do not leak state into one another.

## Tests

### TEAMMATECARD-01 — Empty Teammates page and the New teammate entry point

**Free** · about 3 min

*Proves the page's zero-Persona state renders the empty message, hides the Team filter entirely, and never lists the demo pipe clients.*

**Before you start**

- The app is running with `--Team:Acp:Enabled=false`.
- `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams` contains no `.md` file anywhere, including sub-folders.

**Steps**

1. Confirm in File Explorer that `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams` holds no `.md` file at any depth.
2. Navigate to http://localhost:5100/teammates .
3. Read the top of the page.
4. Look for a `<select>` labelled **Team** anywhere on the page.
5. Read the body text below the intro paragraph.
6. Scan the whole page for the words `echo` or `alpha`.

**Pass if — all of these**

- A heading reads exactly `Teammates`, with a button labelled `New teammate` at the top right.
- An intro paragraph begins `A Persona is a Markdown file describing how one teammate should behave, plus the model it thinks with.`
- The body reads `No Personas yet. Choose New teammate to add one.` with `New teammate` in bold.
- No **Team** filter select is rendered at all.
- Neither `echo` nor `alpha` appears anywhere on the page.

**Fail if — any of these**

- A **Team** filter select is rendered while the list is empty -> the filter is no longer inside the non-empty branch and will offer an empty choice list.
- The empty-state sentence is missing or replaced by a blank area -> a user with a genuinely empty Teams folder is given no next action and no explanation.
- `echo` or `alpha` is listed as a teammate -> the page is reading the Agent directory instead of the Persona index; those are pipe clients with no Persona file, so every card action on them would fail.

**Inconclusive if**

If the page shows tiles, a Persona file still exists somewhere under `Teams` (check sub-folders). Delete them, wait ~1 second for the page to repaint, and repeat. If the page shows a `Files that didn't load` section, a non-Persona `.md` is present — remove it and repeat. If the browser shows nothing at all, the app is not running: restart it with the setup command and retry.

> [!NOTE]
> Run this first; it is the baseline every later test assumes.

### TEAMMATECARD-02 — Clicking New teammate opens a card at all (blank-name crash probe)

**Free** · about 6 min

*Proves the Create card can be opened with an empty Name — the page passes the card a status resolved from the current name, and the resolver throws on a blank one.*

**Before you start**

- The app is running with `--Team:Acp:Enabled=false`.
- The app console is visible.

**Steps**

1. Go to http://localhost:5100/teammates .
2. Click `New teammate`.
3. Observe immediately whether an overlay card headed `New teammate` appears, or whether a pale-yellow bar appears at the bottom of the page reading `An unhandled error has occurred.` with a `Reload` link.
4. Read the app console and copy any exception text printed in the last few seconds.
5. Reload the browser page and click `New teammate` a second time to confirm the outcome reproduces.
6. If the card DID open: type `Nova` into the **Name** field, then select all of it and delete it so the field is empty again. Observe the card and the console.
7. If the card is still alive: type a single space into the **Name** field. Observe the card and the console again.

**Pass if — all of these**

- An overlay card headed `New teammate` appears on both attempts.
- Clearing the **Name** field back to empty leaves the card on screen and working.
- Typing a single space as the whole **Name** leaves the card on screen and working.
- The console prints no exception during any of the above.

**Fail if — any of these**

- No card appears and the yellow bar `An unhandled error has occurred.` with a `Reload` link is shown -> the Blazor circuit was killed during render; the page passes `ResolveStatus(cardName)` to the card unconditionally, and with an empty name that reaches a guard that throws `ArgumentException` on `personaName`. Creating a teammate is impossible at all.
- The card opens but dies the moment the Name is cleared or set to a single space -> the same unguarded call, reached mid-typing; a user who backspaces over their typo loses the whole form.
- The console shows `ArgumentException` naming `personaName` raised through `Agency.Huddle.App.Acp.PersonaHealth.Get` and `Agency.Huddle.App.Components.Pages.Teammates.ResolveStatus` -> confirms the above diagnosis; record the stack verbatim.

**Inconclusive if**

If the yellow bar appears but the console shows an unrelated exception (a database lock, a disposed object), this test says nothing about the blank-name path — record the real exception and mark inconclusive. If the browser had a stale circuit from before an app restart, reload the page once and retry before judging.

> [!NOTE]
> This is the highest-value test in the area and the cheapest. If it fails, every Create test (05-11, 18-21, 27, 28, 33) is blocked; run the Edit/View/Remove tests against hand-placed Persona files instead and report the block explicitly.

### TEAMMATECARD-03 — Create card: every label, placeholder and hint

**Free** · about 6 min

*Proves the Create card shows the create-specific wording and no status line, so a user is never told to type frontmatter they must not type.*

**Before you start**

- TEAMMATECARD-02 passed, so the Create card can be opened.

**Steps**

1. Click `New teammate`.
2. Read the card's header text and hover the small button on its right.
3. Read the four text inputs from top to bottom, noting each label, each placeholder, and the small hint under each.
4. Read the label and placeholder on the large multi-line box below them, and the hint under it.
5. Read the labels of the two `<select>` controls below that.
6. Read the two buttons at the bottom of the card.
7. Scan the whole card for a coloured status dot, or any of the words `Online`, `Offline`, `Starting`, `Degraded`.

**Pass if — all of these**

- The header reads `New teammate`; the button on its right shows `×` and has the tooltip/aria-label `Close`.
- A circular avatar showing a monogram is present at the top left of the card body.
- The four inputs are, in order: **Name** (placeholder `Chief of Staff`, hint `Letters, digits, spaces, - and _. Spaces are fine.`), **Title** (placeholder `Chief of Staff`, hint `A short role description, shown alongside the Name.`), **Alias** (placeholder `coo`, hint `A short working handle, for mentions and quick reference.`), **Teams** (placeholder `Business, Household`, hint `Comma-separated. Leave blank for no team.`).
- The multi-line box is labelled `Persona body`, has placeholder `You are the Chief of Staff. You keep the team honest.`, and its hint reads `Markdown. Becomes the teammate's system prompt, placed after the Name, Title, Alias and Teams above — no front matter needed here.`
- The two selects are labelled `Model` and `Effort`, in that order.
- The buttons read `Add teammate` (styled as the primary action) and `Cancel`.
- No status dot and no status word appears anywhere on the card.

**Fail if — any of these**

- The multi-line box is labelled `Persona text` and/or carries the hint `This is the whole Persona file, front matter included` -> the Edit wording leaked into Create; a user following it would type a `---` block that the Create path composes a second time, producing a file that will not load.
- A status dot or an `Offline` label appears in Create mode -> the status block is no longer guarded to the non-Create branch, and it is reporting the health of a teammate that does not exist yet.
- Any of the four hints is missing -> the only on-screen guidance for a field with strict validation is gone.

**Inconclusive if**

If the card does not open at all, this test is blocked by TEAMMATECARD-02 — record it as blocked, not failed. If a hint is visually truncated rather than absent, widen the browser window before judging.

> [!NOTE]
> The **Name** hint is genuinely incomplete — it never mentions the single-interior-space rule, the must-start-with-a-letter-or-digit rule, or the 64-character cap. Record that as a documentation gap, not a failure of this test; the validation itself is checked in TEAMMATECARD-05.

### TEAMMATECARD-04 — Monogram tracks the Name as you type

**Free** · about 3 min

*Proves the Name input round-trips to the page and back, and that the monogram takes first-and-last word rather than the first two.*

**Before you start**

- The Create card can be opened.

**Steps**

1. Click `New teammate` and look at the circular avatar with the **Name** field empty.
2. Type `echo` into **Name** and read the avatar.
3. Clear the field, type `Emily Lee`, and read the avatar.
4. Clear the field, type `Chief of Staff`, and read the avatar.

**Pass if — all of these**

- Empty Name shows `?`.
- `echo` shows `E`.
- `Emily Lee` shows `EL`.
- `Chief of Staff` shows `CS`.

**Fail if — any of these**

- The avatar stays on `?` however much you type -> the Name binding no longer round-trips from the card to the page; every field on the card is bound the same way, so a save would write the wrong (or empty) identity.
- `Chief of Staff` shows `CO` -> the monogram takes the first two words instead of the first and last, giving a middle word like `of` a letter it does not deserve.

**Inconclusive if**

If clearing the Name kills the card (see TEAMMATECARD-02), do the three positive cases in separate card openings instead of clearing, and record why.

### TEAMMATECARD-05 — Create: the exact Name accept/reject set

**Free** · about 15 min

*Proves the Name validation still rejects everything that would produce two teammates a reader cannot tell apart, or a filename that escapes the Teams folder.*

**Before you start**

- The Create card can be opened.
- `App_Data\Teams` is empty.

**Steps**

1. Click `New teammate`. Fill **Title** with `T`, **Alias** with `a1`, and the **Persona body** with `x`. Leave **Model** and **Effort** on their blank option.
2. Set **Name** to each of the following in turn and click `Add teammate` after each, reading the red error line at the top of the card body and then correcting the Name for the next attempt. REJECT SET, in order: (1) leave empty, (2) ` Nova` (one leading space), (3) `Nova ` (one trailing space), (4) `Emily  Lee` (two spaces between the words), (5) `Nova.1`, (6) `-nova`, (7) `_nova`, (8) `Nova!`, (9) `Ünal`, (10) `AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGGG` (65 characters).
3. After all ten rejects, check File Explorer: `App_Data\Teams` must still contain no `.md` file.
4. Now the ACCEPT set. For each of these, set **Name** to the value, set **Alias** to the matching unique handle, click `Add teammate`, confirm the card switches to View mode, close the card, delete the created `.md` file from `App_Data\Teams`, and click `New teammate` again for the next one: `Nova` (alias `nv1`), `Emily Lee` (alias `nv2`), `1Nova` (alias `nv3`), `nova-2` (alias `nv4`), `nova_2` (alias `nv5`), `AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGG` (64 characters, alias `nv6`).
5. Confirm `App_Data\Teams` is empty again when you finish.

**Pass if — all of these**

- Every one of the ten reject values produces the single red line `'<exactly what you typed>' is not a valid Persona name.` and the card stays open.
- No `.md` file appears under `App_Data\Teams` for any rejected attempt.
- Every one of the six accept values saves successfully and creates one `.md` file named after it.

**Fail if — any of these**

- ` Nova`, `Nova ` or `Emily  Lee` is accepted -> a leading, trailing or doubled space now produces two teammates a reader cannot visually distinguish, and Windows silently strips a trailing space from a filename so two names would resolve to one file.
- `Nova.1` is accepted -> a dot reached the Name, which becomes the filename `{Name}.md`; this rule is the path-traversal guard, so its loss is a security regression, not a cosmetic one.
- `Ünal` or `Nova!` is accepted -> the Name character set widened beyond what Mention matching and filenames can carry.
- The 65-character name is accepted -> the length cap is gone.
- Any accepted value in step 4 is rejected -> the rule tightened; `Emily Lee` in particular must stay legal, a Name is a display name.

**Inconclusive if**

If the card dies on the empty-Name case (TEAMMATECARD-02), skip reject case (1), record it as blocked, and run the other nine. If an accept case fails with `Persona 'X' already exists.` you did not delete the previous file — delete it and retry that one value.

> [!NOTE]
> Do the whole reject set before the accept set: rejects write nothing, so they need no cleanup between attempts.

### TEAMMATECARD-06 — Create: Alias validation uses the same rules and its own wording

**Free** · about 7 min

*Proves an invalid Alias is reported as an alias, not a name — the wording is the only way a user can tell which field failed.*

**Before you start**

- The Create card can be opened.
- `App_Data\Teams` is empty.

**Steps**

1. Click `New teammate`. Set **Name** to `Nova`, **Title** to `T`, **Persona body** to `x`.
2. Set **Alias** to each of these in turn and click `Add teammate` after each, reading the red line: leave empty, then ` jar` (leading space), then `jar jar ` (trailing space), then `jar.1`, then `-jar`.
3. Confirm `App_Data\Teams` still contains no `.md` file.
4. Set **Alias** to `jar` and click `Add teammate`. Confirm it saves, then close the card and delete the created file.
5. Repeat step 4 for **Alias** `Jar Jar`, then for **Alias** `jar-1`, deleting the created file each time.

**Pass if — all of these**

- Each of the five invalid aliases produces the single red line `'<exactly what you typed>' is not a valid Persona alias.` — the word is `alias`, not `name`.
- No `.md` file appears for any rejected attempt.
- `jar`, `Jar Jar` and `jar-1` each save successfully.

**Fail if — any of these**

- The message says `is not a valid Persona name.` while the Name is valid and the Alias is not -> the two guards' wording has been merged; a user is sent to fix the wrong field.
- An empty Alias is accepted -> an Alias is required, and an Alias is accepted anywhere a Name is, so a blank one leaves a teammate unmentionable and the frontmatter incomplete.
- A dotted or space-padded alias is accepted -> the Alias no longer shares the Name rules, and `@jar.1` becomes unresolvable at Mention time with no error.

**Inconclusive if**

If the empty-Alias case shows the Name error instead, first re-read the Name field — the Name guard runs first and wins (see TEAMMATECARD-07). Only judge this test when the Name is known-valid.

### TEAMMATECARD-07 — Create: validation precedence — which error wins

**Free** · about 6 min

*Proves the fixed error order (Name, then Alias, then body, then frontmatter) so a user fixing one problem is never sent to the wrong field.*

**Before you start**

- The Create card can be opened.
- `App_Data\Teams` is empty.

**Steps**

1. Click `New teammate`. Leave **Name**, **Title**, **Alias**, **Teams** and **Persona body** all empty.
2. Click `Add teammate` and read the red line.
3. Set **Name** to `Nova`. Click `Add teammate` and read the red line.
4. Set **Alias** to `nov`. Click `Add teammate` and read the red line.
5. Set **Persona body** to `You are Nova.`, leave **Title** still empty. Click `Add teammate` and read the red line.
6. Confirm at every step that only ONE red line is visible at a time, and that `App_Data\Teams` is still empty.

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

If step 2 kills the circuit rather than showing an error, that is TEAMMATECARD-02's failure, not this one — start this test from step 3 with a valid Name already typed and note the reduced coverage.

> [!NOTE]
> One error at a time is by design. A tester expecting a per-field error list should NOT file that as a bug — only a wrong error winning is a bug.

### TEAMMATECARD-08 — Create: the Title is prose — blank rejected, commas preserved

**Free** · about 6 min

*Proves the Title is validated only for being non-blank and is never split on commas, unlike the Teams field.*

**Before you start**

- The Create card can be opened.
- `App_Data\Teams` is empty.

**Steps**

1. Click `New teammate`. Set **Name** `Nova`, **Alias** `nov`, **Persona body** `You are Nova.`, **Title** empty.
2. Click `Add teammate` and read the red line.
3. Set **Title** to three spaces and click `Add teammate` again. Read the red line.
4. Set **Title** to exactly `Router, triage, and cross-workstation continuity` and click `Add teammate`.
5. Read the second line of the resulting View card (immediately under the name heading).
6. Open `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova.md` in a text editor and read line 3.
7. Close the card and delete `Nova.md`.

**Pass if — all of these**

- Steps 2 and 3 both show `Persona frontmatter is missing required field 'Title'.`
- Step 4 saves successfully.
- The View card's second line reads exactly `Router, triage, and cross-workstation continuity`, commas and all.
- Line 3 of `Nova.md` is exactly `title: 'Router, triage, and cross-workstation continuity'` — one line, single-quoted, commas intact.

**Fail if — any of these**

- A whitespace-only Title is accepted -> a required field has nothing meaningful in it and the teammate presents with a blank role line everywhere.
- The Title is split at the commas, rendered as a list, or truncated at the first comma -> the generic frontmatter reader started guessing lists from comma content; only the `teams` key may ever be comma-split, precisely so a prose field survives.
- The Title appears on the card but the file's `title:` line is broken across two lines -> the composed scalar is not quoted and the file will fail to reload.

**Inconclusive if**

If the save is refused with a Name or Alias error, the precedence rule is masking this test — fix those fields first (TEAMMATECARD-07) and repeat.

### TEAMMATECARD-09 — Create: a failed save never throws away what you typed

**Free** · about 5 min

*Proves an error keeps the card open with every field, both selects and the whole body intact.*

**Before you start**

- The Create card can be opened.

**Steps**

1. Click `New teammate`.
2. Set **Name** `Atlas`, **Title** `Ops Lead`, **Teams** `Business, Household`.
3. Type this exact multi-line text into **Persona body**: `You are Atlas.` then a new line, then `You keep the team honest.` then a new line, then `MARKER-9F3A.`
4. If the **Model** select offers options, choose Haiku; if it offers only the blank option, leave it. Do the same for **Effort** with low.
5. Set **Alias** to `jar.1`.
6. Click `Add teammate`.
7. Without touching anything else, read every field on the card and note the selected option in both selects.
8. Change **Alias** to `atl` and click `Add teammate`.
9. Read the resulting View card's `Persona` block.
10. Close the card and delete the created file.

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

### TEAMMATECARD-10 — Enter in an identity field does not submit, and saving never reloads the page

**Free** · about 4 min

*Proves the identity inputs sit outside the form and that saving is a Blazor event, not a page navigation.*

**Before you start**

- The Create card can be opened.

**Steps**

1. Click `New teammate`. Fill **Name** `Nova`, **Title** `T`, **Alias** `nov`, **Persona body** `x`.
2. Click into the **Name** input and press Enter. Observe.
3. Repeat in the **Title**, **Alias** and **Teams** inputs.
4. Click into the **Persona body** box, press Enter, and observe.
5. Watch the browser's tab spinner / reload indicator, then click `Add teammate`.
6. Close the card and delete the created file.

**Pass if — all of these**

- Pressing Enter in any of the four identity inputs does nothing at all: no save, no error, no navigation.
- Pressing Enter in **Persona body** inserts a newline.
- Clicking `Add teammate` saves without the browser showing a page reload.

**Fail if — any of these**

- Enter in the **Name** field submits the form -> a half-filled card can be saved by a stray keypress.
- Clicking `Add teammate` reloads the page (spinner, flash, the card gone) -> the form is doing a native POST; the card and everything typed in it are lost on every save.

**Inconclusive if**

If the browser reload indicator is too fast to see, open DevTools > Network, check `Preserve log`, and confirm no document-type request appears when you click `Add teammate`.

> [!NOTE]
> Enter not submitting is expected: the four identity inputs genuinely sit above and outside the `<form>` element. Record it as an ergonomic gap if you like, but it is not a defect.

### TEAMMATECARD-11 — A successful create lands on the new teammate's View card

**Free** · about 5 min

*Proves a save does not close the card but switches it to View for the teammate just written, so the save is visibly confirmed.*

**Before you start**

- The Create card can be opened.
- `App_Data\Teams` is empty.

**Steps**

1. Click `New teammate`. Set **Name** `Nova`, **Title** `Ops Lead`, **Alias** `nov`, **Teams** `Business`, **Persona body** `You are Nova.`
2. Click `Add teammate`.
3. Read the card header, the identity block and every section heading in the card body.
4. Look behind the overlay at the list on the page.
5. Confirm `App_Data\Teams\Nova.md` exists in File Explorer.
6. Leave the card and the file in place — TEAMMATECARD-12 continues from here.

**Pass if — all of these**

- The card stays open and its header now reads `Teammate`.
- The card body shows `Nova` as a heading, `Ops Lead` beneath it, a status dot with a status word, `Alias: nov`, `Teams: Business`, and the sections `Persona`, `Model`, `Effort`, `Persona file`.
- Behind the overlay a new tile for `Nova` has appeared under a `Business` heading.
- `App_Data\Teams\Nova.md` exists.

**Fail if — any of these**

- The card closes outright on save -> you cannot confirm what was written without reopening; the create flow gives no receipt.
- The View card shows blank Name/Title/Alias -> the page re-opened the card before refreshing its index, so it could not find the entry the save had just written.
- The file exists but no tile appears -> the list did not refresh off the write; a user would retry and hit `Persona 'Nova' already exists.`

**Inconclusive if**

If the tile appears but the Teams heading reads `No team`, check the Teams input actually contained `Business` — an empty Teams field legitimately groups under `No team` (TEAMMATECARD-17).

### TEAMMATECARD-12 — View card: every section and action, in order

**Free** · about 7 min

*Proves the View card shows the whole raw file, the real discovered path, and exactly the actions that apply to the teammate's current state.*

**Before you start**

- `Nova` exists from TEAMMATECARD-11.
- The app is running with `--Team:Acp:Enabled=false`, so every teammate reads Offline.

**Steps**

1. If no card is open, click the `Nova` tile.
2. Read the card header.
3. Read the identity block top to bottom: monogram, name, title, status dot and word, any reason line, `Alias:` line, `Teams:` line.
4. Read the row of actions under the identity block, left to right.
5. Read any hint text directly under that row.
6. Read the four section headings and their contents, especially the block under `Persona`.
7. Compare the text under `Persona` character for character with the contents of `App_Data\Teams\Nova.md` opened in a text editor.
8. Read the path shown under `Persona file`.

**Pass if — all of these**

- The header reads exactly `Teammate` — not the teammate's name.
- The identity block shows `Nova`, `Ops Lead`, a dot plus the word `Offline`, `Alias: nov`, `Teams: Business`.
- The actions are, in order: `Edit`, `Open`, `Restart`, `Remove`. (`Message` is absent — see TEAMMATECARD-44.)
- Under the actions the hint reads `Like saving an edit, this restarts the teammate, which clears what it remembers.`
- The sections are `Persona`, `Model`, `Effort`, `Persona file`, in that order.
- The `Persona` block contains the WHOLE file including both `---` delimiter lines and the `name:`/`title:`/`alias:`/`teams:` lines.
- `Persona file` shows the absolute path `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova.md`.

**Fail if — any of these**

- The `Persona` block shows only the body, without the `---` block -> the Edit textarea is fed from the same value, so an Edit-then-Save would strip the frontmatter and the teammate would vanish into `Files that didn't load`.
- The header shows `Nova` instead of `Teammate` -> the card title binding changed mode.
- Any field renders the literal text `this.cardName` or similar -> a component attribute lost its leading `@` and is being passed as a string literal; the compiler cannot catch this and it has shipped in this file before.
- `Restart` is absent while the status reads `Offline` -> the one action that can fix a stopped teammate is unreachable.

**Inconclusive if**

If the status reads `Online` or `Starting`, the app was started without `--Team:Acp:Enabled=false`; restart it with the setup command and repeat, because the `Restart` button and its hint are correctly hidden when healthy (TEAMMATECARD-45).

### TEAMMATECARD-13 — View card: 'Agent default' and 'Model default' when nothing is stored

**Free** · about 3 min

*Proves the two unset states use two different, correct words rather than an empty paragraph.*

**Before you start**

- `Nova` exists and was created with both selects left on their blank option.

**Steps**

1. Open `Nova`'s card by clicking its tile.
2. Read the paragraph under the `Model` heading.
3. Read the paragraph under the `Effort` heading.

**Pass if — all of these**

- `Model` reads exactly `Agent default`.
- `Effort` reads exactly `Model default`.
- Neither paragraph is blank.

**Fail if — any of these**

- Either section is empty -> an unset choice renders as nothing and a user cannot tell 'no choice made' from 'the card failed to load it'.
- Both read the same words -> the distinction is lost; a model is resolved by the agent and an effort by the model, and the two defaults are genuinely different things.

**Inconclusive if**

If `Nova` was created with a Model chosen, delete it and re-create it leaving both selects blank, then repeat.

> [!NOTE]
> The Effort SELECT's blank option is labelled `Use the agent's default` while this View section says `Model default`. That inconsistency is real and worth recording once, but it is a known wording gap, not a failure of this test.

### TEAMMATECARD-14 — Closing the card: backdrop, ×, Cancel — and what is deliberately absent

**Free** · about 6 min

*Proves the backdrop closes the card while a click inside it does not, and records the deliberate absence of Escape and focus trapping.*

**Before you start**

- `Nova` exists.

**Steps**

1. Click the `Nova` tile to open the View card.
2. Click the dimmed area well outside the white panel. Observe.
3. Reopen the card. Click on the card header text, then on the avatar, then on a hint line — each a click INSIDE the panel. Observe after each.
4. Click the `×` button at the top right. Observe.
5. Reopen the card, click `Edit`, then click `Cancel`. Observe.
6. Reopen the card and press the Escape key. Observe.
7. Reopen the card and press Tab repeatedly, watching where the focus ring goes.
8. Open the browser's element inspector on the white panel and read its `role`, `aria-modal` and `aria-label` attributes.

**Pass if — all of these**

- Clicking the dimmed backdrop closes the card.
- Clicking anywhere inside the white panel does NOT close it.
- `×` closes the card; `Cancel` in Edit closes it and discards.
- The panel carries `role="dialog"`, `aria-modal="true"` and an `aria-label` equal to the header text (`Teammate`, `Edit Nova` or `New teammate`).

**Fail if — any of these**

- A click inside the panel closes the card -> the click-swallowing on the panel regressed; in Edit or Create this destroys everything typed with one misplaced click.
- `Cancel` in Edit saves instead of discarding -> a user backing out writes to disk.
- The panel is missing `aria-label` -> a screen reader announces an unnamed dialog.

**Inconclusive if**

If the Escape key appears to close the card, check you did not also click the backdrop — then record it as a CHANGE (new behaviour), not a failure.

> [!NOTE]
> Escape not closing the card and focus not being trapped are BOTH deliberate: the card is a plain overlay `<div>`, not a `<dialog>`, because a real dialog needs `showModal()` from JavaScript and nothing else on this page needs interop. Record what you observe but do NOT file either as a defect.

### TEAMMATECARD-15 — Closing a card clears every bit of its state

**Free** · about 7 min

*Proves no field, no error and no pending Remove confirmation survives a close — a leftover Name would be used to create the wrong teammate, and a leftover Confirm is a one-click accidental delete.*

**Before you start**

- `Nova` exists.
- The Create card can be opened.

**Steps**

1. Click `New teammate`. Fill **Name** `Atlas`, **Title** `Ops`, **Alias** `atl`, **Teams** `Business`, **Persona body** `You are Atlas.`; if the selects offer options, choose Haiku and low.
2. Click `Cancel`.
3. Click `New teammate` again and inspect all four inputs, the body box, both selects and the top of the card body.
4. Close the card. Click the `Nova` tile, then click `Remove` so the buttons `Confirm` and `Cancel` are showing.
5. Close the card by clicking the dimmed backdrop — do NOT click `Confirm` or `Cancel`.
6. Click the `Nova` tile again and read the action row.
7. With `Nova`'s card open, click `Edit`, clear the whole text box, click `Save` so a red error appears, then close the card with `×`.
8. Click the `Nova` tile again and look for the red error line.

**Pass if — all of these**

- On reopening Create, all four inputs and the body box are empty, both selects show `Use the agent's default`, and no red error line is showing.
- After reopening `Nova`, the action row shows a single `Remove` button — not `Confirm`.
- After reopening `Nova` following the errored Edit, no red error line is showing.

**Fail if — any of these**

- Any typed value carries over into the next card -> most dangerously a leftover Name, which a Create would then use for a different teammate entirely.
- The reopened card still shows `Confirm` -> one stray click now deletes a Persona file with no further warning.
- A stale error line persists on a freshly opened card -> the user is shown a failure that belongs to a different card.

**Inconclusive if**

If step 7's Save succeeds instead of erroring, you did not fully clear the text box — clear it completely (Ctrl+A, Delete) and retry; an empty Persona text must be refused.

### TEAMMATECARD-16 — Remove is a two-step confirm and Cancel backs out cleanly

**Free** · about 5 min

*Proves deleting a Persona file always takes two deliberate clicks and that backing out changes nothing.*

**Before you start**

- `Nova` exists at `App_Data\Teams\Nova.md`.

**Steps**

1. Click the `Nova` tile.
2. Click `Remove` and read the action row.
3. Click `Cancel` and read the action row again.
4. Confirm in File Explorer that `Nova.md` is still present and its modified time is unchanged.
5. Click `Remove`, then click `Confirm`.
6. Observe the card and the list behind it.
7. Confirm in File Explorer that `Nova.md` is gone.

**Pass if — all of these**

- Clicking `Remove` replaces it with two buttons, `Confirm` (styled as the danger action) and `Cancel`.
- Clicking `Cancel` collapses them back to a single `Remove` and the card stays open.
- `Nova.md` is untouched after the Cancel.
- Clicking `Confirm` closes the card and the `Nova` tile disappears from the list.
- `Nova.md` no longer exists.

**Fail if — any of these**

- `Remove` deletes on a single click -> there is no confirmation step at all on the only destructive action in the app.
- `Cancel` closes the card instead of only cancelling the removal -> a user loses their place and may not realise nothing was deleted.
- The file is gone after the Cancel -> the delete ran before the confirmation.

**Inconclusive if**

If `Confirm` produces a red line `Persona 'Nova' does not exist.`, the file was removed outside the app between steps — recreate `Nova` and repeat.

> [!NOTE]
> Recreate `Nova` (Name `Nova`, Title `Ops Lead`, Alias `nov`, Teams `Business`, body `You are Nova.`) before continuing to later tests that assume it exists.

### TEAMMATECARD-17 — Team headings and the Team filter come from the teams field, not the folder

**Free** · about 10 min

*Proves Team membership is read only from frontmatter, that a two-team teammate appears twice, and that selecting a team never spawns an adapter.*

**Before you start**

- The Create card can be opened.
- `App_Data\Teams` is empty.
- A PowerShell window is available to run `Get-Process node`.

**Steps**

1. Create three teammates from the card, deleting nothing in between: (a) Name `Nova`, Title `T`, Alias `nov`, Teams `Business`, body `x`; (b) Name `Atlas`, Title `T`, Alias `atl`, Teams left EMPTY, body `x`; (c) Name `Iris`, Title `T`, Alias `iri`, Teams `Business, Household`, body `x`.
2. Close the card and read the group headings and which tiles sit under each.
3. Run `Get-Process node` in PowerShell and note the result.
4. Open the **Team** select and read its options.
5. Select `Business`, then run `Get-Process node` again.
6. Select `Household`, then `All teams`, reading the list after each.
7. In File Explorer create the folder `App_Data\Teams\Household` and MOVE `Nova.md` into it, without editing the file.
8. Wait about one second, then read the group headings again.

**Pass if — all of these**

- Headings show `Business` containing `Nova` and `Iris`, `Household` containing `Iris`, and `No team` containing `Atlas`. `Iris` appears once under each of its two teams.
- The **Team** select lists `All teams` plus `Business` and `Household`.
- Selecting a team narrows the page to that one heading; `All teams` restores all of them.
- `Get-Process node` returns the same result before and after changing the filter (no new node process).
- After moving `Nova.md` into the `Household` folder, `Nova` is STILL under the `Business` heading.

**Fail if — any of these**

- Moving the file changed which heading `Nova` sits under -> grouping is following the sub-folder instead of the `teams:` frontmatter; sub-folders are the human's filing system and the code must never read meaning into them.
- `Iris` appears under only one of its two teams -> a multi-team Persona is being collapsed.
- `Atlas` is missing entirely rather than under `No team` -> a teammate with no team has become invisible.
- A node process appears when you change the filter -> the filter is touching the model catalog, spawning an adapter for a pure display change.

**Inconclusive if**

If a team with members shows `No teammates in this team.`, first confirm the frontmatter really carries that team (open the `.md`); a typo in the Teams input produces a genuinely empty team. If `Get-Process node` errors with `Cannot find a process with the name "node"`, that is the zero-process result — treat it as zero, not as an error.

> [!NOTE]
> Leave `Nova`, `Atlas` and `Iris` in place if you are running TEAMMATECARD-18 next; otherwise delete all three files and the `Household` folder.

### TEAMMATECARD-18 — Create: exactly what lands on disk

**Free** · about 8 min

*Proves a created file is written at the Teams ROOT as `{Name}.md`, with canonical lowercase single-quoted frontmatter and the body unchanged — and that the Model and Effort never reach the file.*

**Before you start**

- The Create card can be opened.
- `App_Data\Teams` is empty and writable.

**Steps**

1. Click `New teammate`.
2. Set **Name** `Nova`, **Title** `Ops Lead`, **Alias** `nov`, **Teams** `Business`.
3. Type into **Persona body** exactly: `You are Nova.` then a newline then `You keep the team honest.`
4. If the **Model** select offers options, choose Haiku and set **Effort** to low. If it offers only the blank option, leave both blank and note it.
5. Click `Add teammate`.
6. In File Explorer, confirm the new file is at `App_Data\Teams\Nova.md` — at the Teams root, NOT inside any sub-folder.
7. Open `Nova.md` in a text editor configured to show line endings (in VS Code, the indicator at the bottom right).
8. Read all seven lines and the line-ending indicator.
9. Search the file for the words `haiku`, `model` and `effort`.

**Pass if — all of these**

- The file is at `App_Data\Teams\Nova.md`, at the Teams root.
- Its contents are exactly, in order: `---`, `name: 'Nova'`, `title: 'Ops Lead'`, `alias: 'nov'`, `teams: ['Business']`, `---`, `You are Nova.`, `You keep the team honest.`
- Every frontmatter key is lowercase; every scalar is single-quoted; `teams` is a bracketed flow list.
- The editor reports LF line endings.
- The file contains no `model:` or `effort:` line and no model id.

**Fail if — any of these**

- The file is named anything other than `Nova.md`, or sits inside a Team sub-folder -> the app guessed a folder for a new file, which it must never do; the filename must always be `{Name}.md` at the root.
- A frontmatter key is capitalised or a scalar is unquoted -> the composed file no longer matches the shape the reader accepts; the next edit may fail to reload it.
- The chosen Model or Effort appears in the file -> those belong only in `team.db`; a file carrying them will drift from the database and the two will disagree.
- The body is reordered, re-indented or missing a line -> the body is supposed to be written back verbatim.

**Inconclusive if**

If the Model select offers no options (no node, or the adapter needs authentication), the model-not-in-the-file assertion is untestable this run — record it as unverified and check TEAMMATECARD-40 instead once a catalog is available.

> [!NOTE]
> CRLF here would be a real difference: the create path joins its lines with LF.

### TEAMMATECARD-19 — Create: an apostrophe in the Title round-trips through YAML quoting

**Free** · about 5 min

*Proves the YAML escape is written on disk and stripped on screen, so a natural Title neither breaks the file nor leaks its escaping into the UI.*

**Before you start**

- The Create card can be opened.
- `App_Data\Teams` contains no `Nova.md`.

**Steps**

1. Click `New teammate`.
2. Set **Name** `Nova`, **Alias** `nov`, **Persona body** `x`.
3. Set **Title** to exactly `Chief of Staff's deputy` (one apostrophe).
4. Click `Add teammate`.
5. Read the line directly under the name heading on the resulting View card.
6. Open `App_Data\Teams\Nova.md` and read line 3 exactly.
7. Reload the browser page, click the `Nova` tile and re-read the title line.
8. Close the card and delete `Nova.md`.

**Pass if — all of these**

- The save succeeds with no error.
- The View card's second line reads exactly `Chief of Staff's deputy` — one apostrophe.
- Line 3 of the file is exactly `title: 'Chief of Staff''s deputy'` — TWO apostrophes on disk.
- After a full page reload the card still shows one apostrophe.

**Fail if — any of these**

- The card shows `Chief of Staff''s deputy` -> the YAML escape is leaking into the UI; the reader is not un-doubling what the writer doubled.
- The save fails with a frontmatter error -> the escape was not written, so the quoted scalar terminated early and the rest of the line was lost.
- The file shows a single apostrophe inside the quotes -> the file is invalid YAML and will be rejected on the next reload, dropping the teammate into `Files that didn't load`.

**Inconclusive if**

If your text editor auto-converts the apostrophe to a typographic quote (’), that is a different character and this test proves nothing — type it into the browser field, not into the file, and verify the field contains U+0027.

### TEAMMATECARD-20 — Create: the Teams field trims, drops blanks and collapses duplicates on read-back

**Free** · about 8 min

*Proves the comma parsing, and records the deliberate asymmetry where the file keeps a duplicate team but the screen shows one.*

**Before you start**

- The Create card can be opened.
- `App_Data\Teams` is empty.

**Steps**

1. Create a teammate: Name `Nova`, Title `T`, Alias `nov`, body `x`, **Teams** set to exactly ` Business ,, Household , ` (note leading, doubled and trailing commas and spaces).
2. Read the `Teams:` line on the resulting View card, then open `App_Data\Teams\Nova.md` and read its `teams:` line.
3. Close the card. Create a second teammate: Name `Atlas`, Title `T`, Alias `atl`, body `x`, **Teams** set to exactly `Ops, ops`.
4. Read the `Teams:` line on Atlas's View card, then read `Atlas.md`'s `teams:` line.
5. Close the card. Create a third teammate: Name `Iris`, Title `T`, Alias `iri`, body `x`, **Teams** left EMPTY.
6. Read Iris's View card and look for a `Teams:` line, then open `Iris.md` and look for a `teams:` line.
7. Delete all three files.

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

### TEAMMATECARD-21 — Create: a duplicate filename is caught before anything is written

**Free** · about 6 min

*Proves an existing Persona file can never be silently overwritten, including by a name differing only in case.*

**Before you start**

- `App_Data\Teams\Nova.md` exists (create it if needed: Name `Nova`, Title `Ops Lead`, Alias `nov`, body `You are Nova.`).

**Steps**

1. Note `Nova.md`'s exact modified timestamp and its contents in File Explorer / a text editor.
2. Click `New teammate`. Set **Name** `Nova`, **Title** `Different`, **Alias** `zzz`, body `Different body.`
3. Click `Add teammate` and read the red line.
4. Without closing the card, change **Name** to `nova` (all lowercase) and click `Add teammate` again. Read the red line.
5. Confirm the card is still open and every field still holds what you typed.
6. Check `Nova.md`'s modified timestamp and contents again.
7. Check whether any second file (`nova.md`) appeared in `App_Data\Teams`.

**Pass if — all of these**

- Both attempts show `Persona 'Nova' already exists.` / `Persona 'nova' already exists.` naming exactly the name you typed.
- The card stays open with all fields intact.
- `Nova.md`'s modified timestamp and contents are unchanged.
- No second file appeared.

**Fail if — any of these**

- `Nova.md`'s contents changed -> an existing teammate's Persona was silently overwritten; this is catastrophic, unrecoverable data loss with no warning.
- The lowercase `nova` attempt succeeds and a second file appears -> on Windows those two filenames are the same file, so one of them will win non-deterministically.
- The message names a different name than you typed -> the error is reporting the wrong thing and a user cannot tell which attempt collided.

**Inconclusive if**

On a case-sensitive filesystem the lowercase attempt would legitimately produce the collision message from the index instead (`Persona Name 'nova' is also used by ...`). This test runs on Windows, so treat any non-`already exists` message as a finding and record the exact text.

### TEAMMATECARD-22 — Edit card: what is editable and what deliberately is not

**Free** · about 6 min

*Proves the Edit textarea is a raw whole-file editor, correctly labelled and pre-filled, with no redundant identity inputs and no rename control.*

**Before you start**

- `Nova` exists at `App_Data\Teams\Nova.md`.

**Steps**

1. Click the `Nova` tile, note the path under `Persona file`, then click `Edit`.
2. Read the card header.
3. Read the identity area: what is a heading, what is text, and what (if anything) is an input.
4. Read the label of the large text box, and the hint under it.
5. Select all the text in the box and compare it character for character with the file at the noted path (paste into a diff tool if needed).
6. Read the buttons at the bottom.
7. Look for any control that renames the teammate.

**Pass if — all of these**

- The header reads `Edit Nova`.
- The identity area shows `Nova` as a read-only heading plus a status dot and word — no Title, Alias or Teams inputs, and no `Alias:` / `Teams:` lines.
- The text box is labelled `Persona text`.
- Its hint reads `Markdown. This is the whole Persona file, front matter included, and becomes the teammate's system prompt. Saving restarts it, which clears what it remembers.`
- The box contains the WHOLE file — both `---` lines and every frontmatter key — identical to the file on disk.
- The buttons are `Save` and `Cancel`.

**Fail if — any of these**

- The text box contains only the body without the `---` block -> saving would strip the frontmatter and the teammate would immediately drop into `Files that didn't load`, recoverable only in a text editor.
- Title / Alias / Teams inputs appear in Edit mode -> they can now disagree with the raw text in the same card, and one of the two has to lose silently.
- The box is labelled `Persona body` with the Create hint -> the user is told not to include front matter while editing a file that consists largely of it.

**Inconclusive if**

If the text box is empty, check the teammate still exists on disk — a file deleted underneath an open card produces an empty Edit (see TEAMMATECARD-31). Recreate it and repeat.

> [!NOTE]
> There is deliberately NO rename control. The only way to rename is editing the `name:` line inside this textarea (TEAMMATECARD-26). Do not file its absence as a defect.

### TEAMMATECARD-23 — Edit save writes back to the SAME file path — never a rename, never a move

**Free** · about 10 min

*Proves an edit rewrites the file at the path it was discovered at, even when the filename and folder do not match the Name — and records the LF normalisation.*

**Before you start**

- `App_Data\Teams` contains no other Persona named `Nova`.
- You can create files and folders under `App_Data\Teams`.

**Steps**

1. Stop nothing — the app stays running. In File Explorer create `App_Data\Teams\Business` and, inside it, a file `ops-lead.md`.
2. Put exactly this in `ops-lead.md`, saved with CRLF line endings: `---`, `name: 'Nova'`, `title: 'Ops Lead'`, `alias: 'nov'`, `teams: ['Business']`, `---`, `You are Nova. ORIGINALWORD.`
3. Wait about one second, then confirm a `Nova` tile appears under the `Business` heading at http://localhost:5100/teammates .
4. Click the tile and confirm `Persona file` shows `...\App_Data\Teams\Business\ops-lead.md`.
5. Click `Edit`, change `ORIGINALWORD` to `CHANGEDWORD`, and click `Save`.
6. Confirm the card lands in View mode showing `CHANGEDWORD` in the `Persona` block.
7. In File Explorer, list every `.md` file under `App_Data\Teams` at every depth.
8. Open `Business\ops-lead.md` and confirm the new word and the file's line endings.

**Pass if — all of these**

- `Business\ops-lead.md` now contains `CHANGEDWORD` and has a fresh modified time.
- No file named `Nova.md` was created anywhere.
- `ops-lead.md` was not renamed, moved, or duplicated — the total set of `.md` files is exactly what it was before the save.
- The card's `Persona file` still shows the `Business\ops-lead.md` path.

**Fail if — any of these**

- A new `App_Data\Teams\Nova.md` appeared -> the save reconstructed a path from the Name instead of using the discovered one; the original file is now an orphan and the two will collide on the next scan, sending BOTH into `Files that didn't load`.
- The original file was moved or renamed -> a user's own filing system was rearranged by an edit.
- Both the original and a new file exist with the same `name:` -> the next index rebuild rejects both and the teammate disappears from the list entirely.

**Inconclusive if**

If the tile never appears in step 3, the watcher may not be seeing sub-folders — that is TEAMMATECARD-30's subject; record it there and hand-place the file at the Teams root instead so this test can still run (it then proves less).

> [!NOTE]
> EXPECTED, NOT A DEFECT: after the save the file's line endings are LF, not the CRLF you wrote. The browser hands the textarea back with LF and the save writes it verbatim. Record it so nobody files it. Content loss or doubled blank lines WOULD be a defect.

### TEAMMATECARD-24 — Edit: breaking the frontmatter is refused with the file untouched

**Free** · about 12 min

*Proves every frontmatter failure is named precisely and none of them reaches disk — a written-through bad edit would drop the teammate out of the app entirely.*

**Before you start**

- A teammate exists (for example `Nova` at `App_Data\Teams\Nova.md`).
- You can see the file's modified timestamp.

**Steps**

1. Note the Persona file's exact modified timestamp.
2. Open the teammate's card and click `Edit`.
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

- Any rejected save changed the file on disk -> the teammate now fails to load, disappears from the list into `Files that didn't load`, and is only recoverable by hand in a text editor.
- A generic message such as `Save failed` that does not name the offending field -> the user has a whole file to search and no clue where.
- A duplicate key is accepted with a silent last-one-wins -> a repeat can only be an authoring mistake and must never resolve quietly.

**Inconclusive if**

If the modified timestamp changes but the contents are identical, your editor or an antivirus scanner may have touched the file — compare contents, which is the real oracle, and note the timestamp discrepancy.

> [!NOTE]
> Restore the file to its original content before moving on.

### TEAMMATECARD-25 — Edit: renaming into a collision is refused with the file untouched

**Free** · about 8 min

*Proves a Name or Alias edited into another file's identity is rejected before the write — a write-through would knock BOTH teammates out of the app at once.*

**Before you start**

- Two teammates exist: `Nova` (alias `nov`) and `Atlas` (alias `atl`). Create them from the card if needed.
- You know both files' paths and modified timestamps.

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

- Steps 3 and 4 both show `Persona Name 'Nova' is also used by '<full path to Nova.md>'.` / `Persona Name 'nova' is also used by '<path>'.` — the comparison is case-insensitive.
- Step 5 shows `Persona Alias 'nov' is also used by '<path to Nova.md>'.`
- Step 6 shows `Persona Alias 'Nova' matches the Name used by '<path to Nova.md>'.`
- `Atlas.md` is unchanged on disk and both tiles remain in the list.

**Fail if — any of these**

- Any attempt writes to disk -> BOTH files now claim the same identity, so the index rejects BOTH; two teammates vanish from the list into `Files that didn't load` and the only recovery is a text editor.
- The save succeeds and the list shows two tiles both named `Nova` -> the pre-write validation stopped running; from here `@nov` is ambiguous with no error at Mention time.
- The message names no path -> the user cannot find the other file that is causing the collision.

**Inconclusive if**

If the red line names a path you do not recognise, a third Persona file exists somewhere under `Teams` — list every `.md` at every depth before judging.

### TEAMMATECARD-26 — Edit: renaming via the name: field leaves a ghost card

**Free** · about 12 min

*Characterises what the user actually sees after a rename — the save takes, but the card left behind shows the OLD name with everything blanked, which reads like the teammate was wiped.*

**Before you start**

- A teammate `Nova` exists with a stored Model and Effort. If no catalog is available, seed them directly: `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "INSERT OR REPLACE INTO persona_models(persona_name,model) VALUES('Nova','claude-haiku-test'); INSERT OR REPLACE INTO persona_efforts(persona_name,effort) VALUES('Nova','low');"
- `sqlite3` is available.

**Steps**

1. Run `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "SELECT * FROM persona_models; SELECT * FROM persona_efforts;"` and record the rows.
2. Note `Nova`'s file path and filename.
3. Open `Nova`'s card, click `Edit`, change the `name:` line to `name: 'Aria'`, and click `Save`.
4. WITHOUT closing the card, write down exactly what the card now shows: its header, whether the `Persona` block has content, the Title, whether an `Alias:` line is present, what `Model` and `Effort` read, and whether a `Persona file` section exists.
5. Note whether a `Message` action is present on this card.
6. Look behind the overlay at the list.
7. Close the card and click the `Aria` tile. Read every section.
8. Check the file on disk: its filename, and its `name:` line.
9. Re-run the sqlite query from step 1.

**Pass if — all of these**

- The list behind the overlay shows a tile named `Aria` (and no `Nova`).
- The file keeps its ORIGINAL filename but now contains `name: 'Aria'`.
- Opening the `Aria` tile shows a complete, correct card.
- The `persona_models` and `persona_efforts` rows have MOVED from `Nova` to `Aria` — same values, new key, and no row left under `Nova`.

**Fail if — any of these**

- The `persona_models` or `persona_efforts` row for `Nova` is gone with nothing written under `Aria` -> the rename silently DROPPED the teammate's Model and Effort; next start it thinks with a model nobody chose.
- A row survives under `Nova` as well as `Aria` -> a future teammate named `Nova` would silently inherit a setting nobody chose for it.
- The file was renamed on disk -> the save is reconstructing paths from the Name (see TEAMMATECARD-23).
- No `Aria` tile appears at all -> the save did not take; check the file before concluding.

**Inconclusive if**

If `sqlite3` is unavailable, the Model/Effort half cannot be judged — run only the on-screen half and record the database half as unverified. Note that `team.db` is in WAL mode, so read it while the app runs rather than copying the file.

> [!NOTE]
> THE KNOWN ROUGH EDGE, to be recorded rather than filed as new: after the save the card re-opens on the OLD name, so it shows `Nova` with an empty `Persona` block, no Title, no `Alias:`, `Agent default`, `Model default` and no `Persona file`. Judge and report whether that reads to a user as 'my save wiped the teammate'. The Agent, Rooms and Transcripts staying behind under the old Name is DELIBERATE and must not be filed.

### TEAMMATECARD-27 — Create: a Name colliding with a Persona in a sub-folder

**Free** · about 8 min

*Proves the collision check sees Personas nested in Team sub-folders and reports the colliding path, instead of writing a second file that would knock both out.*

**Before you start**

- `App_Data\Teams` contains no `Nova.md` at the root.
- You can create folders under `App_Data\Teams`.

**Steps**

1. Create `App_Data\Teams\Business\anything.md` containing: `---`, `name: 'Nova'`, `title: 'Ops Lead'`, `alias: 'nov'`, `teams: ['Business']`, `---`, `You are Nova.`
2. Wait about one second and confirm a `Nova` tile appears under the `Business` heading.
3. Click `New teammate`. Set **Name** `Nova`, **Title** `T`, **Alias** `zzz`, body `x`. Click `Add teammate` and read the red line.
4. Change **Name** to `nova` (lowercase) and click `Add teammate` again. Read the red line.
5. Check whether `App_Data\Teams\Nova.md` or `nova.md` exists at the Teams root.
6. Close the card and confirm exactly one `Nova` tile is in the list, with no `Files that didn't load` section.

**Pass if — all of these**

- Both attempts show `Persona Name 'Nova' is also used by 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Business\anything.md'.` (or the same message with `'nova'`), naming the real absolute path.
- No file was created at the Teams root.
- Exactly one `Nova` tile remains and no rejected-files section appears.

**Fail if — any of these**

- No error and the save succeeds -> the pre-write validation is no longer scanning sub-folders; two files now claim `Nova`, so the next index rebuild rejects BOTH and the teammate disappears entirely.
- The lowercase `nova` attempt succeeds -> the comparison became case-sensitive; `Jarvis` and `jarvis` must collide, matching the case-insensitive key the Model/Effort tables use.
- The message names a reconstructed path such as `...\Teams\Nova.md` rather than the real discovered one -> the user is sent to a file that does not exist.

**Inconclusive if**

If no `Nova` tile appears in step 2, the sub-folder file did not load — check its frontmatter and check TEAMMATECARD-30 (sub-folder watching) before judging this test; without the existing Persona there is nothing to collide with.

> [!NOTE]
> Delete `App_Data\Teams\Business\anything.md` afterwards unless TEAMMATECARD-28 reuses it.

### TEAMMATECARD-28 — Create: Alias collisions, and the one that is legal

**Free** · about 8 min

*Proves a duplicate Alias and an Alias equal to another file's Name are both refused, while an Alias equal to its OWN Name is accepted.*

**Before you start**

- Exactly one teammate exists: `Nova`, alias `nov`.

**Steps**

1. Click `New teammate`. Set **Name** `Atlas`, **Title** `T`, body `x`, **Alias** `nov`. Click `Add teammate` and read the red line.
2. Change **Alias** to `NOV` (uppercase) and click `Add teammate` again. Read the red line.
3. Change **Alias** to `Nova` (the other file's NAME) and click `Add teammate`. Read the red line.
4. Change **Alias** to `Atlas` (its own Name) and click `Add teammate`.
5. Confirm what happened, then open `App_Data\Teams\Atlas.md` and read its `alias:` line.
6. Confirm `Nova.md` is untouched throughout.
7. Delete `Atlas.md` afterwards.

**Pass if — all of these**

- Steps 1 and 2 both show `Persona Alias 'nov' is also used by '<path to Nova's file>'.` / `... 'NOV' ...` — case-insensitive.
- Step 3 shows `Persona Alias 'Nova' matches the Name used by '<path to Nova's file>'.`
- Step 4 SUCCEEDS with no error, and `Atlas.md` contains `alias: 'Atlas'`.
- `Nova`'s file is unchanged after steps 1-3 and no file was written for them.

**Fail if — any of these**

- Step 4 is rejected -> an Alias equal to its own Name is an explicitly supported authoring choice and has been broken.
- Steps 1-3 are accepted -> an Alias is accepted anywhere a Name is, so `@nov` becomes ambiguous with no error at Mention time and the wrong teammate may answer.
- The uppercase `NOV` is accepted while `nov` is rejected -> the comparison became case-sensitive.

**Inconclusive if**

If more than one teammate exists at the start, the error may name a different file than expected — reduce to exactly one (`Nova`) and repeat.

### TEAMMATECARD-29 — 'Files that didn't load' names the path and the reason, above the list

**Free** · about 8 min

*Proves a file that fails to become a Persona is impossible to miss, names itself and says why — and that BOTH sides of a collision are rejected, never one silently winning.*

**Before you start**

- `App_Data\Teams` is otherwise empty.
- The Teammates page is open.

**Steps**

1. Create `App_Data\Teams\broken.md` containing exactly: `---`, `name: 'X'`, `---`, `Some body text.` (no title, no alias).
2. Wait about one second and read the top of the Teammates page.
3. Note whether the failing section appears ABOVE or BELOW the teammate list, and whether any tile was rendered for `broken.md`.
4. Now create TWO files, `App_Data\Teams\twin-a.md` and `App_Data\Teams\twin-b.md`, each containing: `---`, `name: 'Twin'`, `title: 'T'`, plus a DIFFERENT alias line (`alias: 'ta'` in one, `alias: 'tb'` in the other), `---`, `body`.
5. Wait about one second and read the page again.
6. Delete all three files.

**Pass if — all of these**

- A section headed `Files that didn't load` appears ABOVE the teammate list.
- It lists `broken.md`'s real absolute path with the reason `Persona frontmatter is missing required field 'Title'.`
- No tile is rendered for `broken.md`.
- Both `twin-a.md` and `twin-b.md` appear in the section, each naming the OTHER's absolute path, and NEITHER gets a tile.

**Fail if — any of these**

- One of the twins gets a tile and the other is rejected -> a winner is being picked by enumeration order; editing the loser then does nothing at all, with no feedback anywhere.
- A bad file is silently absent with nothing in this section -> a user's file has vanished from the app with no explanation.
- The section renders below the list -> its whole purpose is to be impossible to miss.
- The listed path is reconstructed or relative rather than the real absolute path -> the user cannot find the file to fix it.

**Inconclusive if**

If nothing appears after about two seconds, press F5 once. If the section only appears after a manual reload, that is a watcher finding — record it under TEAMMATECARD-30, and judge this test on the post-reload content.

### TEAMMATECARD-30 — An external file edit repaints the page — including inside sub-folders

**Free** · about 12 min

*Proves the file watcher sees the whole tree, including sub-folder edits, renames and deletes, which fail silently when broken; and records that an open View card does not follow.*

**Before you start**

- The Teammates page is open with NO card open.
- At least one Persona exists at the Teams root and one inside `App_Data\Teams\Business\`.

**Steps**

1. With no card open, edit the root Persona's `title:` line in a text editor to `title: 'Root Changed'` and save. Watch the tile's second line, without touching the browser. Time roughly how long it takes.
2. Edit the sub-folder Persona's `title:` line to `title: 'Nested Changed'` and save. Watch its tile.
3. Rename the folder `App_Data\Teams\Business` to `App_Data\Teams\BusinessOps`. Watch the page.
4. Rename it back to `Business`. Watch the page.
5. Move a Persona file from the Teams root into `App_Data\Teams\Business\`. Watch the page: which heading does its tile sit under now?
6. Delete the sub-folder Persona's file outright. Watch the page.
7. Now open a teammate's View card and LEAVE IT OPEN. In the editor, change that same teammate's `title:` and save. Watch both the tile behind the overlay and the card itself.
8. Read the app console for any line mentioning `FileSystemWatcher`.

**Pass if — all of these**

- Root and sub-folder title edits both repaint the tile within roughly half a second to a second, with no browser reload.
- Renaming a sub-folder, and renaming it back, both cause a repaint with no error and no teammate lost.
- Moving a file between folders repaints but does NOT change which Team heading the tile sits under.
- Deleting a file removes its tile within about a second.
- With a card open, the TILE behind updates.

**Fail if — any of these**

- A sub-folder file edit produces no repaint at all while the root one does -> the watcher is not including subdirectories; this fails silently, with no error and no log line, and every nested Persona serves stale prompt text forever.
- A sub-folder rename or delete produces no repaint -> the watcher event is named after the directory and never matches `*.md`; every Persona path under it is now stale.
- Moving a file changed its Team heading -> grouping followed the folder instead of the frontmatter.
- A `PersonaStore's FileSystemWatcher reported an error` line appears with NO repaint following it -> the overflow backstop did not schedule its refresh.

**Inconclusive if**

Some editors write via a temp file plus a rename, which can produce a different event shape. If a repaint does not happen, repeat the edit with a plain `Add-Content` from PowerShell before concluding the watcher is broken.

> [!NOTE]
> EXPECTED ROUGH EDGE, record don't file: with a View card open, the card's own `Persona` block, Title, Alias and Teams keep showing the OLD values until you close and reopen it — only the Room link is refreshed in the background. Judge it as a quirk, not data loss.

### TEAMMATECARD-31 — A card whose file vanished underneath it reports it instead of crashing

**Free** · about 6 min

*Proves every card action on a stale card produces a clear message rather than an unhandled exception or a silent no-op.*

**Before you start**

- A teammate `Nova` exists.
- The app console is visible.

**Steps**

1. Click the `Nova` tile to open its View card. Leave it open.
2. In File Explorer, delete `Nova`'s `.md` file.
3. Wait about one second (the list behind will repaint) but do NOT close the card.
4. On the still-open card, click `Remove`, then `Confirm`. Read the card and the action row.
5. Click `Open`. Read the card.
6. Click `Edit`, change a word, then click `Save`. Read the card.
7. Read the app console for any unhandled exception.

**Pass if — all of these**

- Confirm produces the red line `Persona 'Nova' does not exist.` and the button pair collapses back to a single `Remove`.
- `Open` produces the same message.
- Edit-then-Save produces the same message.
- The console shows no unhandled exception and the page never shows the yellow `An unhandled error has occurred.` bar.

**Fail if — any of these**

- The yellow error bar appears -> an unhandled exception killed the circuit; the user loses the whole page because a file moved.
- Nothing at all happens on click -> a silent no-op leaves the user believing the action worked.
- `Confirm` stays showing after the failure -> the pending-delete state survives an error and can be triggered again by one click.

**Inconclusive if**

If the card auto-closed when the file was deleted, this scenario cannot be produced through the UI — record that the card closes on external delete (a behaviour change) and mark the rest unverified.

### TEAMMATECARD-32 — 'Open' launches the OS editor on the server

**Free** · about 4 min

*Proves the Open action either opens the file or reports why, never failing silently.*

**Before you start**

- A teammate exists whose file is present on disk.
- The machine has some application registered for `.md`.

**Steps**

1. Click the teammate's tile.
2. Click `Open`.
3. Observe whether an application window opens showing the Persona file, and note which application.
4. Return to the browser and check whether a red line appeared on the card.

**Pass if — all of these**

- The Persona file opens in whatever the machine has registered for `.md`, OR a red line reads `Could not open '<Name>': <message>` when no handler exists.
- One of those two things always happens — never neither.

**Fail if — any of these**

- Nothing opens AND no error line appears -> a silent failure; the user clicks repeatedly with no feedback at all.
- An unhandled exception / the yellow error bar appears -> the failure path is not catching what the OS threw.

**Inconclusive if**

If the file opens but in an unexpected application, that is the OS file association, not this app — not a finding. If the app is running on a different machine from the browser, this action opens an editor you cannot see; run it locally or mark inconclusive.

> [!NOTE]
> This action launches a process on the SERVER, not in the browser, ungated in every environment. That is a recorded, deliberate decision for a single-user proof of concept. Do NOT file the absence of a permission prompt as a security defect here.

### TEAMMATECARD-33 — A Windows reserved device name as a Name (documented limit — record the outcome)

**Free** · about 5 min

*Determines WHICH of two known outcomes a reserved device name produces, because an unhandled exception that kills the page is materially worse than a silent no-op and the docs do not say which happens.*

**Before you start**

- Windows.
- The Create card can be opened.
- The app console is visible.

**Steps**

1. Note the current contents of `App_Data\Teams`.
2. Click `New teammate`. Set **Name** `CON`, **Title** `T`, **Alias** `cn1`, body `x`.
3. Click `Add teammate`.
4. Record precisely what happens: does a red validation line appear, does the card switch to View mode as if saved, or does the yellow `An unhandled error has occurred.` bar appear?
5. Read the app console and copy any exception verbatim.
6. List `App_Data\Teams` again and note whether any new file exists.
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
> THIS IS A RECORDED KNOWN LIMIT: reserved device names pass the Name rules and become `CON.md`, which Windows will not create. Do NOT file it as a new defect. The whole value of this test is deciding WHICH outcome occurs — an unhandled exception that kills the circuit, or an apparently successful save with no file — and reporting that precisely.

### TEAMMATECARD-34 — Two browser tabs on /teammates stay in step

**Free** · about 8 min

*Proves both circuits observe the same change event and that closing a tab with a card open leaves no disposed-object exception behind.*

**Before you start**

- Two browser tabs can be opened on the same machine.
- The app console is visible.
- `node` is installed so a probe spawn would be visible.

**Steps**

1. Open http://localhost:5100/teammates in tab A and again in tab B. Arrange them so both are visible.
2. In tab A, create a teammate: Name `Sync`, Title `T`, Alias `syn`, body `x`. Do not touch tab B.
3. Look at tab B's list without reloading it.
4. In tab A, close the card, click the `Sync` tile, click `Remove`, then `Confirm`.
5. Look at tab B's list again without reloading it.
6. Recreate `Sync`. In tab A click its tile then `Edit`; in tab B click the same tile then `Edit`, so both cards are open at once.
7. Run `Get-Process node` in PowerShell and count the node processes.
8. Close tab B while its Edit card is still open. Read the app console.
9. Delete `Sync`'s file afterwards.

**Pass if — all of these**

- Tab B gains the `Sync` tile without a reload.
- Tab B loses the tile when tab A removes it, without a reload.
- With both Edit cards open, at most one node process is running for the model probe at any moment.
- Closing tab B leaves no `ObjectDisposedException` or `InvalidOperationException` in the console.

**Fail if — any of these**

- Tab B never updates -> a component subscription is missing or leaked; two windows on the same app show contradictory teammate lists.
- Two node processes spawn simultaneously for the two card opens -> the probe's serialising gate regressed; every extra adapter process is a real process cost.
- An `ObjectDisposedException` appears when a tab closes -> a singleton-event subscription outlived its component and will keep firing into a dead circuit.

**Inconclusive if**

If no node process ever appears, the adapter is missing or unauthenticated — the concurrency half of this test is unverifiable; record it as unverified and judge only the list-sync half.

### TEAMMATECARD-35 — Remove deletes the file and BOTH stored settings

**Free** · about 8 min

*Proves the Model and Effort rows go with the Persona file, so a later teammate of the same name cannot silently inherit a setting nobody chose.*

**Before you start**

- `sqlite3` is on PATH.
- A teammate `Nova` exists at `App_Data\Teams\Nova.md`.

**Steps**

1. Seed both settings directly so this test needs no model catalog: run `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "INSERT OR REPLACE INTO persona_models(persona_name,model) VALUES('Nova','claude-haiku-test'); INSERT OR REPLACE INTO persona_efforts(persona_name,effort) VALUES('Nova','low');"
2. Verify: `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "SELECT * FROM persona_models WHERE persona_name='Nova'; SELECT * FROM persona_efforts WHERE persona_name='Nova';"` — both must return a row.
3. In the browser, click the `Nova` tile and confirm `Model` shows `claude-haiku-test` and `Effort` shows `low`.
4. Click `Remove`, then `Confirm`.
5. Confirm `App_Data\Teams\Nova.md` is gone.
6. Re-run the sqlite query from step 2.

**Pass if — all of these**

- Before the remove, both tables hold a row for `Nova` and the card shows both values.
- After the remove, `Nova.md` no longer exists.
- Both queries return nothing at all — no row, not a blank row.

**Fail if — any of these**

- A `persona_models` or `persona_efforts` row survives -> a teammate created later under the same Name silently inherits a Model or Effort nobody chose for it, and there is no UI anywhere that would show why.
- A row survives holding an empty string -> worse than a stale value: a row that exists but never resolves warns on every single session start, forever.
- The file is deleted but the card reports success while the rows remain -> the two halves of the delete are no longer one operation.

**Inconclusive if**

If `sqlite3` is missing, seed the values through the UI instead (pick Haiku and low in an Edit card — requires node and an authenticated adapter) and read them back from the View card; that proves less but is not nothing. Record which route you used.

> [!NOTE]
> `team.db` is in WAL mode, so `sqlite3` can read it while the app is running. Do not copy `team.db` on its own — recent writes live in `team.db-wal`.

### TEAMMATECARD-36 — A stored Model or Effort the catalog does not advertise survives an edit

**Free** · about 10 min

*Proves the card synthesises an option for a stored value it cannot find in the catalog, so opening Edit and saving does not silently wipe the teammate's model.*

**Before you start**

- `sqlite3` is on PATH.
- A teammate `Nova` exists.

**Steps**

1. Run `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "INSERT OR REPLACE INTO persona_models(persona_name,model) VALUES('Nova','claude-ghost-9'); INSERT OR REPLACE INTO persona_efforts(persona_name,effort) VALUES('Nova','ghost-effort');"
2. In the browser, reload the page, then click the `Nova` tile.
3. Read the `Model` and `Effort` sections of the View card.
4. Click `Edit`. Read the selected option in the **Model** select and in the **Effort** select, and check whether each list also contains the real catalog options (when a catalog is available).
5. Without touching either select, change one word of the body text and click `Save`.
6. Re-run `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "SELECT * FROM persona_models WHERE persona_name='Nova'; SELECT * FROM persona_efforts WHERE persona_name='Nova';"`
7. Clean up: delete both rows with `sqlite3 "...team.db" "DELETE FROM persona_models WHERE persona_name='Nova'; DELETE FROM persona_efforts WHERE persona_name='Nova';"`

**Pass if — all of these**

- The View card's `Model` section shows the raw id `claude-ghost-9` and `Effort` shows `ghost-effort`.
- In Edit, the **Model** select has `claude-ghost-9` SELECTED (as a synthesised first entry when the real catalog is present), and the **Effort** select has `ghost-effort` selected.
- After the save, `persona_models` still holds `claude-ghost-9` and `persona_efforts` still holds `ghost-effort`.

**Fail if — any of these**

- In Edit, the select falls back to `Use the agent's default` -> and after the save the stored value is GONE from the table; opening Edit for any unrelated reason silently wipes a teammate's model. This is the exact failure the synthesised option exists to prevent.
- The View card shows `Agent default` / `Model default` despite the rows existing -> the stored value is not being read at all.
- The card refuses to open, or an error appears -> an unrecognised stored value is bricking the Persona; it must be at most a warning at session start, never a failure.

**Inconclusive if**

If the app was started before the rows were inserted, reload the browser page and reopen the card — the value is read fresh on each card open, so a stale card is not a failure. If `sqlite3` is missing, this test cannot run; mark it blocked.

> [!NOTE]
> This is the strongest silent-data-loss test in the area that needs no adapter at all.

### TEAMMATECARD-37 — The model probe never runs on a plain page load

**Free** · about 8 min

*Proves the throwaway adapter process is spawned only when a Create or Edit card opens — never from browsing, filtering, or opening a View card.*

**Before you start**

- `node` is on PATH so a spawn would be visible.
- The app is running with `--Team:Acp:Enabled=false`.
- At least two teammates exist, in at least two different Teams.
- A PowerShell window is available.

**Steps**

1. In PowerShell run `Get-Process node` and record the count (an error saying no process was found counts as zero).
2. In the browser, load http://localhost:5100/teammates and reload it three more times.
3. Run `Get-Process node` again and read the app console for any line containing `Model catalog probe`.
4. Change the **Team** filter to a team, then back to `All teams`.
5. Run `Get-Process node` again.
6. Click a teammate tile to open the VIEW card.
7. Run `Get-Process node` again.
8. Now click `Edit` on that card.
9. Run `Get-Process node` again within a couple of seconds, and watch the **Model** hint text on the card.

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

### TEAMMATECARD-38 — Model and Effort pickers: labels, loading text and the empty-catalog text

**Free** · about 8 min

*Proves both selects offer a blank default, announce their loading state, and resolve into either a real list or the correct empty-catalog sentence — never a permanent spinner.*

**Before you start**

- `node` is on PATH.
- At least one teammate exists.
- The app console is visible.

**Steps**

1. Click a teammate tile, then click `Edit`. Immediately read the hint under **Model** and the hint under **Effort**.
2. Wait up to 25 seconds, re-reading both hints until they stop changing.
3. Open the **Model** select and read its first option and the rest of the list.
4. Open the **Effort** select and read its first option and the rest of the list. Look specifically for any option whose value or label is `default`.
5. If a real list came back, set **Model** to Haiku and **Effort** to low. If only the blank option is offered, note that and skip.
6. Read the console for any line beginning `Model catalog probe` or `No ACP adapter is installed`.
7. Click `Cancel` to close without saving.

**Pass if — all of these**

- While loading, the Model hint reads `Reading the models this agent offers…` and the Effort hint reads `Reading the effort levels this model offers…`.
- Both hints resolve within 25 seconds to either the real-list wording (`Which model this teammate thinks with. Changing it restarts the teammate, which clears what it remembers.` / `How hard this teammate thinks. Changing it restarts the teammate, which clears what it remembers.`) or the empty wording (`This agent advertises no models, so it will use its own default.` / `This model offers no effort choice, so it will think as it normally does.`).
- Both selects' first option is labelled `Use the agent's default`.
- No option labelled or valued `default` appears in the Effort list.

**Fail if — any of these**

- A loading hint never clears -> the probe is hung; it should time out at 20 seconds and log `Model catalog probe timed out after 00:00:20.` before falling back to the empty wording.
- An option with the id `default` appears in the Effort list -> the adapter's own sentinel is no longer filtered out; there are now two options meaning the same thing and the literal string `default` can reach the database.
- Either select has no blank first option -> a teammate can no longer be set back to the agent's own default.

**Inconclusive if**

An empty catalog is a LEGITIMATE result when node is missing, the adapter is not installed, or its authentication has lapsed. The picker cannot tell you which; the console can. Read it and record the cause rather than calling the test failed.

> [!NOTE]
> Record once, as a wording inconsistency rather than a bug: the Effort select's blank option says `Use the agent's default` while the View card calls the same state `Model default`. Effort is genuinely resolved by the model, not the agent.

### TEAMMATECARD-39 — Changing the Model clears the Effort and re-probes the ladder (Haiku -> Sonnet)

**Free** · about 8 min

*Proves an effort chosen against the old model is never carried across to a new one, and that a superseded probe cannot land on the newer choice.*

**Before you start**

- A real model catalog is available (TEAMMATECARD-38 returned a populated list).
- A teammate exists.
- A PowerShell window is available for `Get-Process node`.

**Steps**

1. Open the teammate's card and click `Edit`. Wait for both hints to settle.
2. Set **Model** to Haiku and **Effort** to low.
3. Change **Model** to Sonnet.
4. IMMEDIATELY read the **Effort** select's selected option and its hint text.
5. Run `Get-Process node` and note whether a new process appeared.
6. Confirm the **Model** select still shows Sonnet.
7. Now change **Model** to Haiku, and within a second change it back to Sonnet.
8. Wait for the Effort hint to settle, then open the **Effort** select and compare its options against what Sonnet offered in step 3-4.
9. Click `Cancel` to close without saving.

**Pass if — all of these**

- Immediately after the model change the **Effort** select shows the blank `Use the agent's default` option.
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

### TEAMMATECARD-40 — Model is stored as an id, in team.db only

**Free** · about 7 min

*Proves the chosen model is persisted as the catalog id rather than the display name, and never reaches the Persona file.*

**Before you start**

- `sqlite3` is on PATH.
- A real model catalog is available.
- The Create card can be opened.

**Steps**

1. Click `New teammate`. Set **Name** `Modelcheck`, **Title** `T`, **Alias** `mdc`, body `x`.
2. Set **Model** to Haiku. Leave **Effort** blank.
3. Click `Add teammate`.
4. Read the `Model` section of the resulting View card and write down exactly what it says.
5. Run `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "SELECT * FROM persona_models WHERE persona_name='Modelcheck';"` and write down the stored value.
6. Open `App_Data\Teams\Modelcheck.md` and search it for the stored value, for the display name, and for the word `model`.
7. Delete the teammate through the card afterwards.

**Pass if — all of these**

- The View card's `Model` section shows a human display name (for example `Haiku 4.5`).
- The database row holds a machine id (for example `claude-haiku-4-5-...`), lowercase and hyphenated — NOT the display name.
- `Modelcheck.md` contains no `model:` line and neither the id nor the display name.

**Fail if — any of these**

- The database holds the display name (for example `Haiku 4.5`) -> it will never resolve against a catalog id, so it logs a warning at every single session start, forever, and the teammate silently falls back to the agent default.
- A row exists with an empty string -> the blank option wrote a row instead of deleting one; that row exists but never resolves.
- The model appears in the `.md` file -> file and database can now disagree about the same fact.

**Inconclusive if**

If the Model select offers no options, the test cannot run — record it as blocked on the adapter and check the console for the reason line.

### TEAMMATECARD-41 — Effort low -> medium is stored as an id, and the blank option deletes the row

**Free** · about 9 min

*Proves the effort is persisted as its lowercase id, appears as its display name on the card, stays out of the Persona file, and that clearing it removes the row rather than blanking it.*

**Before you start**

- `sqlite3` is on PATH.
- A real effort catalog is available.
- A teammate exists whose Model is set to Haiku.

**Steps**

1. Open the teammate's card, click `Edit`, wait for the hints to settle, set **Effort** to low, and click `Save`.
2. Read the `Effort` section on the resulting View card.
3. Run `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "SELECT * FROM persona_efforts WHERE persona_name='<Name>';"` and record the value.
4. Click `Edit` again, change **Effort** from low to medium, and click `Save`.
5. Read the `Effort` section again and re-run the sqlite query.
6. Open the Persona `.md` file and search it for `effort`, `low` and `medium`.
7. Click `Edit` once more, set **Effort** back to the blank `Use the agent's default` option, and click `Save`.
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

### TEAMMATECARD-42 — Restart starts an adapter even with Team:Acp:Enabled=false

**Free** · about 10 min

*Characterises whether the card's Restart button bypasses the flag that exists to stop the app spending money — the single most important thing to establish in this area.*

**Before you start**

- The app is running with `--Team:Acp:Enabled=false`.
- `node` is on PATH and the adapter is installed.
- At least one teammate exists and reads `Offline`.
- A PowerShell window and the app console are both visible.

**Steps**

1. Confirm every tile on /teammates reads `Offline`.
2. Run `Get-Process node` and record the count.
3. Click a teammate tile and confirm the card offers a `Restart` button.
4. Click `Restart` once and immediately read the button's label.
5. Within five seconds run `Get-Process node` again and record the count.
6. Watch the card's status line and the tile behind it for up to 30 seconds; record every state it passes through.
7. Read the app console for any line naming this Persona.
8. Run `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "SELECT name, kind FROM users;"` and note whether a row now exists for this teammate's Name.
9. Reopen the card, click `Restart` and then immediately click it again (double-click). Confirm the node process count does not jump by two.

**Pass if — all of these**

- The button label changes to `Restarting…` and the button is disabled while the restart is in flight.
- Whatever the outcome, it is reported: either the badge reaches `Starting`/`Online`, or a red line naming the failure appears on the card.
- A double-click never produces two adapter processes for one teammate.
- The page never shows the yellow unhandled-error bar.

**Fail if — any of these**

- A double-click spawns two node processes -> two hosts under one agent id; the teammate will answer twice or fight itself.
- The badge stays `Offline` with NO reason line and nothing in the console -> the failure is invisible; a reason line is the whole feature here.
- The yellow error bar appears -> the restart path let an exception reach the circuit instead of surfacing it on the card.

**Inconclusive if**

If no node process spawns and the console says the adapter is missing or needs authentication, the money-guard question cannot be answered this run — record the console line and mark inconclusive rather than concluding the guard holds.

> [!NOTE]
> REPORT EXACTLY WHAT YOU OBSERVE, do not assume. The concern being tested: the supervisor's Restart path does not check `Team:Acp:Enabled`, only its background start loop does, and the agent host factory is registered unconditionally. If the teammate genuinely reaches `Online` with the flag off, then Restart is a route around the money guard and the teammate will take real, billed Turns the moment anyone messages it — that is a headline finding. Also note: with the flag off nothing is subscribed to persona changes, so an edit will NOT auto-restart it afterwards.

### TEAMMATECARD-43 — A newly created teammate comes online by itself when ACP is on

**Free** · about 10 min

*Proves a create is picked up by the supervisor exactly once, with the status following live on both the tile and the open card.*

**Before you start**

- Stop the app and restart it WITHOUT the flag: `dotnet run --project E:\Repos\Huddle\src\Huddle.App` (Development turns ACP on).
- `node` is on PATH and the adapter is authenticated.
- `App_Data\Teams` is empty at the start.

**Steps**

1. Run `Get-Process node` and record the count.
2. Create a teammate from the card: Name `Live`, Title `T`, Alias `lvo`, body `You are Live.`, Model Haiku, Effort low.
3. Leave the resulting View card OPEN and watch its status line, without reloading, for up to 60 seconds. Record every state it shows.
4. Watch the tile behind the overlay at the same time.
5. Run `Get-Process node` again and count how many NEW processes appeared for this one teammate.
6. Read the app console for any line naming `Live`.
7. Close the card, click `Remove` then `Confirm`, and run `Get-Process node` once more after 10 seconds.

**Pass if — all of these**

- The status moves `Offline` (or `Starting`) -> `Starting` -> `Online` within a few seconds, on both the open card and the tile, with no page reload.
- Exactly ONE new node process appears for the one teammate.
- After the remove, that node process goes away.

**Fail if — any of these**

- Two or three node processes appear for a single save -> one save is raising several change events, so one teammate spawns several adapters, each spending independently.
- The badge sticks on `Offline` with NO reason line and nothing in the console -> a start failure with no diagnosis anywhere; a reason line IS the feature.
- The open card's status never moves while the tile's does -> the card is not following live health, so a user watching the card sees a permanently starting teammate.
- The node process survives the remove -> an orphaned adapter keeps running with no teammate attached.

**Inconclusive if**

If the badge sticks on `Offline` WITH a reason line naming a missing adapter or lapsed authentication, that is correct behaviour for an unauthenticated machine — record the reason text and mark the online path unverified. Starting a session sends no prompt, so this costs nothing either way.

> [!NOTE]
> Starting a session spends no tokens by itself — no prompt is sent. Leave the app in ACP-on mode for TEAMMATECARD-44 through 50, then switch it back.

### TEAMMATECARD-44 — The Message action appears only once a Room exists

**Free** · about 8 min

*Proves the Message link is absent rather than broken for a teammate that has never connected, and that it appears on an already-open card when the teammate connects.*

**Before you start**

- The app is running with ACP ON (see TEAMMATECARD-43).
- A teammate name that has never connected before (use a fresh name such as `Freshone`).

**Steps**

1. Stop the app (Ctrl+C) and restart it WITH ACP OFF: `dotnet run --project E:\Repos\Huddle\src\Huddle.App -- --Team:Acp:Enabled=false`.
2. Create a teammate: Name `Freshone`, Title `T`, Alias `frn`, body `x`.
3. On the resulting View card, read the action row.
4. Confirm with `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "SELECT name FROM users WHERE name='Freshone';"` that no Agent row exists.
5. Leave the card OPEN. Click `Restart` (or, alternatively, restart the app with ACP on and reopen the page) and wait for the teammate to reach `Online`.
6. Without closing or reopening the card, read the action row again.
7. If a `Message` link appeared, hover it and read its target, then click it.
8. Re-run the sqlite query, plus `sqlite3 "...team.db" "SELECT id, name FROM rooms;"`.

**Pass if — all of these**

- Before the teammate ever connects, the action row is exactly `Edit`, `Open`, `Restart`, `Remove` — NO `Message`.
- After it connects, a `Message` link appears whose href is `/rooms/<id>`, and it appears WITHOUT closing and reopening the card.
- Clicking `Message` navigates to that Room and the Room opens.
- A `users` row for `Freshone` and a two-member Room containing it and the Human now exist.

**Fail if — any of these**

- A `Message` link is offered before the teammate has ever connected -> it leads to a Room that does not exist; the link 404s or lands on an empty page.
- The link never appears even after the teammate is `Online` and the Room row exists -> the open card is not refreshing its Room link in the background, so the user must close and reopen the card to reach a chat that already exists.
- Clicking `Message` lands on an empty or missing Room -> the id being rendered is not the Room's.

**Inconclusive if**

If the teammate never reaches `Online` (no adapter, no authentication), the second half of the test cannot run — record the first half (Message correctly absent) as passed and the rest as unverified.

> [!NOTE]
> A teammate that has never connected having no Message action is DELIBERATE — absent rather than broken. Do not file the absence itself.

### TEAMMATECARD-45 — Restart is hidden for a healthy or starting teammate

**Free** · about 5 min

*Proves the Restart button and its warning appear only when there is something to fix, so a healthy session's memory is never thrown away for nothing.*

**Before you start**

- The app is running with ACP ON.
- A teammate is `Online`.

**Steps**

1. Open the `Online` teammate's card.
2. Read the whole action row and the area directly beneath it.
3. Watch a teammate while it is in the `Starting` state (restart the app and open a card quickly, or click Restart on another teammate and open its card immediately). Read its action row.
4. Force the teammate offline: stop the app, restart it with `--Team:Acp:Enabled=false`, reload the page, and open the same card.
5. Read the action row and the area beneath it again.

**Pass if — all of these**

- While `Online`, the actions are `Message`, `Edit`, `Open`, `Remove` — NO `Restart` — and no `Like saving an edit…` hint is shown.
- While `Starting`, `Restart` is likewise absent.
- Once `Offline`, both `Restart` and the hint `Like saving an edit, this restarts the teammate, which clears what it remembers.` reappear.

**Fail if — any of these**

- `Restart` is offered for an `Online` teammate -> clicking it destroys that agent's conversation memory for no benefit, and nothing on screen warns that is the trade.
- `Restart` is offered while `Starting` -> a Persona launching for the first time reads as a fault and invites a user to interrupt it.
- The hint appears without the button, or vice versa -> the two are supposed to move together.

**Inconclusive if**

If you cannot get a teammate to `Online` at all, only the Offline half is testable — record the rest as unverified. Catching the `Starting` state is timing-dependent; if you miss it, say so rather than guessing.

### TEAMMATECARD-46 — A save that changes nothing does not restart the teammate

**Free** · about 8 min

*Proves a no-op Save does not destroy a live session's conversation memory, while a real change does.*

**Before you start**

- The app is running with ACP ON.
- One teammate is `Online`.
- The app console and a PowerShell window are visible.

**Steps**

1. Run `Get-Process node` and note the process id of the teammate's adapter.
2. Open the teammate's card and confirm the badge reads `Online`.
3. Click `Edit`, touch nothing at all, and click `Save`.
4. Watch the badge continuously for 15 seconds and record every state it shows.
5. Run `Get-Process node` again and compare the process id.
6. Read the console for a fresh adapter launch sequence.
7. Now click `Edit` again, add a single space to the END of the body text, and click `Save`.
8. Watch the badge again for 15 seconds, and compare the node process id once more.

**Pass if — all of these**

- After the no-op save the badge stays `Online` and never shows `Starting`.
- The node process id is unchanged after the no-op save.
- After the whitespace change the badge DOES pass through `Starting` and back to `Online`, and the node process id changes.

**Fail if — any of these**

- The badge flickers `Starting` -> `Online` after the no-op save -> the session was restarted and that agent's entire conversation memory was destroyed for nothing; the comparison that should have short-circuited is whole-record equality over text, Model and Effort.
- The whitespace change does NOT restart -> a real edit did not take effect, and the card claims otherwise; the teammate keeps thinking with the old prompt.
- The node process id changes but the badge never moves -> the status is not following the actual session.

**Inconclusive if**

If the badge is never `Online` to begin with, this test cannot distinguish a restart from a failed start — get one teammate genuinely `Online` first or mark blocked.

> [!NOTE]
> Free: a restart sends no prompt. Only the later memory test (TEAMMATECARD-50) spends anything.

### TEAMMATECARD-47 — Changing only the Model or only the Effort restarts the session

**Free** · about 8 min

*Proves a Model or Effort change actually takes effect, which is only possible by stopping and restarting the session.*

**Before you start**

- The app is running with ACP ON.
- A teammate is `Online` with Model Haiku and Effort low.
- `sqlite3` is on PATH.
- A PowerShell window is visible.

**Steps**

1. Note the teammate's node process id with `Get-Process node`.
2. Open the card, click `Edit`, change ONLY the **Model** from Haiku to Sonnet (do not touch the text), and click `Save`.
3. Watch the badge for 20 seconds and record the states.
4. Run `Get-Process node` and compare process ids.
5. Run `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "SELECT * FROM persona_models WHERE persona_name='<Name>';"`.
6. Wait for `Online`, then click `Edit` and change ONLY the **Effort** from the blank option to low (the model change cleared it), and click `Save`.
7. Watch the badge for 20 seconds and compare node process ids again.
8. Run `sqlite3 "...team.db" "SELECT * FROM persona_efforts WHERE persona_name='<Name>';"`.
9. Set the Model back to Haiku and the Effort back to low when finished.

**Pass if — all of these**

- Each change makes the badge pass through `Starting` and back to `Online`.
- The node process is replaced each time (a new process id).
- `persona_models` holds the Sonnet id after step 2; `persona_efforts` holds `low` after step 6.

**Fail if — any of these**

- No restart happens after a Model or Effort change -> a model and an effort are both fixed when a session is created and cannot be swapped into a running one, so the teammate keeps thinking with the OLD model while the card claims the new one. This is silent and permanent until something else restarts it.
- The database updates but the session does not restart -> the card and the running session now disagree with no way to tell from the UI.
- The session restarts but the database is unchanged -> the change is lost at the next app start.

**Inconclusive if**

If the model catalog offers only one model, the Model half cannot run; record it as blocked and run the Effort half alone. Never select Opus to obtain a second model — if Sonnet is unavailable, mark blocked.

> [!NOTE]
> Free: no prompt is sent by a restart.

### TEAMMATECARD-48 — Remove does NOT cascade: the Agent and its Room survive

**Free** · about 8 min

*Proves that removing a Persona takes it offline without deleting the chat facts it created — a deliberate decision that a tester must not file as a bug.*

**Before you start**

- The app is running with ACP ON.
- A teammate has connected at least once, so it has an Agent row and a Room.
- `sqlite3` is on PATH.

**Steps**

1. Open the teammate's card and click `Message`. Note the full `/rooms/<id>` URL from the address bar.
2. Run `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "SELECT id,name FROM users WHERE name='<Name>'; SELECT id,name FROM rooms;"` and record the rows.
3. Go back to /teammates, open the teammate's card, click `Remove`, then `Confirm`.
4. Confirm the tile is gone from /teammates and the `.md` file is gone from disk.
5. Look at the Rooms list in the left sidebar.
6. Navigate to the `/rooms/<id>` URL you noted and read the page.
7. Re-run the sqlite query from step 2.
8. Check the transcript files under `App_Data` for that Room and confirm they still exist.

**Pass if — all of these**

- The teammate's tile and its `.md` file are gone.
- The Room is STILL listed in the sidebar and STILL opens at its URL.
- Every message that was already in that Room is still shown.
- The `users` row and the `rooms` row are both still present.
- The removed teammate shows as permanently `Offline` inside the Room.

**Fail if — any of these**

- The Room disappears from the sidebar or 404s at its URL -> removing a Persona deleted an Agent's chat history; 'removing a Persona' is explicitly not 'deleting an Agent'.
- The transcript file is gone -> conversation history was destroyed by a Persona delete.
- The `users` row is gone -> the Agent identity was cascaded away, which would also break the re-create case (TEAMMATECARD-49).

**Inconclusive if**

If the teammate never connected, there is no Room to survive and this test proves nothing — bring it online first (TEAMMATECARD-43) and repeat. A Room with no messages still proves the Room survives; whether the TRANSCRIPT survives needs at least one exchange, which costs a Turn (see TEAMMATECARD-50).

> [!NOTE]
> THIS IS A RECORDED REPO-OWNER DECISION. A tester expecting a full cleanup will want to file it — do not. Only the Model and Effort rows go with the file; Agents, Rooms and Transcripts stay.

### TEAMMATECARD-49 — Delete then recreate under the same Name: fresh settings, old Room

**Free** · about 10 min

*The sharpest test in the area: proves settings must NOT survive a delete while chat facts MUST. It catches both opposite regressions at once.*

**Before you start**

- TEAMMATECARD-48 has just completed, so a teammate Name was removed but its Agent row and Room remain.
- `sqlite3` is on PATH.

**Steps**

1. Run `sqlite3 "E:\Repos\Huddle\src\Huddle.App\App_Data\team.db" "SELECT * FROM persona_models WHERE persona_name='<Name>'; SELECT * FROM persona_efforts WHERE persona_name='<Name>'; SELECT id,name FROM users WHERE name='<Name>';"` and record all three results.
2. Click `New teammate` and recreate the SAME Name, with the same Title and Alias, body `x`, but leave BOTH **Model** and **Effort** on their blank option.
3. Click `Add teammate` and read the `Model` and `Effort` sections on the resulting View card.
4. Re-run the two settings queries.
5. Wait for the teammate to come `Online`, then read its card's action row and click `Message`.
6. Compare the `/rooms/<id>` URL with the one recorded in TEAMMATECARD-48, and read what is already in the Room.

**Pass if — all of these**

- Before the recreate, `persona_models` and `persona_efforts` hold NO row for that Name, while the `users` row is still present.
- The recreated teammate's card reads `Agent default` and `Model default` — the Haiku/low chosen before has NOT come back.
- Both settings queries still return nothing.
- `Message` leads to the SAME Room id as before, with its earlier content intact.

**Fail if — any of these**

- The old Model or Effort reappears on the new teammate -> a stored setting was resurrected from before the delete; the user is now running a model they never chose, with nothing in the UI to explain it.
- A brand-new empty Room appears instead of the old one, or the `users` id has changed -> the Agent row WAS cascaded away on the delete, which destroys chat history and breaks every existing link to that Room.
- The card shows `Agent default` but the database holds a row -> the card is not reading what is stored.

**Inconclusive if**

If the teammate cannot come online, the Room half is unverifiable — the settings half is still decisive on its own. Record which half you verified.

> [!NOTE]
> Settings must not survive; chat facts must. Either direction being wrong is a defect, and they are opposite regressions.

### TEAMMATECARD-50 — Editing a teammate really does lose its conversation memory (COSTS MONEY)

**💰 Spends money** · about 12 min

*Confirms once, end to end, that the card's own warning is true: saving an edit restarts the session and the teammate forgets what it was told.*

**Before you start**

- The app is running with ACP ON and the adapter is authenticated.
- A teammate is `Online` with Model = Haiku and Effort = low. Verify BOTH on its card before starting.
- The app console is visible.

**Steps**

1. CONFIRM COST FIRST: check the teammate's card shows Model Haiku and Effort low. Do not run this test against any other model.
2. Open the teammate's Room via the card's `Message` action.
3. Type exactly `Remember the codeword is PELICAN.` and send it. Wait for the reply and confirm the teammate acknowledged.
4. Type exactly `What is the codeword?` and send it. Confirm it answers `PELICAN` — this proves the session remembers before the edit.
5. Go to /teammates, open the teammate's card, click `Edit`, change ONE word of the Persona body, and click `Save`.
6. Watch the badge until it returns to `Online`, and confirm in the console that the adapter process was replaced.
7. Return to the Room and send exactly `What is the codeword?` again.
8. Read the reply.

**Pass if — all of these**

- Before the edit, the teammate answers `PELICAN`.
- After the edit and restart, the teammate does NOT know the codeword (it says it does not know, or asks what codeword).
- The console shows the adapter process being replaced between the two questions.

**Fail if — any of these**

- After the edit the teammate still answers `PELICAN` -> the session was not actually restarted, so the edited Persona text has NOT taken effect; the card's own promise (`Saving restarts it, which clears what it remembers`) is false and every edit is silently a no-op on the running session.
- The teammate never answers the first question -> this test cannot judge memory; treat as inconclusive, not failed.

**Inconclusive if**

If the teammate never reaches `Online`, or the adapter reports an authentication failure, stop — no Turn was spent and nothing was proven. If the reply is ambiguous (the teammate guesses or refuses), ask once more with `Repeat the codeword I gave you earlier.` and judge that; do not keep asking, each question costs.

> [!NOTE]
> COST: roughly three short agent Turns on Haiku at low effort — a few hundred tokens in total, well under a cent. Run this ONCE for the whole area, not per edit. It is the only money-spending test here. Do not raise the model or the effort to make the answer 'better'.

---

Back to [the manual test script](../manual-tests.md).
