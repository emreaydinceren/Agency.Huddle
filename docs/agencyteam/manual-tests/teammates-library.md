# Teammates page: tiles, Teams grouping, filter and rejected files

Prove that the /teammates page is an honest, live mirror of the Persona files on disk: that a teammate's identity and Team come from its frontmatter and never from its filename or folder, that every file which fails to load is named out loud instead of silently vanishing, that the Team filter narrows without probing anything, that the status badge tells the truth (health outranking a live pipe), and that the file watcher keeps up with edits, creations, deletions, moves and folder renames at every depth. Almost every failure in this area is silent — a moved file that quietly changes identity, a nested file that stops reloading, a colliding file that disappears without a word — so these tests are written for a human driving a browser and a file explorer side by side.

**40 tests** · 39 free, 1 paid 💰 · about 4.2 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB` / `O-WIRE`, the four resets,
`P-NEW-PERSONA`, `P-ECHO-BOT` and the standing conventions. This area adds:

1. Lane is `P-LAUNCH-FREE`. A handful of tests need ACP on: stop the app and restart it as `dotnet run --project src/Huddle.App -- --Team:Acp:Enabled=true`, then drop the argument to turn it off again. Never edit `appsettings.json` to do this — leave the shipped defaults alone.
2. Confirm the Persona library is empty before you start: `Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams` should list nothing. If it lists files, move them to a scratch folder now and put them back when you are finished — TEAMMATESLIBRARY-01 and -02 require an empty library.
3. Open a browser at `http://localhost:5100` and a File Explorer window (or a text editor) on `src\Huddle.App\App_Data\Teams\`, side by side. Many tests change a file on disk and then watch the browser WITHOUT touching it — do not press F5 unless a step says to.
4. Learn the CORE FIXTURE SET but do NOT create it yet — TEAMMATESLIBRARY-03 creates it. Four files under `src\Huddle.App\App_Data\Teams\`: (1) `Nova.md` = frontmatter name 'Nova', title 'Research Lead', alias 'nov', teams ['Business']; (2) `Vale.md` = name 'Vale', title 'Ops Lead', alias 'val', teams ['Business', 'Household']; (3) `Household\Rune.md` = name 'Rune', title 'Home Steward', alias 'run', teams ['Household']; (4) `Quill.md` = name 'Quill', title 'Scribe', alias 'qui', and NO teams line at all. Exact text is given in TEAMMATESLIBRARY-03.
5. Do NOT copy the repo-root `personas\*.md` files into the Teams folder. None has a `title:` or `alias:` field, so all twelve would land in the rejected block and drown every other observation. `tools/migrate-personas-to-teams.ps1` reshapes them if you want a realistic library later.
6. All sorting on this page is ORDINAL, not case-insensitive: an upper-case letter sorts before a lower-case one, so a Team named `Zulu` legitimately appears above one named `admin`, and a Persona named `Zoe` above one named `ada`. This is deliberate and pinned by tests. Do not file it.

## Tests

### TEAMMATESLIBRARY-01 — The page loads, is styled, and spawns nothing

**Free** · about 3 min

*Proves /teammates is reachable, carries its heading, button and intro copy, is actually styled, and starts no adapter process merely by being visited.*

**Before you start**

- App running at http://localhost:5100.
- Any state of the Teams folder (empty is fine).

**Steps**

1. In `T-B` run `O-ADAPTERS`. It should print `0`.
2. In the browser, click **Teammates** in the left sidebar.
3. Confirm the address bar now reads http://localhost:5100/teammates .
4. Read the page's top heading, the button beside it, and the paragraph beneath them.
5. Run `O-ADAPTERS` again in `T-B`.

**Pass if — all of these**

- The top heading reads exactly `Teammates`.
- A button labelled exactly `New teammate` sits on the same row as the heading.
- The paragraph under them begins `A Persona is a Markdown file describing how one teammate should behave, plus the model it thinks with.` and ends `Removing the file only takes it offline — its chats and their history stay.`
- The page is visibly styled — heading and button laid out as a row, page gutters, a non-default font — not a bare stack of black serif text on white.
- `O-ADAPTERS` still prints `0` — loading the page spawned no Adapter.

**Fail if — any of these**

- 404, or no `Teammates` link in the sidebar -> the route or the nav entry was renamed or dropped.
- The page renders as unstyled default-browser text -> a CSS class rename was missed and app.css no longer matches the markup; this is the exact regression this step exists to catch.
- The intro paragraph is missing -> the only place the page tells a user that editing a file restarts a teammate and clears its memory is gone.
- A `node` process appears purely from loading the page -> the model-catalog probe is running on page load; it must run only when a New teammate / Edit card is opened.

**Inconclusive if**

Browser shows 'connection refused' or the page never finishes loading -> the app is not running or port 5100 is taken; check the `dotnet run` console, fix it, and rerun. Do not record a result. If unrelated `node` processes were already running before the app started, note their PIDs first so the before/after comparison means something.

> [!NOTE]
> This is the cheap sanity version of the no-probe rule. TEAMMATESLIBRARY-34 is the full version (filter changes and a View card as well).

### TEAMMATESLIBRARY-02 — Empty library shows its sentence and no Team filter

**Free** · about 2 min

*Proves an empty Persona library reads as empty rather than broken, and that the Team dropdown is not rendered when there is nothing to filter.*

**Before you start**

- `src\Huddle.App\App_Data\Teams\` contains no .md files.

**Steps**

1. In `T-B` run `Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams -Filter *.md` and confirm it lists nothing.
2. In the browser, go to http://localhost:5100/teammates and press F5.
3. Read the whole page from top to bottom.

**Pass if — all of these**

- The page shows exactly the sentence `No Personas yet. Choose New teammate to add one.`, with `New teammate` in bold.
- No label reading `Team` and no dropdown appear anywhere on the page.
- No group headings appear.
- No `Files that didn't load` section appears.
- The heading, the `New teammate` button and the intro paragraph are all still present.

**Fail if — any of these**

- A `Team` dropdown is rendered over an empty library -> the filter is not gated on there being any loaded Persona.
- The page body is blank — neither the empty-state sentence nor any tiles -> reads to a user as a load failure rather than an empty library.
- The empty-state sentence appears but the `New teammate` button does not -> a user with an empty library has no way to create their first teammate.

**Inconclusive if**

The folder is not empty (leftover files from an earlier run) -> move them to a scratch folder, press F5, and rerun. If the folder does not exist at all, that is fine: the app creates it at startup; restart the app and rerun.

### TEAMMATESLIBRARY-03 — The fixture library loads, and one tile shows monogram, Name, Title · @alias and status

**Free** · about 8 min

*Creates the core fixture set and proves a tile renders every identity field from frontmatter, with the documented first-and-last-word monogram rule.*

**Before you start**

- Empty Teams folder at the start (TEAMMATESLIBRARY-02 just ran).

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\Nova.md` containing exactly:
---
name: 'Nova'
title: 'Research Lead'
alias: 'nov'
teams: ['Business']
---
You are Nova. Answer in one short sentence.
2. Create `src\Huddle.App\App_Data\Teams\Vale.md` containing exactly:
---
name: 'Vale'
title: 'Ops Lead'
alias: 'val'
teams: ['Business', 'Household']
---
You are Vale. Answer in one short sentence.
3. Create the folder `src\Huddle.App\App_Data\Teams\Household\` and inside it create `Rune.md` containing exactly:
---
name: 'Rune'
title: 'Home Steward'
alias: 'run'
teams: ['Household']
---
You are Rune. Answer in one short sentence.
4. Create `src\Huddle.App\App_Data\Teams\Quill.md` containing exactly (note: no teams line):
---
name: 'Quill'
title: 'Scribe'
alias: 'qui'
---
You are Quill. Answer in one short sentence.
5. Create `src\Huddle.App\App_Data\Teams\Emily Lee.md` containing exactly:
---
name: 'Emily Lee'
title: 'Analyst'
alias: 'emi'
teams: ['Business']
---
You are Emily Lee. Answer in one short sentence.
6. Create `src\Huddle.App\App_Data\Teams\cos.md` containing exactly (note the filename `cos.md` deliberately does not match the Name):
---
name: 'Chief of Staff'
title: 'Chief of Staff'
alias: 'cos'
teams: ['Business']
---
You are the Chief of Staff. Answer in one short sentence.
7. Go to http://localhost:5100/teammates and press F5.
8. Read the second line of the Nova tile character by character, including the separator between Title and alias.
9. Read the coloured square at the left of each of the six tiles.
10. Read the status line under each tile's second line.

**Pass if — all of these**

- Six tiles are on the page: Chief of Staff, Emily Lee, Nova, Quill, Rune, Vale.
- Nova's tile shows `Nova` on its first line and `Research Lead · @nov` on its second — a real middle dot, and the alias carries a leading `@`.
- Every tile's second line is `<Title> · @<alias>` with values matching that file's frontmatter (Vale: `Ops Lead · @val`; Rune: `Home Steward · @run`; Quill: `Scribe · @qui`).
- Monograms read: Emily Lee -> `EL`; Chief of Staff -> `CS`; Nova -> `N`; Quill -> `Q`; Rune -> `R`; Vale -> `V`.
- Each tile has a status line with a coloured dot and one of `Starting` / `Online` / `Degraded` / `Offline` — with ACP off, every one reads `Offline` with a RED dot (`agent-dot offline`, computed `rgb(224, 90, 90)` from `--status-offline`). There is no grey dot in the design.
- The tiles have rounded borders and a coloured monogram square — they are tiles, not bullet points.
- The `Chief of Staff` tile exists even though its file is named `cos.md` — the heading text comes from frontmatter, not the filename.

**Fail if — any of these**

- A Title or an alias renders blank although the file has it -> a dropped `@` on a card/tile binding; the page is rendering a string literal instead of the field.
- The literal text `this.cardName`, `this.cardTitle` or similar appears anywhere on screen -> the documented leading-`@` binding bug has shipped again.
- The alias appears without its leading `@` -> the handle no longer reads as a handle, and a user cannot tell it from a second name.
- `Chief of Staff` shows the monogram `CO` -> the monogram is taking the first TWO words instead of first-and-last.
- The `Chief of Staff` tile is missing or is headed `cos` -> identity is being taken from the filename rather than the frontmatter Name.

**Inconclusive if**

A tile you expected is missing AND a `Files that didn't load` block names its path -> you mistyped that file's frontmatter; fix the file (watch for smart quotes inserted by your editor — the parser wants plain `'`) and rerun. If the page shows nothing at all after F5, restart the app and rerun before recording anything.

> [!NOTE]
> Keep all six fixture files in place — later tests depend on them and say so. Use a plain-text editor; a word processor that converts `'` into a curly quote will make files fail to parse.

### TEAMMATESLIBRARY-04 — One heading per Team, ordinal order, "No team" last, no empty groups

**Free** · about 4 min

*Proves tiles are grouped under a heading per declared Team and that a Persona with no Team falls under a final "No team" heading rather than vanishing.*

**Before you start**

- The core fixture set from TEAMMATESLIBRARY-03 is in place.
- The Team filter is on `All teams` (its default).

**Steps**

1. Go to http://localhost:5100/teammates and press F5.
2. Read every group heading on the page, from top to bottom, in order.
3. Under each heading, list the tiles it holds.
4. In the file explorer, open `Quill.md` and confirm it still has no `teams:` line.

**Pass if — all of these**

- Exactly three headings appear, in this order: `Business`, `Household`, `No team`.
- `Business` holds Chief of Staff, Emily Lee, Nova and Vale.
- `Household` holds Rune and Vale.
- `No team` holds Quill, and is the LAST heading on the page.
- No heading appears with no tiles under it.

**Fail if — any of these**

- `No team` appears first, or in ordinal position among the real Teams -> the no-team bucket is being sorted with the real Teams instead of appended last.
- Quill appears under no heading at all, or is missing from the page -> a Persona with no Team is being silently dropped; the file is valid and the user gets no explanation anywhere.
- A heading appears with an empty grid under it -> empty groups are being rendered; this is how a Team lingers after its last member leaves.
- Headings are in some order other than ordinal (`Business` before `Household`) -> the heading order no longer follows the sorted Team list.

**Inconclusive if**

A tile is under the wrong heading AND you have edited a fixture since -03 -> re-read that file's `teams:` line first; the file is the oracle. If the page has stale content, press F5 once before judging.

### TEAMMATESLIBRARY-05 — A Persona in two Teams renders in full under both headings

**Free** · about 3 min

*Proves multi-Team membership duplicates the tile across headings rather than picking the first Team.*

**Before you start**

- Core fixture set in place; `Vale.md` declares `teams: ['Business', 'Household']`.

**Steps**

1. Go to http://localhost:5100/teammates and press F5.
2. Find the Vale tile under the `Business` heading and write down its Name, second line and status label.
3. Find the Vale tile under the `Household` heading and write down the same three things.
4. Click the Vale tile under `Business` and note the card's Name and `Teams:` line, then close the card with the × button.
5. Click the Vale tile under `Household` and note the same two things.

**Pass if — all of these**

- A full Vale tile appears under BOTH `Business` and `Household`.
- Both copies show the identical Name (`Vale`), second line (`Ops Lead · @val`), monogram (`V`) and status label.
- Both clicks open the same card: Name `Vale`, and a line reading `Teams: Business, Household`.

**Fail if — any of these**

- Vale appears under only `Business` (the first Team named) -> membership is being read as a single value, not a list.
- The two copies show different status labels -> the badge is being derived per group instead of once per Persona; two surfaces now disagree about one teammate.
- Clicking one copy opens a card for a different teammate -> the tile's click target is bound to the group index rather than the Persona.

**Inconclusive if**

Vale is absent from one heading and also absent from the Team dropdown -> re-read `Vale.md`; if its `teams:` line is malformed this test cannot judge. Fix and rerun.

### TEAMMATESLIBRARY-06 — Folders are cosmetic: a nested file is grouped by its field, not its folder

**Free** · about 5 min

*Proves the single most important rule in this area — a Persona's Team comes from frontmatter, never from the sub-folder its file happens to sit in, and never from its filename.*

**Before you start**

- Core fixture set in place, including the folder `App_Data\Teams\Household\`.

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\Household\zzz-misfiled.md` containing exactly:
---
name: 'Orin'
title: 'Strategy Lead'
alias: 'ori'
teams: ['Business']
---
You are Orin. Answer in one short sentence.
2. Wait about two seconds WITHOUT touching the browser, then look at the page. If nothing changed, press F5 once and note that you had to.
3. Find the `Orin` tile and note which heading it sits under.
4. Use the browser's find (Ctrl+F) and search the page for the word `Household`.
5. Click the Orin tile and read the `Persona file` section at the bottom of the card.
6. Close the card.

**Pass if — all of these**

- An `Orin` tile appears, and it sits under the `Business` heading.
- No `Orin` tile appears under `Household`.
- Ctrl+F finds `Household` only as a group heading (for Rune and Vale) — never as anything attached to Orin.
- The card's `Persona file` section prints an absolute path ending `...\App_Data\Teams\Household\zzz-misfiled.md`, while the tile's heading still reads `Business`.
- The tile is headed `Orin`, not `zzz-misfiled`.

**Fail if — any of these**

- Orin appears under `Household` -> grouping is derived from the containing folder. This is the highest-value regression in the area: it compiles, it looks entirely reasonable on screen, and it silently reverses the Teams-as-a-field decision.
- No Orin tile appears at all and no rejected entry names the file -> the directory scan is not recursive; every Persona in a sub-folder is invisible.
- The tile reads `zzz-misfiled` -> identity is being taken from the filename.

**Inconclusive if**

The tile only appears after you press F5 -> the grouping half of this test still passes/fails on its own merits, but record the refresh requirement and run TEAMMATESLIBRARY-19 and -20, which exist to judge the watcher properly.

> [!NOTE]
> Delete `zzz-misfiled.md` when finished with this test unless a later test says otherwise; TEAMMATESLIBRARY-07's expected dropdown list assumes only the core fixtures.

### TEAMMATESLIBRARY-07 — The Team filter's label, options, order and de-duplication

**Free** · about 6 min

*Proves the dropdown is built from the union of every loaded Persona's Teams, sorted ordinal, each Team once, with "All teams" first.*

**Before you start**

- Core fixture set in place.
- `zzz-misfiled.md` from TEAMMATESLIBRARY-06 deleted.

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\Zed.md` containing exactly:
---
name: 'Zed'
title: 'Night Lead'
alias: 'zed'
teams: ['Zulu']
---
You are Zed. Answer in one short sentence.
2. Create `src\Huddle.App\App_Data\Teams\Ada.md` containing exactly:
---
name: 'Ada'
title: 'Admin Lead'
alias: 'ada'
teams: ['admin']
---
You are Ada. Answer in one short sentence.
3. Go to http://localhost:5100/teammates and press F5.
4. Find the control above the first group heading and read its label.
5. Open the dropdown and write down every option, in order.

**Pass if — all of these**

- A label reading exactly `Team` wraps a dropdown, positioned above the first group heading.
- The first option is `All teams`, and it is the one selected.
- After it the options are, in this order: `Business`, `Household`, `Zulu`, `admin` — upper-case before lower-case, because sorting is ordinal.
- `Business` appears exactly once even though four Personas name it.
- No option appears for a Team no Persona names.

**Fail if — any of these**

- A Team is missing from the list although a tile on the page names it -> the option list is being built from the visible group rather than from every loaded Persona; that Team becomes unreachable through the filter.
- The same Team appears twice -> the Team list is not de-duplicated, and two identical-looking options select different things.
- `All teams` is absent -> once a Team is chosen there is no way back to the full grouped view.
- `admin` sorts above `Zulu` -> the sort changed from ordinal to case-insensitive. Confirm against the rule in the setup notes before filing: ordinal is deliberate.

**Inconclusive if**

The dropdown does not render at all -> check that at least one tile is on the page; the filter is deliberately absent for an empty library (TEAMMATESLIBRARY-02). If tiles are present and the dropdown is not, that is a FAIL, not inconclusive.

> [!NOTE]
> Keep `Zed.md` and `Ada.md` until TEAMMATESLIBRARY-08 and -09 are done; they are the cheapest Teams to empty out.

### TEAMMATESLIBRARY-08 — Choosing a Team narrows to one heading and drops the no-team Personas

**Free** · about 3 min

*Proves the filter narrows instantly on selection, with no reload and no button, and that "No team" is filtered away with everything else.*

**Before you start**

- Core fixture set plus Zed.md and Ada.md from TEAMMATESLIBRARY-07.

**Steps**

1. Go to http://localhost:5100/teammates and press F5.
2. Open the **Team** dropdown and choose `Business`. Do not press any other key or button.
3. Read every heading and tile now on the page.
4. Open the dropdown and choose `Household`; read the headings and tiles again.
5. Open the dropdown and choose `All teams`; read the headings again.

**Pass if — all of these**

- Choosing `Business` changes the page immediately, with no page reload and without clicking anything else.
- With `Business` chosen, exactly ONE heading is on the page, reading `Business`, holding Chief of Staff, Emily Lee, Nova and Vale.
- No `Household` heading, no `Zulu`, no `admin` and no `No team` heading remains; Quill is not on the page.
- With `Household` chosen, exactly one heading reading `Household` holds Rune and Vale.
- Choosing `All teams` restores the full grouped view from TEAMMATESLIBRARY-04 plus `Zulu` and `admin`.

**Fail if — any of these**

- Nothing happens when you pick an option -> the select is wired to the wrong DOM event (`oninput` rather than `change`); this is an explicitly documented hazard in this file, and the filter is simply dead.
- Other headings remain visible alongside the chosen one -> narrowing is not applied, only highlighting.
- Quill (the no-team Persona) survives under the chosen Team -> the no-team bucket is being appended after filtering instead of being filtered away.
- The narrowing only appears after F5 -> the page is not re-rendering on selection; a user will read the filter as broken.

**Inconclusive if**

The browser shows a stale page and other interactions are also dead -> the Blazor circuit has dropped (look for a reconnect banner); reload and rerun.

### TEAMMATESLIBRARY-09 — A Team whose membership drops to zero says so

**Free** · about 4 min

*Proves an emptied Team shows its own sentence rather than a bare heading, an empty grid, or the empty-library sentence.*

**Before you start**

- Core fixture set plus Zed.md (the only member of Team `Zulu`).

**Steps**

1. Go to http://localhost:5100/teammates, press F5, and choose `Zulu` in the **Team** dropdown.
2. Confirm the page shows one heading `Zulu` holding the Zed tile.
3. Without touching the browser, open `src\Huddle.App\App_Data\Teams\Zed.md` in your editor and change the line `teams: ['Zulu']` to `teams: ['Business']`. Save.
4. Wait about two seconds and watch the page WITHOUT refreshing.
5. Read what the page now shows.
6. Open the **Team** dropdown and choose `All teams`.

**Pass if — all of these**

- The page repaints on its own within about a second of the save.
- It then shows exactly the sentence `No teammates in this team.`
- It does NOT show a `Zulu` heading with nothing under it.
- It does NOT show `No Personas yet. Choose New teammate to add one.`
- Choosing `All teams` brings back every other tile, proving the library is intact; Zed now appears under `Business`, and `Zulu` is gone from the dropdown.

**Fail if — any of these**

- An empty `Zulu` heading is rendered with nothing beneath it -> an empty group is being produced, and a user cannot tell an empty Team from a broken page.
- The page falls back to `No Personas yet…` while other Personas still exist -> the empty-library state is being reused for an empty filter; the user is told their whole library is gone.
- The page throws, or the yellow `An unhandled error has occurred.` banner appears -> a filter selection that outlives its Team is crashing the circuit.

**Inconclusive if**

Nothing repaints even after 10 seconds -> this may be the watcher rather than the filter; press F5, and if the correct `No teammates in this team.` sentence then appears, record this test as passing with a note and treat the watcher failure under TEAMMATESLIBRARY-19.

> [!NOTE]
> Afterwards, delete `Zed.md` and `Ada.md` to return to the core fixture set.

### TEAMMATESLIBRARY-10 — Team names match case-insensitively and appear once

**Free** · about 5 min

*Proves two Personas whose Team names differ only in case land under one heading and produce one dropdown option.*

**Before you start**

- Core fixture set in place; Zed.md and Ada.md deleted.

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\Casey.md` containing exactly:
---
name: 'Casey'
title: 'Shift Lead'
alias: 'cas'
teams: ['Nightshift']
---
You are Casey. Answer in one short sentence.
2. Create `src\Huddle.App\App_Data\Teams\Dana.md` containing exactly (note the lower-case team):
---
name: 'Dana'
title: 'Shift Second'
alias: 'dan'
teams: ['nightshift']
---
You are Dana. Answer in one short sentence.
3. Go to http://localhost:5100/teammates and press F5.
4. Count the headings that read `Nightshift` or `nightshift`, in any casing.
5. Open the **Team** dropdown and count the options matching that name in any casing.
6. Choose that option and read the tiles shown.

**Pass if — all of these**

- Exactly ONE heading appears for that Team, reading `Nightshift` (the casing used by Casey, the first Persona by Name).
- Both the Casey and Dana tiles sit under that single heading.
- The dropdown offers that Team exactly ONCE.
- Selecting it shows both Casey and Dana.

**Fail if — any of these**

- Two headings appear, `Nightshift` and `nightshift` -> Team matching became case-sensitive; the same Team now splits in two and each half hides the other.
- Only one of Casey and Dana appears under the heading -> membership matching is case-sensitive even though the heading list is not.
- The dropdown offers two options that look nearly identical -> the Team list is de-duplicated by exact case; a user cannot tell which one to pick.

**Inconclusive if**

Neither Persona appears and the rejected block names them -> fix the frontmatter (check for smart quotes) and rerun.

> [!NOTE]
> Delete `Casey.md` and `Dana.md` when done.

### TEAMMATESLIBRARY-11 — All three YAML shapes of `teams` group identically, and the documented comma cost

**Free** · about 10 min

*Proves the comma scalar, the flow list and the block list all produce the same headings, that a Team name containing a comma becomes two Teams, and that a Team repeated in different case collapses.*

**Before you start**

- Core fixture set in place; no other throwaway fixtures present.

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\ShapeA.md` containing exactly (plain comma scalar):
---
name: 'ShapeA'
title: 'Scalar Form'
alias: 'sha'
teams: Business, Household
---
You are ShapeA. Answer in one short sentence.
2. Create `src\Huddle.App\App_Data\Teams\ShapeB.md` containing exactly (flow list):
---
name: 'ShapeB'
title: 'Flow Form'
alias: 'shb'
teams: ['Business', 'Household']
---
You are ShapeB. Answer in one short sentence.
3. Create `src\Huddle.App\App_Data\Teams\ShapeC.md` containing exactly (block list — two leading spaces before each dash):
---
name: 'ShapeC'
title: 'Block Form'
alias: 'shc'
teams:
  - 'Business'
  - 'Household'
---
You are ShapeC. Answer in one short sentence.
4. Create `src\Huddle.App\App_Data\Teams\ShapeD.md` containing exactly (a Team name with a comma inside quotes, plus a quoted prose field that must NOT be split):
---
name: 'ShapeD'
title: 'Comma Form'
alias: 'shd'
teams: ['Sales, EMEA']
role: 'Router, triage, and cross-workstation continuity'
---
You are ShapeD. Answer in one short sentence.
5. Create `src\Huddle.App\App_Data\Teams\ShapeE.md` containing exactly:
---
name: 'ShapeE'
title: 'Repeat Form'
alias: 'she'
teams: ['Ops', 'ops']
---
You are ShapeE. Answer in one short sentence.
6. Go to http://localhost:5100/teammates and press F5.
7. Read every heading, and under each, note whether ShapeA, ShapeB and ShapeC appear.
8. Open the **Team** dropdown and read every option.
9. Click the ShapeE tile and read its `Teams:` line on the card, then close the card.

**Pass if — all of these**

- ShapeA, ShapeB and ShapeC each appear under BOTH `Business` and `Household` — all three shapes produce identical grouping.
- Two headings `Sales` and `EMEA` exist, each holding ShapeD. (This is the documented, accepted cost — see the note below; it is a pass, not a bug.)
- No heading reads `Business, Household` and no heading reads `Business; Household`.
- No heading is named after any fragment of ShapeD's `role:` sentence — there is no `Router`, `triage` or `cross-workstation continuity` heading, and no `Router` option in the dropdown.
- ShapeE appears under exactly one heading, `Ops`, and its card's `Teams:` line reads `Ops` once, not `Ops, ops`.

**Fail if — any of these**

- A single heading reading `Business, Household` -> the comma scalar shape is being treated as one literal Team name; every Persona using that shape becomes a group of its own.
- A single heading reading `Business; Household` -> the block-list shape's internal join is leaking into the Team name.
- ShapeC appears under no heading -> block lists are not being parsed as a list at all.
- A heading appears named after part of ShapeD's `role:` line -> the comma split is being applied to every field, not just `teams`; carefully-worded prose fields are being shredded into Teams.
- ShapeE appears under two headings `Ops` and `ops` -> per-Persona de-duplication of Team names has stopped being case-insensitive.

**Inconclusive if**

Any Shape file lands in the rejected block -> your editor most likely rewrote the quoting or the indentation. Re-create the file with a plain-text editor (two spaces before each `-` in ShapeC) and rerun.

> [!NOTE]
> `teams: ['Sales, EMEA']` producing TWO Teams is a documented known limit, not a defect: a Team name can never contain a comma. It is the accepted price of splitting `teams` at the consumer so quoted prose fields elsewhere in the file are never split. Delete all five Shape files when done.

### TEAMMATESLIBRARY-12 — A rejected file is named by path and reason, above the tiles, live

**Free** · about 5 min

*Proves the single failure this feature exists to prevent — a file silently vanishing — cannot happen: a broken file is listed loudly, without a refresh, and restoring it brings the tile back.*

**Before you start**

- Core fixture set in place; no Shape/throwaway files left over.

**Steps**

1. Go to http://localhost:5100/teammates and press F5. Confirm the Nova tile is present and there is no `Files that didn't load` section.
2. Without touching the browser, open `src\Huddle.App\App_Data\Teams\Nova.md` and delete the whole line `title: 'Research Lead'`. Save.
3. Wait about two seconds and watch the browser WITHOUT refreshing.
4. Read the new section that appears, including its heading, the path line and the line under it.
5. Scroll and note whether that section is above or below the tiles.
6. Check the `dotnet run` console for any exception or stack trace.
7. Put the `title: 'Research Lead'` line back and save.
8. Wait about two seconds and watch the browser again, without refreshing.

**Pass if — all of these**

- Within about a second of the save, and with no refresh, the Nova tile disappears and a new section appears.
- That section is headed exactly `Files that didn't load` and sits ABOVE the tiles and above the Team filter.
- It lists the absolute path of `Nova.md` on one line, and under it the reason `Persona frontmatter is missing required field 'Title'.`
- The section is visually distinct (its own bordered block), not a plain sentence lost in the page.
- No exception or stack trace appears in the `dotnet run` console.
- After restoring the line, the section disappears on its own and the Nova tile returns with `Research Lead · @nov`.

**Fail if — any of these**

- The Nova tile vanishes and NOTHING appears to say why -> the worst failure in this area: a user's file is on disk, invisible in the app, with no explanation anywhere.
- The block renders below the tiles -> it can be scrolled past, and a broken file goes unnoticed in a long library.
- The reason does not name the field (`Title`) -> the message is undiagnosable; the user cannot tell which line to fix.
- An exception appears in the console, or the yellow `An unhandled error has occurred.` banner shows -> the read path is throwing for a bad file, which it must never do.
- The tile stays on the page with its old Title -> stale cached text is being served for a file that no longer parses.

**Inconclusive if**

Nothing changes until you press F5 -> the rejection wording still passes or fails on its own, but record the refresh requirement and run TEAMMATESLIBRARY-19, which judges the watcher.

### TEAMMATESLIBRARY-13 — Every distinct rejection reason, word for word

**Free** · about 12 min

*Proves each malformed-file case produces a reason that names the offending field, and that two faults in one file produce two reasons on one line.*

**Before you start**

- Core fixture set in place and loading cleanly (no rejected block on screen at the start).

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\bad1.md` containing exactly (no name field):
---
title: 'Nameless'
alias: 'nl1'
---
You are nameless.
2. Look at the page and record the reason shown under `bad1.md`. Then delete `bad1.md`.
3. Create `src\Huddle.App\App_Data\Teams\bad2.md` containing exactly (no frontmatter at all — plain prose):
You are a teammate with no front matter whatsoever.
4. Record the reason shown under `bad2.md`. Then delete `bad2.md`.
5. Create `src\Huddle.App\App_Data\Teams\bad3.md` containing exactly (blank title):
---
name: 'Blanky'
title: ''
alias: 'bla'
---
You are Blanky.
6. Record the reason shown under `bad3.md`. Then delete `bad3.md`.
7. Create `src\Huddle.App\App_Data\Teams\bad4.md` containing exactly (name written twice):
---
name: 'Twice'
name: 'Twice'
title: 'Repeat'
alias: 'twi'
---
You are Twice.
8. Record the reason shown under `bad4.md`. Then delete `bad4.md`.
9. Create `src\Huddle.App\App_Data\Teams\bad5.md` containing exactly (two spaces inside the name):
---
name: 'Emily  Lee'
title: 'Analyst'
alias: 'em2'
---
You are Emily Lee.
10. Record the reason shown under `bad5.md`. Then delete `bad5.md`.
11. Create `src\Huddle.App\App_Data\Teams\bad6.md` containing exactly (alias starting with a hyphen):
---
name: 'Hyphen'
title: 'Tester'
alias: '-nope'
---
You are Hyphen.
12. Record the reason shown under `bad6.md`. Then delete `bad6.md`.
13. Confirm the page has returned to the clean core fixture set with no rejected block, and check the `dotnet run` console for exceptions across the whole test.

**Pass if — all of these**

- bad1.md -> `Persona frontmatter is missing required field 'Name'.`
- bad2.md -> `Persona frontmatter is missing required field 'Name'.` (a file with no frontmatter is reported the same way, and is NOT loaded as a Persona)
- bad3.md -> `Persona frontmatter is missing required field 'Title'.` (a blank value and a missing line are the same failure)
- bad4.md -> `Persona frontmatter has a duplicate 'Name' field.`
- bad5.md -> `Persona frontmatter field 'Name' has an invalid value: 'Emily  Lee'.`
- bad6.md -> `Persona frontmatter field 'Alias' has an invalid value: '-nope'.`
- Every reason is shown under that file's own absolute path.
- The four valid core fixtures keep rendering as tiles throughout — one bad file never takes the page down.
- No exception or stack trace appears in the console at any point.

**Fail if — any of these**

- Any reason is generic and does not name the offending field -> the user cannot diagnose their own file; the rejected block stops being useful.
- bad2.md (no frontmatter) loads as a Persona and shows a tile -> the required-field schema is not being enforced; files with no identity silently become teammates.
- A reason names the wrong field (e.g. bad3 reported against `Name`) -> the message points the user at the wrong line.
- The app crashes, or the page stops rendering tiles, on any of these files -> a malformed file is being treated as an exception rather than as data.

**Inconclusive if**

A `bad*.md` file produces no rejected entry at all AND no tile -> confirm the file is really under `App_Data\Teams` and ends in `.md`; if it is a `.md` and it is neither loaded nor rejected, that IS a fail (a file must always be accounted for as one or the other).

> [!NOTE]
> A file that trips two INDEX-level rules (e.g. two files sharing both Name and Alias) shows both reasons on one line separated by a space — that case is covered in TEAMMATESLIBRARY-14. Parser-level faults short-circuit, so a file missing both `name:` and `title:` correctly reports `Name` only.

### TEAMMATESLIBRARY-14 — A duplicate Alias rejects BOTH files, each naming the other

**Free** · about 5 min

*Proves a collision takes out both sides rather than picking a winner — the rule that keeps editing the loser from silently doing nothing.*

**Before you start**

- Core fixture set in place; Nova.md has `alias: 'nov'`.

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\Twin.md` containing exactly (same alias as Nova, plus the same Name too, so both reason types appear):
---
name: 'Twin'
title: 'Duplicate Alias'
alias: 'nov'
---
You are Twin.
2. Wait about two seconds and watch the page without refreshing.
3. Note whether the Nova tile is still present.
4. Read every entry in the `Files that didn't load` block.
5. Change `Twin.md`'s alias line to `alias: 'twi'` and save.
6. Wait about two seconds and watch the page again.

**Pass if — all of these**

- BOTH tiles disappear: `Nova` and `Twin`.
- The `Files that didn't load` block lists BOTH absolute paths.
- Under `Nova.md`'s path the reason reads `Persona Alias 'nov' is also used by '<absolute path of Twin.md>'.`
- Under `Twin.md`'s path the reason reads `Persona Alias 'nov' is also used by '<absolute path of Nova.md>'.` — each names the OTHER file.
- Both files are still on disk, unmodified, throughout.
- After fixing `Twin.md`'s alias, both tiles come back on their own.

**Fail if — any of these**

- Only one file is rejected while the other keeps its tile -> a 'first one wins' rule has been introduced; editing the losing file then does nothing at all, with no feedback anywhere. This is exactly the silent failure the both-sides rule exists to prevent.
- A reason names the file's own path rather than the other file's -> the user has no way to find the colliding file.
- Either file is modified or deleted on disk by the app -> rejection must never write.

**Inconclusive if**

Only one entry appears and the other file is also missing from disk -> you may have overwritten a fixture; restore the core fixture set and rerun.

> [!NOTE]
> Seeing two teammates vanish for one typo is the design, not a bug — it is called out in the known limits.

### TEAMMATESLIBRARY-15 — A duplicate Name — including one differing only in case — rejects both

**Free** · about 5 min

*Proves Name collisions are caught case-insensitively, closing the documented bug where 'Jarvis' and 'jarvis' loaded as two teammates sharing one database row.*

**Before you start**

- Core fixture set in place.

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\jarvis-a.md` containing exactly:
---
name: 'Jarvis'
title: 'Chief of Staff'
alias: 'jva'
---
You are Jarvis.
2. Create `src\Huddle.App\App_Data\Teams\jarvis-b.md` containing exactly (same name, different case, different alias):
---
name: 'jarvis'
title: 'Deputy'
alias: 'jvb'
---
You are jarvis.
3. Wait about two seconds and read the page without refreshing.
4. Read both entries in the `Files that didn't load` block.
5. In `T-B` run `sqlite3 src/Huddle.App/App_Data/team.db "select * from persona_models;"` (skip if you have no sqlite3).
6. Delete both files.

**Pass if — all of these**

- NO tile appears for Jarvis or jarvis — not two, and not one.
- The `Files that didn't load` block lists BOTH absolute paths.
- `jarvis-a.md`'s reason reads `Persona Name 'Jarvis' is also used by '<absolute path of jarvis-b.md>'.`
- `jarvis-b.md`'s reason reads `Persona Name 'jarvis' is also used by '<absolute path of jarvis-a.md>'.`
- `persona_models` holds no row for Jarvis or jarvis — a rejected file must never create one.

**Fail if — any of these**

- Two tiles appear, `Jarvis` and `jarvis` -> the case-insensitive Name collision rule has regressed; the two teammates will silently share one `persona_models` row, and one of them will appear to change Model by itself.
- Only one is rejected -> a winner is being picked by enumeration order; edits to the loser do nothing.
- A `persona_models` row exists for either name after this test -> a rejected file is writing to the database.

**Inconclusive if**

No sqlite3 available -> run the test without the database step and record that the database half was not checked; the tile and reason observations still stand on their own.

### TEAMMATESLIBRARY-16 — One file's Alias equal to another file's Name rejects both, with two different sentences — and a self-matching alias is legal

**Free** · about 6 min

*Proves the cross-collision is caught in both directions with direction-specific wording, and that a file whose alias equals its own name is deliberately allowed.*

**Before you start**

- Core fixture set in place, including Nova.md with `name: 'Nova'`.

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\shadow.md` containing exactly (alias equal to Nova's NAME):
---
name: 'Shadow'
title: 'Shadow Lead'
alias: 'Nova'
---
You are Shadow.
2. Wait about two seconds and read the page without refreshing.
3. Read both entries in the `Files that didn't load` block, word for word.
4. Delete `shadow.md` and confirm the Nova tile returns.
5. Create `src\Huddle.App\App_Data\Teams\selfsame.md` containing exactly (alias equals its OWN name):
---
name: 'Echoic'
title: 'Self Named'
alias: 'Echoic'
---
You are Echoic.
6. Wait about two seconds and read the page.
7. Delete `selfsame.md`.

**Pass if — all of these**

- With `shadow.md` present, BOTH the Nova tile and any Shadow tile are gone, and both paths are listed in the rejected block.
- `shadow.md`'s reason reads `Persona Alias 'Nova' matches the Name used by '<absolute path of Nova.md>'.`
- `Nova.md`'s reason reads `Persona Name 'Nova' matches the Alias used by '<absolute path of shadow.md>'.`
- The two sentences are DIFFERENT and each points at the other file, so the direction of the clash is readable.
- With `selfsame.md` present, an `Echoic` tile appears and its second line reads `Self Named · @Echoic`; it is NOT in the rejected block.

**Fail if — any of these**

- `selfsame.md` is rejected -> a file whose alias equals its own name is being refused; that is a plausible authoring choice and is explicitly legal.
- Only one side of the `shadow.md` clash is rejected -> the other file's owner gets no feedback at all.
- Both sides show identical wording -> which file holds the Name and which holds the Alias is unreadable, so the user cannot tell which one to change.
- Neither file is rejected and both tiles appear -> an Alias can now shadow another teammate's Name, and mentions become ambiguous.

**Inconclusive if**

Nova's reason is missing but shadow.md's is present -> re-read the block carefully (entries are sorted by path, so Nova's may be far from shadow's). Only record a fail once you have checked the whole list.

### TEAMMATESLIBRARY-17 — The rejected block is ordered, survives the filter, and coexists with the empty-library sentence

**Free** · about 6 min

*Proves a broken file stays visible in a filtered view and when the library has no valid Persona left at all — the moments the block matters most.*

**Before you start**

- Core fixture set in place.

**Steps**

1. Create three broken files with these exact contents:
`src\Huddle.App\App_Data\Teams\aaa-broken.md`:
---
name: 'AAA'
alias: 'aaa'
---
No title here.
`src\Huddle.App\App_Data\Teams\mmm-broken.md`:
---
name: 'MMM'
alias: 'mmm'
---
No title here.
`src\Huddle.App\App_Data\Teams\zzz-broken.md`:
---
name: 'ZZZ'
alias: 'zzz'
---
No title here.
2. Reload http://localhost:5100/teammates and read the order of the three paths in the `Files that didn't load` block.
3. Choose `Business` in the **Team** dropdown and check whether the rejected block is still on screen and still lists all three.
4. Choose `All teams` again.
5. Now break the whole library: in the file explorer, delete the `title:` line from `Nova.md`, `Vale.md`, `Quill.md`, `Household\Rune.md`, `Emily Lee.md` and `cos.md`, saving each.
6. Wait two seconds, read the whole page.
7. In `T-B` run `Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams -Filter *.md | Measure-Object` and compare the count with the number of entries in the rejected block plus the number of tiles.
8. Restore all six `title:` lines and delete the three `*-broken.md` files.

**Pass if — all of these**

- The three broken paths are listed sorted by path: `aaa-broken.md`, `mmm-broken.md`, `zzz-broken.md`.
- With `Business` selected, the rejected block is still fully visible and still lists all three — the filter does not touch it.
- With every file broken, the page shows BOTH the `Files that didn't load` block AND the sentence `No Personas yet. Choose New teammate to add one.`
- In that all-broken state, no `Team` dropdown is rendered.
- The .md file count on disk equals (tiles on screen + entries in the rejected block) at every point in this test — every file is accounted for by exactly one of the two.

**Fail if — any of these**

- The rejected block disappears while a Team is selected -> a broken file is invisible to anyone browsing a filtered view; the one user most likely to miss their file is the one who filtered.
- The block disappears when there are zero valid Personas -> it vanishes exactly when it is the only thing that can explain the empty page.
- The entries are in a shifting or arbitrary order between reloads -> the list is unstable and hard to scan.
- The file count on disk exceeds tiles + rejected entries -> some file is neither loaded nor explained; it has silently disappeared.

**Inconclusive if**

A `.md` file elsewhere in the tree (a README, a scratch note) makes the count not line up -> remove or account for it explicitly before judging the count criterion; the other criteria still stand.

### TEAMMATESLIBRARY-18 — Non-markdown files are ignored entirely — not loaded, not rejected

**Free** · about 4 min

*Proves stray files in the Teams tree cause neither a rejected entry nor a reload burst.*

**Before you start**

- Core fixture set in place and loading cleanly (no rejected block on screen).

**Steps**

1. Note the exact contents of the page: tiles present, and no `Files that didn't load` block.
2. Create `src\Huddle.App\App_Data\Teams\notes.txt` with the text `scratch notes, not a persona`.
3. Create an empty file `src\Huddle.App\App_Data\Teams\.gitkeep`.
4. Create `src\Huddle.App\App_Data\Teams\Household\readme.rst` with the text `this is not markdown`.
5. Watch the page for about five seconds WITHOUT refreshing.
6. Check the `dotnet run` console for any new lines.
7. Delete the three files you just created.

**Pass if — all of these**

- No new tile appears for any of the three files.
- No `Files that didn't load` block appears, and no entry is added to one.
- The page does not visibly flicker or re-render.
- The console logs no Persona reload or restart lines for these files.

**Fail if — any of these**

- `notes.txt` (or either of the others) is listed in the rejected block -> non-Persona files are being parsed; the block fills with noise and real rejections become unreadable.
- The page repaints each time you add one -> stray files are churning the reload path; with ACP on this restarts every online teammate and costs money for nothing.
- A tile appears for one of them -> the `*.md` scan filter has been widened.

**Inconclusive if**

You cannot tell whether a repaint happened -> repeat with the browser devtools open on the Elements panel, where a re-render highlights, or repeat this test after TEAMMATESLIBRARY-26 so an Online badge gives you a visible marker of a page reload.

### TEAMMATESLIBRARY-19 — The watcher is live and recursive: a NESTED Persona reloads on edit

**Free** · about 8 min

*Catches the headline silent failure of this area — a file in a Team sub-folder that is found once at startup and then never reloads, with no error anywhere.*

**Before you start**

- Core fixture set in place, including the NESTED `Household\Rune.md`.
- The browser is on /teammates and you will not press F5 during the test.

**Steps**

1. Go to http://localhost:5100/teammates, press F5 once, and confirm the Rune tile reads `Home Steward · @run` under the `Household` heading.
2. In your editor, open `src\Huddle.App\App_Data\Teams\Household\Rune.md` and change `title: 'Home Steward'` to `title: 'House Captain'`. Save. Do not touch the browser.
3. Wait up to about two seconds and read the Rune tile.
4. In the same NESTED file, change `alias: 'run'` to `alias: 'rn'`. Save, wait, and read the tile again.
5. In the same NESTED file, change `teams: ['Household']` to `teams: ['Business']`. Save, wait, and read which heading Rune now sits under.
6. Click the Rune tile and compare the text in the card's `Persona` block with your editor's buffer, then close the card.
7. Now repeat the first edit on a ROOT file: open `src\Huddle.App\App_Data\Teams\Nova.md`, change `title: 'Research Lead'` to `title: 'Research Chief'`, save, wait, and read the Nova tile.
8. Restore all four edited values (`House Captain` -> `Home Steward`, `rn` -> `run`, `Business` -> `Household`, `Research Chief` -> `Research Lead`).

**Pass if — all of these**

- Within about a second of each save, and with NO page refresh and NO app restart, the tile shows the new value.
- The nested Rune tile's second line becomes `House Captain · @run`, then `House Captain · @rn`.
- After the `teams:` edit, the Rune tile moves from under `Household` to under `Business` on its own.
- The card's `Persona` block shows the whole raw file text, matching the editor buffer exactly (frontmatter included).
- The root-level Nova tile reloads on edit in exactly the same way.

**Fail if — any of these**

- The ROOT file reloads but the NESTED file does not -> `IncludeSubdirectories` is off; every Persona in a sub-folder is frozen at whatever it said when the app started, forever, with no error, no log and nothing on screen. This is the headline silent failure of the area and the reason the nested file must be tested explicitly.
- Neither reloads -> the watcher is dead altogether; every edit needs an app restart.
- The tile updates but the card's `Persona` text stays stale -> cached file text is not being refreshed with the index; a teammate's system prompt and its displayed prompt now disagree.
- The change appears only after several seconds and several saves -> the debounce has been set far too long; judge against TEAMMATESLIBRARY-22 before filing.

**Inconclusive if**

Your editor saves via a temp-file-and-rename dance and you see two repaints -> that is expected editor behaviour, not a defect; judge only whether the final state is correct. If nothing at all repaints AND the app console shows an exception, restart the app and rerun before recording.

> [!NOTE]
> Testing only the root-level file passes a broken build. The nested edit is the test.

### TEAMMATESLIBRARY-20 — The watcher notices creations and deletions while the page is open

**Free** · about 5 min

*Proves a file added or removed from outside the app appears or disappears — including its heading and its dropdown option — with no refresh.*

**Before you start**

- Core fixture set in place; browser on /teammates, freshly refreshed once.

**Steps**

1. Note the current headings and the current contents of the **Team** dropdown.
2. From outside the app, copy a new file `src\Huddle.App\App_Data\Teams\Pike.md` containing exactly:
---
name: 'Pike'
title: 'Field Lead'
alias: 'pik'
teams: ['Logistics']
---
You are Pike. Answer in one short sentence.
3. Wait about two seconds and watch the page WITHOUT refreshing.
4. Open the **Team** dropdown and read the options.
5. Delete `src\Huddle.App\App_Data\Teams\Pike.md`.
6. Wait about two seconds and watch the page again, and reopen the dropdown.

**Pass if — all of these**

- A `Pike` tile appears on its own, under a new `Logistics` heading, within about a second.
- The **Team** dropdown gains a `Logistics` option without a refresh.
- After the delete, the Pike tile disappears on its own and the `Logistics` heading disappears with it (it was the only member).
- The dropdown loses the `Logistics` option too.

**Fail if — any of these**

- The tile only appears after F5 -> a user copying in a Persona file sees nothing happen and will assume the app did not accept it.
- A deleted Persona's tile lingers -> the card would then offer Edit and Open on a file that no longer exists.
- The tiles update but the dropdown's option list does not -> the filter and the grid are reading from two different moments in time; selecting the stale option shows `No teammates in this team.`
- The heading survives after its last member is deleted -> an empty group is left behind.

**Inconclusive if**

Nothing happens at all for either step -> run TEAMMATESLIBRARY-19 first; if edits also do not reload, the watcher is dead and this test adds nothing new. Record it as blocked by -19.

### TEAMMATESLIBRARY-21 — Renaming or deleting a Team sub-folder keeps everyone inside reachable

**Free** · about 8 min

*Catches two documented watcher traps: a directory event's name is the directory, which never matches a *.md filter, so a folder rename can leave every nested Persona's path stale forever and a folder delete can leave their tiles on screen forever.*

**Before you start**

- Core fixture set in place.
- At least two Personas inside `App_Data\Teams\Household\`.

**Steps**

1. Create a second nested file `src\Huddle.App\App_Data\Teams\Household\Bram.md` containing exactly:
---
name: 'Bram'
title: 'Home Second'
alias: 'bra'
teams: ['Household']
---
You are Bram. Answer in one short sentence.
2. Go to /teammates, press F5, and confirm both `Rune` and `Bram` tiles are on the page. Click each and write down the absolute path shown in its card's `Persona file` section. Close the card.
3. PART A — RENAME. In File Explorer, rename the folder `App_Data\Teams\Household` to `App_Data\Teams\HouseholdOps`. Do not touch the browser.
4. Wait about two seconds and confirm both tiles are still on the page and still read `Home Steward · @run` and `Home Second · @bra`.
5. Click the Rune tile and read the `Persona file` path.
6. With that card open, click **Open** and note whether the correct file opens in your Markdown editor. Close the editor window.
7. Click **Edit**, add a space to the end of the body line, click **Save**, and note whether it succeeds or shows an error line on the card. Close the card.
8. PART B — DELETE. Move the whole `App_Data\Teams\HouseholdOps` folder OUT of `App_Data\Teams` (to your Desktop, say). Do not touch the browser.
9. Wait about two seconds and read the page.
10. Move the folder back in, rename it to `Household`, delete `Bram.md`, and confirm the core fixture set is restored.

**Pass if — all of these**

- PART A: both tiles stay on the page and keep their Name, Title and alias after the folder rename.
- PART A: the card's `Persona file` path now shows the NEW folder (`...\Teams\HouseholdOps\Rune.md`), not the old one.
- PART A: **Open** launches the correct, existing file.
- PART A: **Edit** -> **Save** succeeds with no error line on the card, and the change is on disk at the new path.
- PART B: within about a second of moving the folder away, BOTH tiles disappear on their own.
  The `Household` heading REMAINS, now holding only Vale: the core fixture set gives `Vale.md`
  `teams: ['Business', 'Household']`, so Household still has a member and an empty group is not
  what should be produced. The heading only goes if you also remove Vale from that Team.
- PART B: no exception appears in the `dotnet run` console.

**Fail if — any of these**

- After the rename the card still prints the OLD path -> the directory event was dropped; **Open** will fail and **Edit** -> **Save** will write to a path that no longer exists. Silent until a user tries to save.
- **Edit** -> **Save** shows an error, or appears to succeed but the file on disk is unchanged -> the same stale-path trap, now losing a user's edit.
- After the folder is moved away, the tiles linger indefinitely -> a deleted directory raises an event named after the directory, which has no extension; missing it means the app serves stale prompt text for every Persona that was inside, forever.
- The app throws when the folder disappears -> a removed directory is an ordinary event, not an error.

**Inconclusive if**

Windows refuses the folder rename because a file inside is open in your editor -> close the editor and retry; a rename that never happened proves nothing. If **Open** does nothing at all on your machine, check TEAMMATESLIBRARY-33 first — you may have no application registered for .md.

### TEAMMATESLIBRARY-22 — A burst of file writes becomes one update, not five

**Free** · about 5 min

*Proves the watcher debounces, so a multi-file copy or a branch switch does not repaint five times (and, with ACP on, does not restart five sessions).*

**Before you start**

- Core fixture set in place.
- `Team:Acp:Enabled` false (the default) — run it free first, before ever trying it with ACP on.

**Steps**

1. Prepare five valid Persona files in a scratch folder OUTSIDE `App_Data`, named `Burst1.md` … `Burst5.md`, each like this with the digit changed:
---
name: 'Burst1'
title: 'Load Tester'
alias: 'bu1'
teams: ['Business']
---
You are Burst1. Answer in one short sentence.
2. Go to /teammates and press F5.
3. Select all five files in the scratch folder and copy them into `src\Huddle.App\App_Data\Teams\` in ONE paste operation. Watch the page closely while it happens.
4. Count how many distinct repaints you see (a repaint is the tile grid visibly changing).
5. Check the `dotnet run` console and count the lines it logged for this burst.
6. Delete the five Burst files from `App_Data\Teams`.

**Pass if — all of these**

- All five tiles appear together, roughly half a second after the copy settles.
- You see ONE update, not five separate flickering repaints.
- The console shows no repeated per-file churn for the same Persona.

**Fail if — any of these**

- Five separate visible repaints -> the debounce is gone; with ACP on, each repaint restarts sessions and spawns adapter processes, which costs real money for one logical copy.
- The page shows only some of the five and never converges -> events are being lost, not coalesced.
- Repaints continue after the copy has finished -> the debounce timer is being re-armed by its own work.

**Inconclusive if**

Your copy is slow enough that the five files land more than half a second apart -> that legitimately produces more than one update; retry by copying from a local SSD folder, or record the test as not-provable on this machine rather than failing it.

> [!NOTE]
> Only repeat this with `Team:Acp:Enabled=true` if you deliberately want to watch adapter restarts — that path starts real sessions. The free run above is the one that matters.

### TEAMMATESLIBRARY-23 — A dropped-event storm still converges on what is on disk

**Free** · about 8 min

*Checks the backstop for the OS silently dropping watcher events — a permanent, unannounced desync between the page and the disk.*

**Before you start**

- Core fixture set in place.
- Ability to write many files at once.

**Steps**

1. Go to /teammates and press F5.
2. In the second PowerShell window, run this to write 50 valid Personas at once:
1..50 | ForEach-Object { "---`nname: 'Storm$_'`ntitle: 'Storm Tester'`nalias: 'st$_'`nteams: ['Storm']`n---`nYou are Storm$_." | Set-Content -Encoding utf8 "src\Huddle.App\App_Data\Teams\Storm$_.md" }
3. Wait about five seconds, then WITHOUT refreshing count the tiles under the `Storm` heading.
4. Check the `dotnet run` console for a warning mentioning a FileSystemWatcher error.
5. Now delete them all at once: `Remove-Item src\Huddle.App\App_Data\Teams\Storm*.md`
6. Wait about five seconds and, without refreshing, confirm the `Storm` heading is gone.
7. Run `Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams -Filter *.md` and compare it against the tiles plus rejected entries on screen.

**Pass if — all of these**

- After the storm settles, the page agrees exactly with the disk: every .md file on disk is either a tile or a rejected entry, and nothing on screen has no file.
- If the OS dropped events, the console carries the warning `PersonaStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.` AND the page still converges.

**Fail if — any of these**

- The page permanently disagrees with the disk — a missing tile or a lingering one that never resolves, even after a minute -> a silent, permanent desync; it clears only when something unrelated touches a file, so a user can be looking at a library that does not exist.
- The overflow warning appears and the page does NOT converge -> the error backstop is not scheduling a refresh.

**Inconclusive if**

No overflow warning ever appears (likely — the watcher buffer is raised to 64 KB specifically to make this rare) and the page converges correctly -> record as PASS-with-note: 'could not provoke a dropped-event storm; convergence verified for a 50-file burst'. Do NOT record a silent pass as if the overflow path had been exercised, and do not fail it for being unprovokable.

> [!NOTE]
> This is the hardest behaviour in the area to provoke deliberately. Its value is the convergence check, which is worth running even when no overflow occurs.

### TEAMMATESLIBRARY-24 — A tile click opens the card, and the card shows the real file path

**Free** · about 6 min

*Proves the whole tile is the click target, the card shows every View-mode field including the discovered path, and it closes the way an overlay should.*

**Before you start**

- Core fixture set in place, including the nested `Household\Rune.md`.

**Steps**

1. Go to /teammates and press F5.
2. Click the Nova tile on its coloured monogram square. Note whether the card opens.
3. Close it by clicking the × at the top right.
4. Click the Nova tile on its status line. Note whether the card opens.
5. Read the card from top to bottom and write down: the header text, the Name, the Title, the status line, the `Alias:` line, whether a `Teams:` line is present and what it says, the section headings, and the text under `Persona file`.
6. Click INSIDE the card body (on the Persona text). Note whether it closes.
7. Click the dimmed area outside the card. Note whether it closes.
8. Click the Quill tile (the Persona with no Teams) and check whether a `Teams:` line appears.
9. Close the card, then click the nested Rune tile and read its `Persona file` path.
10. In the second PowerShell window, run `Test-Path '<paste the exact path the card printed>'`.

**Pass if — all of these**

- Clicking anywhere on the tile — monogram, name, or status line — opens the card.
- The card header reads exactly `Teammate` in View mode.
- The card shows: Name `Nova`; Title `Research Lead`; a status line with a dot and `Offline`; `Alias: nov`; `Teams: Business`.
- Section headings, in order, read `Persona`, `Model`, `Effort`, `Persona file`.
- Under `Persona` is the whole raw file text including the `---` frontmatter; `Model` reads `Agent default`; `Effort` reads `Model default`.
- Clicking inside the card does NOT close it; clicking the dimmed backdrop DOES.
- Quill's card has NO `Teams:` line at all (not an empty one).
- Rune's card prints a path ending `...\App_Data\Teams\Household\Rune.md`, and `Test-Path` on it returns `True`.

**Fail if — any of these**

- Only the name is clickable -> the tile stops behaving like a directory entry.
- Rune's card prints a root path (`...\Teams\Rune.md`) -> the path is being reconstructed as `{Name}.md` at the root instead of reporting where the file was actually discovered; **Open** and **Edit** will then work on the wrong file.
- `Test-Path` returns `False` -> the card is printing a path that does not exist.
- Quill shows an empty `Teams:` line -> a teammate with no Teams reads as having a blank Team rather than none.
- After switching from one tile to another, the previous teammate's Persona text is still shown -> card state is not being cleared between opens.
- A `node` process appears when you click a tile -> View mode is probing the adapter; opening a card to read it must never spawn anything.

**Inconclusive if**

The card does not open at all and other clicks on the page are also dead -> the Blazor circuit has dropped; reload and rerun.

### TEAMMATESLIBRARY-25 — Offline is the honest default, with no invented reason

**Free** · about 4 min

*Proves that with ACP off every teammate reads Offline with no tooltip, and that Restart (with its warning) is offered exactly there.*

**Before you start**

- App started WITHOUT `--Team:Acp:Enabled=true` (the ship default).
- Core fixture set in place.
- No Persona named `echo` or `alpha` present.

**Steps**

1. Go to /teammates and press F5.
2. Read the status label and dot colour on every tile.
3. Hover the mouse over a tile's status line for three seconds and note whether a tooltip appears.
4. Click the Nova tile and read the lines under the Name.
5. Note which buttons are in the card's action row, and read any hint sentence below it.
6. In `T-B` run `O-ADAPTERS` and, if you have sqlite3, `sqlite3 src/Huddle.App/App_Data/team.db "select id,name,kind from users;"`.

**Pass if — all of these**

- Every tile reads `Offline` with a red dot (`agent-dot offline`).
- Hovering a tile's status line shows NO tooltip — no reason is known, so none is invented.
- The card shows the same `Offline` status line and NO reason paragraph under it.
- The card's action row contains `Edit`, `Open`, `Restart` and `Remove` (a `Message` link appears only if that teammate already has a Room).
- A hint below the action row reads `Like saving an edit, this restarts the teammate, which clears what it remembers.`
- `O-ADAPTERS` prints `0`.
- In `users`, the only `agent` rows are the demo agents `echo` and `alpha`.

**Fail if — any of these**

- A tile reads `Online` when nothing is running -> the badge is not derived from real presence; a user will message a teammate that cannot answer.
- A tooltip appears with a reason although nothing has ever started -> a reason is being fabricated.
- No `Restart` button appears on an Offline teammate -> the user can see the failure but cannot act on it.
- A `Restart` button appears on a HEALTHY teammate (check again after TEAMMATESLIBRARY-26) -> Restart is meant to be offered only for Offline or Degraded.

**Inconclusive if**

Some tiles read Online -> check whether you left ACP enabled from a later test, or whether a Persona is named after a demo agent; restart the app without the flag and rerun.

> [!NOTE]
> Every tile reading Offline on a stock install is CORRECT and is listed as a known limit. Do not file it.

### TEAMMATESLIBRARY-26 — Online, proved for free through a demo agent

**Free** · about 5 min

*Gets a real green Online badge with ACP off and no money spent, by naming a Persona after a connected demo agent.*

**Before you start**

- App started WITHOUT `--Team:Acp:Enabled=true`.
- `Team:DemoAgent:Enabled` is true (the shipped default) with names `echo` and `alpha`.
- Core fixture set in place.

**Steps**

1. In the `dotnet run` console, search the startup output for the line `Demo agent echo connected.` Confirm it is there.
2. Create `src\Huddle.App\App_Data\Teams\echo.md` containing exactly:
---
name: 'echo'
title: 'Demo Agent'
alias: 'ech'
teams: ['Business']
---
You are echo. Answer in one short sentence.
3. Go to /teammates and press F5.
4. Read the status label and dot colour on the `echo` tile, and on every other tile.
5. Click the `echo` tile and read its status line and the action row.
6. If you have sqlite3, run `sqlite3 src/Huddle.App/App_Data/team.db "select id,name,kind from users;"`.

**Pass if — all of these**

- The `echo` tile reads `Online` with a green dot.
- Every other tile still reads `Offline` with a red dot (`agent-dot offline`).
- The echo card shows `Online`, no reason paragraph, and NO `Restart` button in its action row (Restart is offered only for Offline or Degraded).
- The `users` table has an `agent` row named `echo`.
- No `node` process is running — this Online badge costs nothing.

**Fail if — any of these**

- The `echo` tile still reads `Offline` -> the page is failing to join the Persona's frontmatter Name to the connected directory user; that join is case-insensitive by design, so a case mismatch is not an excuse.
- Every tile reads `Online` -> presence is not being resolved per teammate.
- A `Restart` button appears on the Online echo card -> Restart is being offered for a healthy teammate, which just throws away its memory for nothing.

**Inconclusive if**

The console has no `Demo agent echo connected.` line -> the demo agents are disabled or failed to connect; check `appsettings.json` for `Team:DemoAgent:Enabled` and look for `Demo agent echo failed to connect to pipe`. Without a connected agent this test cannot be judged.

> [!NOTE]
> Keep `echo.md` in place for TEAMMATESLIBRARY-38, which needs a Persona whose pipe is live while its health is Offline.

### TEAMMATESLIBRARY-27 — Badges repaint live on connect and disconnect, with no refresh

**Free** · about 6 min

*Proves the tile and the open card both follow presence changes by themselves, and that leaving the page does not throw — the shape of a leaked event subscription.*

**Before you start**

- App running with ACP OFF.
- Core fixture set in place.
- PowerShell available to run `tools/echo-bot.ps1`.

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\Scout.md` containing exactly:
---
name: 'Scout'
title: 'Pipe Tester'
alias: 'sct'
teams: ['Business']
---
You are Scout. Answer in one short sentence.
2. In a third PowerShell window at the repo root, run `pwsh tools/echo-bot.ps1 -Name Scout` and confirm it prints that it connected.
3. Go to /teammates and press F5 ONCE (this is the only refresh in this test; it lets the page learn the new agent's id).
4. Confirm the `Scout` tile reads `Online` with a green dot.
5. Click the Scout tile so its card is open, and leave it open.
6. In the third PowerShell window, press Ctrl+C to stop the echo bot.
7. Watch the page for up to five seconds WITHOUT refreshing: read the tile's dot and label, and the open card's status line.
8. Run `pwsh tools/echo-bot.ps1 -Name Scout` again and watch the same two places, again without refreshing.
9. Close the card, click another page in the sidebar (e.g. **Settings**), then come back to **Teammates**. Check the `dotnet run` console for exceptions.
10. Stop the echo bot and delete `Scout.md`.

**Pass if — all of these**

- After the bot is stopped, both the tile AND the open card change from `Online` to `Offline` on their own, within a few seconds, with no refresh.
- The tile and the card never disagree — they change at the same moment and to the same value.
- After the bot reconnects, both go back to `Online` by themselves.
- Navigating away from /teammates and back produces no exception in the console and no yellow `An unhandled error has occurred.` banner.

**Fail if — any of these**

- You must press F5 to see the change -> the page is not subscribed to presence; a user watching a teammate go down sees a green dot indefinitely.
- The tile updates while the open card stays stale -> two surfaces disagree about one teammate.
- An exception appears in the console when you navigate away -> a component failed to unsubscribe from a singleton event; that leak throws on every circuit disconnect and never dies.

**Inconclusive if**

The echo bot cannot connect (`Connecting to pipe...` then an error) -> the pipe name may differ from the default `team`; check `Team:PipeName` in appsettings.json and pass `-Pipe <name>`. Without a connecting client this test cannot be judged.

> [!NOTE]
> The very FIRST connection of a brand-new agent name may not flip the tile without one refresh, because the page learns the agent's id when it loads its data. That is why step 3 refreshes once; judge only the disconnect and reconnect that follow.

### TEAMMATESLIBRARY-28 — Moving a Persona file between Team sub-folders is a complete no-op

**Free** · about 10 min

*The highest-value identity test in the area: proves a file's location is storage only, so moving it cannot change its Name, its Team, its Model or its Effort.*

**Before you start**

- Core fixture set in place.
- A Model and an Effort stored for the Persona under test. If an ACP adapter is installed (`tools\acp\node_modules` exists), set them through the card as in step 2. If not, use the sqlite3 fallback in the notes.

**Steps**

1. Create `src\Huddle.App\App_Data\Teams\Business\Mira.md` (creating the `Business` folder) containing exactly:
---
name: 'Mira'
title: 'Field Analyst'
alias: 'mir'
teams: ['Business']
---
You are Mira. Answer in one short sentence.
2. Go to /teammates, press F5, click the Mira tile, click **Edit**, choose the **Haiku** entry in the **Model** dropdown and **low** in the **Effort** dropdown, then click **Save**.
3. Reopen the Mira card in View mode and write down all six facts: Name, Title, alias, group heading, the `Model` line, the `Effort` line, and the `Persona file` path. Close the card.
4. If you have sqlite3, run `sqlite3 src/Huddle.App/App_Data/team.db "select * from persona_models; select * from persona_efforts;"` and note the rows for Mira.
5. With the browser open on /teammates and untouched, MOVE the file `Business\Mira.md` to `src\Huddle.App\App_Data\Teams\` (drag in Explorer, or `Move-Item src\Huddle.App\App_Data\Teams\Business\Mira.md src\Huddle.App\App_Data\Teams\`).
6. Wait about two seconds and read the page WITHOUT refreshing.
7. Click the Mira tile and compare all six facts with what you wrote down.
8. MOVE the file again, this time into `src\Huddle.App\App_Data\Teams\Household\`. Wait two seconds, reopen the card, and compare the six facts again.
9. If you have sqlite3, re-run the query and compare the rows.
10. Move the file back out and delete it, and delete the now-empty `Business` folder.

**Pass if — all of these**

- After each move the page repaints on its own within about a second.
- Name, Title, alias, group heading (`Business`), `Model` and `Effort` are IDENTICAL before and after each move.
- The ONLY thing that differs is the `Persona file` path, which shows the new location.
- The `persona_models` and `persona_efforts` rows are still keyed by the frontmatter Name `Mira` and still hold the same values after both moves.
- Mira stays under the `Business` heading even while its file sits in the `Household` folder.

**Fail if — any of these**

- The `Model` reverts to `Agent default` or the `Effort` to `Model default` after a move -> a path-derived database key has been reintroduced; this is the exact silent identity change that Teams-as-a-field exists to prevent, and nothing on screen announces it.
- The group heading follows the folder -> grouping is folder-derived (see also TEAMMATESLIBRARY-06).
- The tile disappears and reappears as a different teammate -> identity is being rebuilt from the path.
- The tile vanishes permanently after a move INTO a sub-folder -> the watcher is missing moves into nested folders.

**Inconclusive if**

No adapter is installed, so the Model dropdown offers only `Use the agent's default` and no Model can be stored through the UI -> use the sqlite3 fallback: stop the app, run `sqlite3 src/Huddle.App/App_Data/team.db "insert into persona_models(persona_name,model) values('Mira','claude-haiku-4-5'); insert into persona_efforts(persona_name,effort) values('Mira','low');"`, restart the app, and read the values off the card before moving. If you have no sqlite3 either, run the test without the Model/Effort half and say so — but note that the Model/Effort half is the part that catches the silent failure.

> [!NOTE]
> This is manual checklist step 20 and the test most likely to regress if someone reintroduces a path-derived key. Honour the standing convention: Model = Haiku, Effort = low.

### TEAMMATESLIBRARY-29 — New teammate writes to the Teams root, with the Teams field it was given

**Free** · about 6 min

*Proves the create path composes a file that round-trips through the parser, at the root, never guessing a sub-folder.*

**Before you start**

- Core fixture set in place, INCLUDING at least one Team sub-folder (`Household\`), so "never guesses a sub-folder" is actually exercised.
- No Persona named `Wren` exists.

**Steps**

1. Go to /teammates, press F5, and click **New teammate**.
2. Read the four identity field labels and the hint under the Teams field.
3. Type `Wren` into **Name**, `Ops Lead` into **Title**, `wre` into **Alias**, and `Business, Household` into **Teams**.
4. Type `You are Wren. Answer in one short sentence.` into the **Persona body** textarea.
5. If the **Model** dropdown offers real models, choose the **Haiku** entry and choose **low** in **Effort**. If it only offers `Use the agent's default`, leave both alone and note it.
6. Click **Add teammate**.
7. Read the card that is now on screen: its header, Name, and `Teams:` line. Close it.
8. Read where the Wren tile appears on the page.
9. In `T-B` run `Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams -Filter Wren.md` and then `Get-Content src\Huddle.App\App_Data\Teams\Wren.md`.
10. Confirm there is no `Files that didn't load` block on the page.

**Pass if — all of these**

- The Teams field's hint reads `Comma-separated. Leave blank for no team.`
- After **Add teammate** the card switches to View mode: header `Teammate`, Name `Wren`, and a line reading `Teams: Business, Household`.
- A Wren tile appears under BOTH the `Business` and the `Household` headings.
- The file is at `src\Huddle.App\App_Data\Teams\Wren.md` — at the ROOT, not inside `Household\` or any other sub-folder, and it is the only Wren.md anywhere in the tree.
- Its contents are lowercase, single-quoted frontmatter: `name: 'Wren'`, `title: 'Ops Lead'`, `alias: 'wre'`, `teams: ['Business', 'Household']`, then `---`, then the body.
- No `Files that didn't load` entry appears for the file just created.

**Fail if — any of these**

- The file lands inside a sub-folder -> the app is choosing a user's filing location for them.
- `teams:` reads `['Business, Household']` (one Team) -> the comma list typed by the user is not being split; the user gets one nonsense Team.
- A `teams:` line is written even when the Teams field was left blank (test this separately by creating one with Teams empty) -> an empty field is not how a person writes 'no Teams'.
- The newly created file appears in the `Files that didn't load` block -> what the writer composes does not parse with what the reader requires; loud, but a real round-trip break.
- The card closes outright instead of landing on the new teammate -> the user cannot read back what they just made.

**Inconclusive if**

The Model dropdown is empty of real models -> that means no adapter is installed; the rest of this test still stands. Record that the Model/Effort half was skipped.

> [!NOTE]
> Delete `Wren.md` afterwards unless TEAMMATESLIBRARY-30 or -32 uses it.

### TEAMMATESLIBRARY-30 — A save that would create a rejected file is refused before anything is written

**Free** · about 8 min

*Proves the write path validates first — no file is written and then explained, and nothing the user typed is thrown away.*

**Before you start**

- Core fixture set in place: `Nova.md` at the root (alias `nov`) and `Household\Rune.md` nested.

**Steps**

1. In `T-B` run `Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams -Filter *.md | Measure-Object` and write down the Count.
2. Click **New teammate**. Type Name `Nova`, Title `Clash`, Alias `cl1`, Teams `Business`, body `You are a clash test.` Click **Add teammate**. Record the message shown and whether your typed text is still in the fields.
3. Change only the Name to `Rune` (a Persona that lives in a SUB-FOLDER) and click **Add teammate** again. Record the message.
4. Change only the Name to `rune` (lower case) and click **Add teammate**. Record the message.
5. Change the Name to `Clash1` and the Alias to `nov` (Nova's alias) and click **Add teammate**. Record the message.
6. Change the Alias to `cl1`, clear the **Persona body** textarea completely, and click **Add teammate**. Record the message.
7. Type `You are a clash test.` back into the body, change the Name to `Emily  Lee` (TWO spaces), and click **Add teammate**. Record the message.
8. Change the Name to `Clash1` and the Alias to `-nope`, and click **Add teammate**. Record the message.
9. Click **Cancel** to close the card.
10. Re-run the file count command and compare with the number you wrote down. Confirm no new entry appeared in a `Files that didn't load` block.

**Pass if — all of these**

- Every attempt keeps the card OPEN and shows a message in red at the top of the card body; nothing you typed is lost.
- Name `Nova` -> `Persona 'Nova' already exists.`
- Name `Rune` -> `Persona Name 'Rune' is also used by '<absolute path of Household\Rune.md>'.` — the sub-folder collision is caught even though no `Rune.md` exists at the root.
- Name `rune` -> `Persona Name 'rune' is also used by '<absolute path of Household\Rune.md>'.` — a case-only difference still collides.
- Alias `nov` -> `Persona Alias 'nov' is also used by '<absolute path of Nova.md>'.`
- Blank body -> `Persona text must not be blank.`
- Name `Emily  Lee` -> `'Emily  Lee' is not a valid Persona name.`
- Alias `-nope` -> `'-nope' is not a valid Persona alias.`
- The .md file count is UNCHANGED across every failed attempt, and the rejected block gained no entry.

**Fail if — any of these**

- The file count goes up after any refusal -> the file was written and then explained; the write path is validating after the fact.
- A new entry appears in `Files that didn't load` immediately after a save -> same thing, the loud version.
- The card closes on error and discards everything typed -> the user retypes their Persona body from scratch.
- The `Rune` or `rune` attempt succeeds -> only the root path was checked for existence; two teammates now share one Name (and one database row) and one of them silently loses its Model.

**Inconclusive if**

A message appears but its wording differs slightly from the text above -> record the exact wording you saw and flag it as a wording drift rather than a functional failure, unless the message fails to name the offending field or the colliding file, which IS a failure.

### TEAMMATESLIBRARY-31 — Renaming through the frontmatter: the tile renames, Model and Effort follow, the file does not

**Free** · about 10 min

*Proves a Name change through the card's textarea carries the stored Model and Effort with it and does not rename the file — and documents the ghost it leaves behind.*

**Before you start**

- A Persona with a stored Model and Effort. Reuse `Mira` from TEAMMATESLIBRARY-28, or set Model = Haiku and Effort = low on a fresh Persona through the card (or via the sqlite3 fallback in -28's notes).

**Steps**

1. Go to /teammates, press F5, click the Mira tile and write down its `Model` line, its `Effort` line and its `Persona file` path. Close the card.
2. Click the Mira tile again and click **Edit**.
3. Read the hint under the textarea.
4. In the textarea (which holds the WHOLE file, frontmatter included), change `name: 'Mira'` to `name: 'Mira Prime'`. Change nothing else. Click **Save**.
5. Close whatever card is on screen, then look at the tile grid: find the renamed tile and read its Name and monogram.
6. Click the renamed tile and read its `Model`, `Effort` and `Persona file` lines.
7. In `T-B` run `Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams -Filter Mira*.md`.
8. If you have sqlite3, run `sqlite3 src/Huddle.App/App_Data/team.db "select * from persona_models; select * from persona_efforts;"`.
9. Look at the left sidebar and note whether a Room named after the OLD name is still listed.

**Pass if — all of these**

- The Edit hint reads `Markdown. This is the whole Persona file, front matter included, and becomes the teammate's system prompt. Saving restarts it, which clears what it remembers.`
- A tile now reads `Mira Prime` with monogram `MP`, and no tile reads `Mira`.
- The renamed teammate's card shows the SAME `Model` and the SAME `Effort` as before the rename.
- On disk the file is STILL called `Mira.md` — identity is frontmatter, not filename — and the card's `Persona file` path still ends `Mira.md`.
- `persona_models` and `persona_efforts` hold a row keyed `Mira Prime` and NO row keyed `Mira`.
- Any Room or transcript that existed under the old name is still in the sidebar under the old name (this is the documented ghost, and is expected).

**Fail if — any of these**

- The `Model` or `Effort` reverts to `Agent default` / `Model default` after the rename -> the stored rows were not moved, and a user's settings vanished silently.
- A row keyed under the OLD name is left behind -> recreating a teammate with the old name later would silently resurrect a setting nobody chose.
- The file is renamed on disk -> identity is being written back to the filename.
- Both an old and a new tile appear -> the index is not replacing the old entry.

**Inconclusive if**

After Save, the card on screen looks empty (no Title, no Persona text, no `Persona file` section) with the OLD name in it -> that is the page reopening the card by the name it was opened with, and is a known rough edge, not the failure this test is judging. Close it and click the renamed TILE instead; judge from the tile and the reopened card. Record the empty-card observation as a note.

> [!NOTE]
> The ghost — the old Agent, its Room and its transcript remaining — is a documented known limit, not a bug. Only Model and Effort follow a rename.

### TEAMMATESLIBRARY-32 — Remove takes the file, the Model and the Effort — and nothing else

**Free** · about 8 min

*Proves removal is confirm-gated, deliberately non-cascading, and leaves no setting behind to resurrect.*

**Before you start**

- A Persona with a stored Model = Haiku and Effort = low (reuse Mira/Mira Prime from -31, or set them on a fresh one).
- Ideally the `echo` Persona from -26, which has a real Room, so the non-cascade half can be observed.

**Steps**

1. Go to /teammates, press F5, click the teammate's tile and note its group heading and whether its Team has any other member.
2. In the card, click **Remove**. Read the button row that appears.
3. Click **Cancel**. Confirm the row returns to its previous buttons and nothing was deleted.
4. Click **Remove** again, then click **Confirm**.
5. Read the page: is the card closed, is the tile gone, is its group heading gone (if it was the last member), and is its Team still in the **Team** dropdown?
6. In the second PowerShell window confirm the file is gone: `Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams -Filter '<that name>.md'`.
7. If you have sqlite3: `sqlite3 src/Huddle.App/App_Data/team.db "select * from persona_models; select * from persona_efforts; select id,name,kind from users;"` and also check the `rooms` table.
8. Look at the left sidebar: is the removed teammate's Room (if it had one) still listed, with its messages intact?
9. Now click **New teammate** and recreate a Persona with the SAME Name, Title and Alias, leaving Model and Effort untouched. Click **Add teammate**.
10. Read the recreated teammate's `Model` and `Effort` lines on the card.

**Pass if — all of these**

- **Remove** does not delete on the first click: the row swaps to `Confirm` and `Cancel`.
- `Cancel` restores the previous button row and deletes nothing.
- `Confirm` closes the card, removes the tile, removes the group heading if it was that Team's last member, and removes that Team from the **Team** dropdown if nobody else names it.
- The `.md` file is gone from disk.
- `persona_models` and `persona_efforts` hold NO row for that name.
- Any Room and transcript the teammate had are STILL in the sidebar and still readable — removal is deliberately non-cascading.
- The recreated Persona of the same Name reads `Model: Agent default` and `Effort: Model default`.

**Fail if — any of these**

- One click deletes with no confirmation -> a destructive action with no guard.
- The recreated teammate shows the OLD Model or Effort -> a setting nobody chose has been resurrected; the row survived the removal.
- The Room or the transcript disappears -> removal cascaded; chat history a user expected to keep is gone.
- The tile lingers after Confirm -> the index is not being refreshed on the write path.

**Inconclusive if**

The teammate never had a Room (nothing ever connected as it), so the non-cascade half cannot be observed -> run this against the `echo` Persona instead: open its card, click **Message** (or use **New chat** in the sidebar, tick `echo`, click **Start chat**), send `hi`, then rerun. Messaging a demo agent is free.

### TEAMMATESLIBRARY-33 — Open launches the Persona file on the SERVER, including for a nested file

**Free** · about 4 min

*Proves the Open action opens the real discovered file, and fails with a message on the card rather than taking the page down.*

**Before you start**

- The app and the browser are on the same machine (the expected single-user setup).
- Core fixture set in place, including the nested `Household\Rune.md`.

**Steps**

1. Go to /teammates, press F5, click the Nova tile, and click **Open**.
2. Note whether an editor opens, and read the path in its title bar.
3. Close the editor. Close the card.
4. Click the NESTED Rune tile and click **Open**.
5. Read the path in the editor's title bar and compare it with the card's `Persona file` line.
6. Close the editor and the card, and check the `dotnet run` console and the page for errors.

**Pass if — all of these**

- **Open** launches whatever the machine has registered for `.md`, showing the real file.
- For the nested Rune, the file opened is `...\App_Data\Teams\Household\Rune.md` — the nested file, not a root path.
- The path in the editor matches the card's `Persona file` line exactly.
- No yellow `An unhandled error has occurred.` banner appears, and the page stays usable.

**Fail if — any of these**

- Nothing happens and no message appears anywhere -> the user cannot tell whether the click registered.
- The wrong file opens for the nested Persona -> the path is being reconstructed rather than read from the index.
- The yellow `An unhandled error has occurred.` banner appears -> an unhandled exception is taking down the circuit; a failed launch must show a line like `Could not open 'Nova': ...` on the card instead.

**Inconclusive if**

Nothing opens AND the card shows an error line beginning `Could not open '...'` -> that is the handled path working; your machine simply has no application registered for `.md`. Register one (or set Notepad as the default for .md) and rerun, or record the test as environment-blocked with the handled-error observation noted.

> [!NOTE]
> This action launches a process on the machine running the app, not the one running the browser. That is deliberate and documented.

### TEAMMATESLIBRARY-34 — Neither a page load, a filter change, nor a View card ever spawns an adapter

**Free** · about 6 min

*Proves the model-catalog probe is confined to the Create/Edit card, so browsing the directory never starts a process.*

**Before you start**

- An ACP adapter is installed: `tools\acp\node_modules` exists. (If it does not, run `pwsh tools/acp/install.ps1` or record this test as inconclusive.)
- Core fixture set in place. Works with `Team:Acp:Enabled` either true or false — the probe is deliberately not gated by that flag.

**Steps**

1. In `T-B` run `O-ADAPTERS-LIST` and note every PID (probably none).
2. In the browser, navigate to http://localhost:5100/teammates.
3. Run `O-ADAPTERS-LIST` again.
4. Open the **Team** dropdown and select each Team in turn, ending on `All teams`.
5. Run `O-ADAPTERS-LIST` again.
6. Click a tile to open its card in View mode. Read it, then run `O-ADAPTERS-LIST` again.
7. Now click **Edit** on that card. Wait for the Model dropdown's hint to settle, then run `O-ADAPTERS-LIST` again.
8. Check the `dotnet run` console for probe lines.
9. Click **Cancel** to close the card.

**Pass if — all of these**

- The set of `node` PIDs is unchanged after the page load.
- It is unchanged after every filter change.
- It is unchanged after opening a View card.
- It CHANGES (a new node process appears, or the console logs a probe) only after **Edit** (or **New teammate**) is clicked — that is the expected place for it.
- No probe log line (`No ACP adapter is installed; the model catalog is empty.`, or a probe warning) appears in the console before a Create/Edit card is opened.

**Fail if — any of these**

- A `node` process appears on page load or on a filter change -> the adapter is being probed for browsing; it spends no tokens, so this is silent and shows up only as latency and a process — which is exactly why it is worth watching by hand.
- A `node` process appears when a View card is opened -> reading a teammate must never start anything.

**Inconclusive if**

No adapter is installed, so no probe could ever spawn -> this test cannot distinguish 'correctly never probes' from 'has nothing to probe'. Install the adapter (`pwsh tools/acp/install.ps1`) or record the test as inconclusive; do not record a pass.

### TEAMMATESLIBRARY-35 — Restart from the card is NOT gated by Team:Acp:Enabled — judge and record

**Free** · about 5 min

*Establishes what the Restart button actually does when ACP is configured off, since only the supervisor's startup path checks that flag.*

**Before you start**

- App started WITHOUT `--Team:Acp:Enabled=true` (ACP off).
- An ACP adapter IS installed (`tools\acp\node_modules` exists).
- Core fixture set in place; at least one tile reading `Offline`.

**Steps**

1. In `T-B` run `O-ADAPTERS-LIST` and note the PIDs.
2. Confirm the app was started with no `--Team:Acp:Enabled` argument (check the `dotnet run` command line you used).
3. Go to /teammates, press F5, click an Offline teammate's tile (e.g. Nova) and read the hint under the action row.
4. Click **Restart**. Start watching `O-ADAPTERS-LIST` and the `dotnet run` console immediately.
5. Within ten seconds, run `O-ADAPTERS-LIST` again and check `Get-ChildItem src\Huddle.App\App_Data\work`.
6. Read the tile's and the card's status labels.
7. Do NOT send the teammate a message. Close the card.

**Pass if — all of these**

- Record what actually happened, precisely: whether a new `node` process appeared, whether `App_Data\work\<Persona>\` was created, and whether the badge moved (`Offline` -> `Starting` -> `Online`).
- Whatever happens, the page does not crash and the card shows any failure as a message line rather than a yellow error banner.
- The hint under the action row reads `Like saving an edit, this restarts the teammate, which clears what it remembers.`

**Fail if — any of these**

- The page throws, or the yellow `An unhandled error has occurred.` banner appears -> a restart failure is crashing the circuit instead of reporting itself on the card.
- The button does nothing at all and no message appears -> the user has a control that silently does nothing.

**Inconclusive if**

No adapter installed -> the Restart will simply fail with a reason (see TEAMMATESLIBRARY-37); that is a different test. Record this one as not-applicable on this machine.

> [!NOTE]
> Judge this carefully rather than filing it blind. Starting a session spends no tokens, but it genuinely brings a teammate online, and any message sent to it afterwards spends real money. The docs say `Team:Acp:Enabled` exists because ACP spends money, and nothing documents this door — so report exactly what you observed and flag the gap. Do NOT message the teammate as part of this test.

### TEAMMATESLIBRARY-36 — Restart is busy-flagged and cannot be double-fired

**Free** · about 5 min

*Proves one teammate cannot be handed two adapter processes by an impatient double click.*

**Before you start**

- An ACP adapter installed, and a teammate currently reading `Offline` or `Degraded`.
- Ideally follows straight on from TEAMMATESLIBRARY-35 so a restart is slow enough to watch (a real adapter start takes a second or two).

**Steps**

1. In `T-B` run `O-ADAPTERS` and count the processes.
2. Open an Offline teammate's card.
3. Click **Restart** and immediately click it two more times as fast as you can.
4. Watch the button's label and whether it responds to the extra clicks.
5. When it settles, read the button row again.
6. Run `O-ADAPTERS` and compare the count with step 1.

**Pass if — all of these**

- While the restart is in flight the button reads `Restarting…` and is disabled — the extra clicks do nothing.
- When it finishes, the button returns to `Restart`, or disappears entirely if the teammate is now `Online`.
- The `node` process count rises by at most ONE, not by two or three.

**Fail if — any of these**

- Two or three new `node` processes appear for one teammate -> the busy flag is gone; a double click doubles a teammate's adapter processes.
- The button stays disabled forever after a failed restart -> the user can never retry.
- A restart failure crashes the circuit instead of showing its message on the card -> an expected failure is being treated as fatal.

**Inconclusive if**

The restart completes too fast to see the `Restarting…` label -> judge on the `node` process count alone and say that the label transition was too fast to observe. If no adapter is installed, the restart fails instantly and this test cannot be judged.

### TEAMMATESLIBRARY-37 — Offline WITH a reason, in both of its homes

**Free** · about 5 min

*Proves that when a start genuinely fails, the exact reason reaches the user twice — as a tile tooltip and as a full line on the card — and clears when the cause is fixed.*

**Before you start**

- Core fixture set in place.
- You are willing to rename `tools\acp\node_modules` aside and back. No adapter process ever starts in this configuration, so nothing is spent.

**Steps**

1. Stop the app with Ctrl+C in the `dotnet run` terminal.
2. Rename the folder: `Rename-Item tools\acp\node_modules node_modules.off`
3. Restart the app with ACP ON: `dotnet run --project src/Huddle.App -- --Team:Acp:Enabled=true`
4. Go to http://localhost:5100/teammates and press F5.
5. Read the status label on every tile.
6. HOVER the mouse over the Nova tile's status line for three seconds and read the tooltip.
7. Click the Nova tile and read the line directly under its status line.
8. Check the `dotnet run` console for a warning naming Nova.
9. Restore the adapter: `Rename-Item tools\acp\node_modules.off node_modules`
10. On the still-open Nova card, click **Restart** and watch the badge on both the tile and the card.

**Pass if — all of these**

- Every tile reads `Offline`.
- Hovering a tile's status line shows a tooltip carrying exactly: `No ACP adapter is installed for Persona 'Nova'. Run tools/acp/install.ps1 (or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.`
- The card shows that SAME text as a full paragraph line under the status — not as a tooltip.
- The console logs a warning `Persona 'Nova' failed to start.`
- After restoring node_modules and clicking **Restart**, the badge moves `Offline` -> (briefly `Starting`) -> `Online`, and the reason disappears from both the tooltip and the card.
- No `node` process runs while the adapter is renamed aside — no tokens, no money.

**Fail if — any of these**

- A tile reads `Offline` with NO tooltip although a reason exists -> the diagnostic is the entire value of this state; without it the user cannot tell a never-started teammate from a failed one.
- The reason appears only on the tile, or only on the card -> the two homes have drifted apart.
- The reason wording uses `bot`, `backend`, `LLM`, `rate limit` or `quota` -> the Reason is human-facing interface copy with a binding glossary; those words are banned.
- After restoring the adapter, Restart leaves the stale reason on screen -> the reason is not cleared when the condition clears.

**Inconclusive if**

You cannot rename `node_modules` because a process has it open -> stop the app first (step 1), and close any editor with files open under it. If the app fails to start with the `--Team:Acp:Enabled=true` argument, check the argument spelling — it must be exactly `--Team:Acp:Enabled=true` after a bare `--`.

> [!NOTE]
> Put `node_modules` back before moving on; TEAMMATESLIBRARY-38 needs it renamed aside again, so you may prefer to run -38 immediately after this one.

### TEAMMATESLIBRARY-38 — Health outranks pipe liveness: a live pipe with a failed start must read Offline

**Free** · about 8 min

*The one browser-visible proof of the precedence rule, and of the silent failure it prevents — a runner whose loops have died leaves its pipe open, so connectivity-first reporting shows a deaf teammate as healthy.*

**Before you start**

- `Team:DemoAgent:Enabled` true (the shipped default) with `echo` among its names.
- The `echo.md` Persona from TEAMMATESLIBRARY-26 in place.
- `tools\acp\node_modules` renamed aside so no adapter can be found.

**Steps**

1. Stop the app with Ctrl+C.
2. Confirm the adapter is unfindable: `Test-Path tools\acp\node_modules` must return `False` (rename it aside if not: `Rename-Item tools\acp\node_modules node_modules.off`).
3. Confirm `src\Huddle.App\App_Data\Teams\echo.md` exists with `name: 'echo'`.
4. Start the app with ACP ON: `dotnet run --project src/Huddle.App -- --Team:Acp:Enabled=true`
5. In the console, confirm BOTH of these appear: `Demo agent echo connected.` AND a warning `Persona 'echo' failed to start.`
6. Go to http://localhost:5100/teammates and press F5.
7. Read the `echo` tile's status label and dot colour.
8. Hover its status line and read the tooltip.
9. If you have sqlite3, run `sqlite3 src/Huddle.App/App_Data/team.db "select id,name,kind from users;"` and confirm the `echo` agent row is there.
10. Restore the adapter (`Rename-Item tools\acp\node_modules.off node_modules`) and restart the app without the ACP flag when you are finished.

**Pass if — all of these**

- Both facts are true at once: the console shows `Demo agent echo connected.` (the pipe is live) AND `Persona 'echo' failed to start.` (health says Offline).
- The `echo` tile reads `Offline`, not `Online`.
- Hovering it shows the start-failure reason in its tooltip.
- The `users` table still holds the connected `echo` agent row — the pipe really is up, which is the whole point.

**Fail if — any of these**

- The `echo` tile reads `Online` -> precedence has been reversed: connectivity is being checked before health. This is the documented silent failure — a teammate whose loops have died keeps a green badge for as long as its dead pipe stays open, it compiles, and it passes every test written before health existed.
- The tile reads Offline but carries NO reason -> half the signal is lost; the user sees a fault with no diagnosis.

**Inconclusive if**

Only one of the two console lines appears -> the setup is not in the state this test needs. No `Demo agent echo connected.` means the demo agents are off or failed; no `Persona 'echo' failed to start.` means ACP is not actually enabled or the adapter is still findable. Fix the setup and rerun; do not judge the badge until both lines are present.

> [!NOTE]
> This is the only combination that can prove the ordering through a browser, and it is entirely free.

### TEAMMATESLIBRARY-39 — The whole page in Dark mode, with a card open and a rejected file present

**Free** · about 8 min

*Catches colour literals that survived tokenisation, in exactly the places no automated test in this repo can see — no test here renders a browser.*

**Before you start**

- Core fixture set in place.
- App running (ACP state irrelevant).

**Steps**

1. Break one file so the red block is on screen: open `src\Huddle.App\App_Data\Teams\Quill.md` and delete its `title: 'Scribe'` line. Save.
2. Click **Settings** in the left sidebar, then the **Appearance** tab.
3. In the **Theme** dropdown choose `Dark`. The page will reload fully — that is expected and documented.
4. Click **Teammates** in the sidebar.
5. Inspect, one at a time, and note any element that is still light: the page background; the `Files that didn't load` block's background, border and text; the `Team` filter's label and dropdown; each group heading and its underline; each tile's background; a tile's hover state (move the mouse over it); the monogram square; the status dot.
6. Click a tile to open the card over the page and inspect: the card panel's background, the dimmed backdrop, the header, the `<pre>` block holding the Persona text, and the `Persona file` path line.
7. Click **Edit** on the card and inspect the textarea, the **Model** dropdown and the **Effort** dropdown.
8. Close the card. Return to Settings -> Appearance and set the Theme back to `Light` (or `System`), then restore Quill's `title:` line.

**Pass if — all of these**

- Every surface listed is dark: no element keeps a white or near-white background, and no text becomes unreadable (dark-on-dark or light-on-light).
- The `Files that didn't load` block keeps a clearly distinct danger treatment (a red-family border/accent) that reads correctly against the dark background — it is still obviously a warning.
- The card panel, its backdrop, the `<pre>` Persona block and both dropdowns are all dark.
- Tile hover produces a visible, dark-appropriate change, not a white flash.

**Fail if — any of these**

- Any element stays light -> a colour literal survived tokenisation in that rule. The rejected block (its own danger palette) and the card overlay are the likeliest stragglers precisely because no automated test in this repo renders a browser.
- Text becomes unreadable anywhere -> a foreground token was changed without its background, or vice versa.
- The dark theme does not apply at all after the reload -> the theme choice is not reaching the document head.

**Inconclusive if**

The theme does not change and the page looks identical -> confirm `src\Huddle.App\App_Data\appearance.json` was written with your choice; if it was not, the failure is in Settings -> Appearance, not on this page, and belongs to that area. Note that the Theme is per installation (a file), not per browser — a second browser or a private window will show the same theme, which is expected.

> [!NOTE]
> `src\Huddle.App\wwwroot\theme.css` is the only file allowed to hold a colour literal; anything else holding one is the defect.

### TEAMMATESLIBRARY-40 — Degraded, with the persistence reason — and the two things that must NOT badge

**💰 Spends money** · about 20 min

*Proves three consecutive failed Turns move a teammate to Degraded with a reason, while a Human-stopped Turn and a spent Room Budget deliberately do not.*

**Before you start**

- `Team:Acp:Enabled=true`, a working installed adapter, and working provider credentials.
- One Persona configured Model = **Haiku** and Effort = **low** (the standing convention — do not use a larger model for this).
- `Team:AgentMessageBudget` set low (say 2) before you start, so the Budget half is reachable cheaply.
- The ability to interrupt the provider mid-Turn (disable the network adapter, or invalidate credentials briefly).

**Steps**

1. Start the app with ACP on and confirm the teammate under test reads `Online` on /teammates.
2. Open its Room from the card's **Message** link (or via **New chat** in the sidebar) and send `Say hello in five words.`
3. While that Turn is running, cut the network (disable the adapter) so the Turn fails. Restore the network.
4. Repeat the previous two steps twice more, so THREE Turns in a row have failed.
5. Go to /teammates and read the teammate's tile: its dot colour, its label and its tooltip.
6. Open its card and read the line under the status and the action row.
7. Now prove a stopped Turn does not badge: restore normal operation, restart the teammate, send `Write a very long story.` and click **Stop** while it is replying. Read the tile.
8. Now prove a spent Budget does not badge: with `Team:AgentMessageBudget` low, let the teammate post until the Room refuses further agent messages. Read the tile again.
9. Read the reason strings you collected and check them against the banned vocabulary list.

**Pass if — all of these**

- After the third consecutive failed Turn, and not before, the tile moves to `Degraded` with an amber dot.
- The tile's tooltip carries a reason, and the card shows that same reason as a full line, plus a **Restart** button.
- A single failed Turn does NOT produce `Degraded` — one failure is noise, three in a row is a pattern.
- After a Human clicks **Stop** on a live Turn, the tile does NOT read `Degraded` and no reason appears — a stopped Turn is not a failure and does not break the failure streak.
- After a Room hits its Budget and refuses further agent messages, the tile is unchanged — a spent Budget is not a fault.
- No reason string contains `bot`, `backend`, `LLM`, `rate limit` or `quota`.

**Fail if — any of these**

- One failed Turn escalates straight to `Degraded` -> ordinary noise is being reported as a fault and users will learn to ignore the badge.
- A Human-stopped Turn marks the teammate `Degraded` -> the app is badging a teammate that did exactly what it was told.
- A spent Room Budget marks the teammate `Degraded` -> the app is badging its own working safety cap as a fault.
- `Degraded` never appears after three consecutive genuine failures -> the health signal is not reaching the badge, and a broken teammate looks fine.

**Inconclusive if**

You cannot interrupt the provider reliably mid-Turn, so the failures do not land as failures -> record the test as not-executed rather than passing it. Likewise if the teammate never comes Online at all, the whole test is blocked: fix that first (TEAMMATESLIBRARY-37 diagnoses it).

> [!NOTE]
> COSTS MONEY. Budget: a handful of short Haiku/low Turns, each a few hundred tokens — well under a cent in total. Do NOT attempt the token-Budget route to Degraded; that needs roughly a million tokens and is not worth buying. Quota, network and credential failures are indistinguishable to the app by design and are all reported by persistence — do not file that as a wording bug.

---

Back to [the manual test script](../manual-tests.md).
