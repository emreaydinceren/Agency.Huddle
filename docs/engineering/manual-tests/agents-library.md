# Agent files: loading, rejected files, watcher, status badge and the card

Prove that the app is an honest, live mirror of the Persona files on disk (the Agents page that once showed them was removed on 2026-11-18, so the cases below read them through a Team's Agents tab, Ctrl K, the From other teams picker and the Agent card): that an agent's identity and Team come from its frontmatter (its file sits at `Agents\<Name>\<Name>.md`, and its Team never comes from the folder), that every file which fails to load is named out loud instead of silently vanishing, that opening a card probes nothing, that the status badge tells the truth (health outranking a live pipe), and that the file watcher keeps up with edits, creations, deletions, moves and folder renames. Almost every failure in this area is silent — a moved file that quietly changes identity, an Agent folder that stops reloading, a colliding file that disappears without a word — so these tests are written for a human driving a browser and a file explorer side by side.

**42 tests** · 41 free, 1 paid 💰 · about 3.4 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB` / `O-WIRE`, the five resets,
`P-NEW-PERSONA`, `P-ECHO-BOT` and the standing conventions. This area adds:

1. Lane is `P-LAUNCH-FREE`. A handful of tests need ACP on: stop the app and launch them under [`P-LAUNCH-UAT`](common.md#p-launch-uat) (the mock adapter, free) instead, where `<data>` is `C:\seeds\huddle\uat\data`; relaunch under `P-LAUNCH-FREE` for the next test. Do not switch ACP on with `--Team:Acp:Enabled=true` against the real adapter: the Chief of Staff takes a paid Greeting Turn in its empty direct Room. Never edit `appsettings.json` — leave the shipped defaults alone.
2. Start from a library that holds only the built-in Chief of Staff. Persona files live at `<data>\Agents\<Name>\<Name>.md` ([`P-NEW-PERSONA`](common.md#p-new-persona) explains why a hand-written file skips the Model and Effort rows). Run [`P-RESET-TEAMS`](common.md#the-five-resets) before launching: it deletes every folder under `<data>\Agents`, and the app re-seeds the Chief of Staff (alias `cos`, no Teams, no stored Model) at every start, whatever `Acp:Enabled` is. `<data>\Teams\` holds Team folders, notes and Tasks, never Personas; do not put Persona files there.
3. Open a browser at `http://localhost:5100` and a File Explorer window (or a text editor) on `<data>\Agents\`, side by side. Many tests change a file on disk and then watch the browser WITHOUT touching it — do not press F5 unless a step says to.
3a. Where to look. There is no Agents page. The tests read Agents from: the **Agents** tab of the Team `Business` or `Household` (its roster cards and avatar badges, and the `Files that didn't load` block at the top of every Team's Agents tab); Ctrl K (type a Name); the **From other teams** picker (**Add an Agent** → **From other teams**, which lists the Agents NOT already in that Team, Agents in no Team under **No team**); and the Agent card ([`P-OPEN-AGENT`](common.md#p-open-agent)). If `Business` or `Household` is not in the sidebar's Teams list, create the empty folder `<data>\Teams\Business\` (or `Household`) and reload, as in [`P-NEW-PERSONA`](common.md#p-new-persona) step 1. There is no Team filter and no list of every Agent. Below, a "tile" means the Agent's roster card on the Agents tab of a Team it belongs to, and "the tile reads Online" means its avatar badge does; "open the card" means `P-OPEN-AGENT`.
4. Learn the CORE FIXTURE SET but do NOT create it yet — AGENTSLIBRARY-03 creates it. Five Agents, each a folder named after the Agent holding a file of the same name, under `<data>\Agents\`: (1) `Nova\Nova.md` = frontmatter name 'Nova', title 'Research Lead', alias 'nov', teams ['Business']; (2) `Vale\Vale.md` = name 'Vale', title 'Ops Lead', alias 'val', teams ['Business', 'Household']; (3) `Rune\Rune.md` = name 'Rune', title 'Home Steward', alias 'run', teams ['Household']; (4) `Quill\Quill.md` = name 'Quill', title 'Scribe', alias 'qui', and NO teams line at all; (5) `Emily Lee\Emily Lee.md` = name 'Emily Lee', title 'Analyst', alias 'emi', teams ['Business']. With the built-in Chief of Staff that is six Agents. Exact text is given in AGENTSLIBRARY-03.
5. Never put a `.md` directly in `<data>\Agents\` and never put a second `.md` in an Agent's folder: the first is rejected with `A definition must be inside its agent's folder.`, the second with `An agent folder holds only its definition; use work/.`. Any such stray file would land in the rejected block and drown every other observation.
6. All sorting on this page is ORDINAL, not case-insensitive: an upper-case letter sorts before a lower-case one, so a Team named `Zulu` legitimately appears above one named `admin`, and a Persona named `Zoe` above one named `ada`. This is deliberate and pinned by tests. Do not file it.

## Tests

### AGENTSLIBRARY-01 — The page loads, is styled, and spawns nothing

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-02 — A library of only the Chief of Staff reads as one tile, then as empty

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-03 — The fixture library loads, and a tile shows monogram, Name, Title, an @alias caption and status

**Free** · about 8 min

*Creates the core fixture set and proves a tile renders every identity field from frontmatter, with the documented monogram rule (several words give first and last initials, one word gives its first two letters).*

**Before you start**

- `<data>\Agents\` holds only the Chief of Staff's folder (Setup step 2: `P-RESET-TEAMS`, then launch).

**Steps**

1. Create the folder `<data>\Agents\Nova\` and in it `Nova.md` containing exactly:
---
name: 'Nova'
title: 'Research Lead'
alias: 'nov'
teams: ['Business']
---
You are Nova. Answer in one short sentence.
2. Create the folder `<data>\Agents\Vale\` and in it `Vale.md` containing exactly:
---
name: 'Vale'
title: 'Ops Lead'
alias: 'val'
teams: ['Business', 'Household']
---
You are Vale. Answer in one short sentence.
3. Create the folder `<data>\Agents\Rune\` and in it `Rune.md` containing exactly:
---
name: 'Rune'
title: 'Home Steward'
alias: 'run'
teams: ['Household']
---
You are Rune. Answer in one short sentence.
4. Create the folder `<data>\Agents\Quill\` and in it `Quill.md` containing exactly (note: no teams line):
---
name: 'Quill'
title: 'Scribe'
alias: 'qui'
---
You are Quill. Answer in one short sentence.
5. Create the folder `<data>\Agents\Emily Lee\` and in it `Emily Lee.md` containing exactly:
---
name: 'Emily Lee'
title: 'Analyst'
alias: 'emi'
teams: ['Business']
---
You are Emily Lee. Answer in one short sentence.
6. Do not create a Chief of Staff file: the app already seeded one (alias `cos`, title `Chief of Staff`, no Teams). A second file named `Chief of Staff` or aliased `cos` would be rejected as a duplicate of it.
7. Press F5. In Ctrl K, type each of the six Names in turn and confirm each Agent is found.
8. Open Nova's card with `P-OPEN-AGENT` and read its Name, its Title, its `Alias:` line and its status line.
9. Do the same for the other five cards and read the round avatar on each.
10. Hover the badge on the bottom-right rim of each avatar and read its tooltip, and inspect it in the browser's developer tools (F12). There is no `Online` chip or dot beside the avatar.

**Pass if — all of these**

- Ctrl K finds all six Agents: Chief of Staff, Emily Lee, Nova, Quill, Rune, Vale.
- Nova's card shows `Nova` as its Name and `Research Lead` as its Title, with `Alias: nov` (with the word `Alias`, no chip). The avatar's badge carries the status and its tooltip reads `Offline`.
- Every card has the same shape: Name, Title, `Alias:` line and an avatar badge, with values matching that Persona's frontmatter (Vale: `Ops Lead`, `val`; Rune: `Home Steward`, `run`; Quill: `Scribe`, `qui`; Chief of Staff: `Chief of Staff`, `cos`).
- Monograms read: Emily Lee -> `EL`; Chief of Staff -> `CS`; Nova -> `NO`; Quill -> `QU`; Rune -> `RU`; Vale -> `VA`.
- With ACP off, every avatar badge reads `Offline` (its hollow ring is neutral grey; the badge has the CSS class `agent-dot` with `offline` and `role="img"`, so do not compare an RGB value). The state words the badge can show are `Starting`, `Online`, `Degraded` and `Offline`, each with its own glyph (pulse, check, dash, ring).
- Each card has a round monogram avatar.

**Fail if — any of these**

- A Title or an alias renders blank although the file has it -> a dropped `@` on a card binding; the card is rendering a string literal instead of the field.
- The literal text `this.cardName`, `this.cardTitle` or similar appears anywhere on screen -> the documented leading-`@` binding bug has shipped again.
- `Chief of Staff` shows the monogram `CO` -> the monogram is taking the first TWO words instead of first-and-last.
- Ctrl K does not find `Chief of Staff` -> the built-in seed did not run, or a fixture took its Name or alias and both were rejected.
- A status dot has no `agent-dot offline` class while ACP is off -> the badge is not reading the app's real state.

**Inconclusive if**

An Agent you expected is missing from Ctrl K AND a `Files that didn't load` block (top of a Team's Agents tab) names its path -> you mistyped that file's frontmatter; fix the file (watch for smart quotes inserted by your editor — the parser wants plain `'`) and rerun. If nothing is found at all after F5, restart the app and rerun before recording anything.

> [!NOTE]
> Keep all five fixture folders in place — later tests depend on them and say so. Use a plain-text editor; a word processor that converts `'` into a curly quote will make files fail to parse.

### AGENTSLIBRARY-04 — One heading per Team, ordinal order, "No team" last, no empty groups

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-05 — A Persona in two Teams renders in full under both headings

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-06 — A Persona's Team comes from its field, never from its folder

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-07 — The Team filter's label, options, order and de-duplication

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-08 — Choosing a Team narrows to one heading and drops the no-team Personas

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-09 — A Team whose membership drops to zero says so

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-10 — Team names match case-insensitively and appear once

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-11 — All three YAML shapes of `teams` group identically, and the documented comma cost

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-12 — A rejected file is named by path and reason, above the tiles, live

**Free** · about 5 min

*Proves the single failure this feature exists to prevent — a file silently vanishing — cannot happen: a broken file is listed loudly, without a refresh, and restoring it brings the tile back.*

**Before you start**

- Core fixture set in place; no Shape/throwaway files left over.

**Steps**

1. Open the Agents tab of the Team `Business` and press F5. Confirm Nova's roster card is present and there is no `Files that didn't load` section at the top.
2. Without touching the browser, open `<data>\Agents\Nova\Nova.md` and delete the whole line `title: 'Research Lead'`. Save.
3. Wait about two seconds and watch the browser WITHOUT refreshing.
4. Read the new section that appears, including its heading, the path line and the line under it.
5. Scroll and note whether that section is above or below the roster cards.
6. Check the `dotnet run` console for any exception or stack trace.
7. Put the `title: 'Research Lead'` line back and save.
8. Wait about two seconds and watch the browser again, without refreshing.

**Pass if — all of these**

- Within about a second of the save, and with no refresh, the Nova tile disappears and a new section appears.
- That section is headed exactly `Files that didn't load` and sits at the TOP of the Agents tab, ABOVE the roster cards.
- It lists the absolute path of `Nova.md` (ending `...\Agents\Nova\Nova.md`) on one line, and under it the reason `Persona frontmatter is missing required field 'Title'.`
- The section is visually distinct (its own bordered block), not a plain sentence lost in the page.
- No exception or stack trace appears in the `dotnet run` console.
- After restoring the line, the section disappears on its own and Nova's roster card returns; its card (`P-OPEN-AGENT`) shows the Title `Research Lead` and `Alias: nov`.

**Fail if — any of these**

- The Nova tile vanishes and NOTHING appears to say why -> the worst failure in this area: a user's file is on disk, invisible in the app, with no explanation anywhere.
- The block renders below the roster cards -> it can be scrolled past, and a broken file goes unnoticed in a long library.
- The reason does not name the field (`Title`) -> the message is undiagnosable; the user cannot tell which line to fix.
- An exception appears in the console, or the yellow `An unhandled error has occurred.` banner shows -> the read path is throwing for a bad file, which it must never do.
- The tile stays on the page with its old Title -> stale cached text is being served for a file that no longer parses.

**Inconclusive if**

Nothing changes until you press F5 -> the rejection wording still passes or fails on its own, but record the refresh requirement and run AGENTSLIBRARY-19, which judges the watcher.

### AGENTSLIBRARY-13 — Every distinct rejection reason, word for word

**Free** · about 12 min

*Proves each malformed-file case produces a reason that names the offending field, and that two faults in one file produce two reasons on one line.*

**Before you start**

- Core fixture set in place and loading cleanly (no rejected block on screen at the start).

**Steps**

1. Create the folder `<data>\Agents\bad1\` and in it `bad1.md` containing exactly (no name field):
---
title: 'Nameless'
alias: 'nl1'
---
You are nameless.
2. Open the Agents tab of the Team `Business` and read its `Files that didn't load` block (top of the tab); record the reason shown under `bad1.md`. Then delete the `bad1` folder.
3. Create the folder `<data>\Agents\bad2\` and in it `bad2.md` containing exactly (no frontmatter at all — plain prose):
You are an agent with no front matter whatsoever.
4. Record the reason shown under `bad2.md`. Then delete the `bad2` folder.
5. Create the folder `<data>\Agents\bad3\` and in it `bad3.md` containing exactly (blank title):
---
name: 'Blanky'
title: ''
alias: 'bla'
---
You are Blanky.
6. Record the reason shown under `bad3.md`. Then delete the `bad3` folder.
7. Create the folder `<data>\Agents\bad4\` and in it `bad4.md` containing exactly (name written twice):
---
name: 'Twice'
name: 'Twice'
title: 'Repeat'
alias: 'twi'
---
You are Twice.
8. Record the reason shown under `bad4.md`. Then delete the `bad4` folder.
9. Create the folder `<data>\Agents\bad5\` and in it `bad5.md` containing exactly (two spaces inside the name):
---
name: 'Emily  Lee'
title: 'Analyst'
alias: 'em2'
---
You are Emily Lee.
10. Record the reason shown under `bad5.md`. Then delete the `bad5` folder.
11. Create the folder `<data>\Agents\bad6\` and in it `bad6.md` containing exactly (alias starting with a hyphen):
---
name: 'Hyphen'
title: 'Tester'
alias: '-nope'
---
You are Hyphen.
12. Record the reason shown under `bad6.md`. Then delete the `bad6` folder.
13. Confirm the page has returned to the clean core fixture set with no rejected block, and check the `dotnet run` console for exceptions across the whole test.

**Pass if — all of these**

- bad1.md -> `Persona frontmatter is missing required field 'Name'.`
- bad2.md -> `Persona frontmatter is missing required field 'Name'.` (a file with no frontmatter is reported the same way, and is NOT loaded as a Persona)
- bad3.md -> `Persona frontmatter is missing required field 'Title'.` (a blank value and a missing line are the same failure)
- bad4.md -> `Persona frontmatter has a duplicate 'Name' field.`
- bad5.md -> `Persona frontmatter field 'Name' has an invalid value: 'Emily  Lee'.`
- bad6.md -> `Persona frontmatter field 'Alias' has an invalid value: '-nope'.`
- Every reason is shown under that file's own absolute path.
- The valid core fixtures keep their roster cards on the Business and Household Agents tabs, and the Chief of Staff is still found by Ctrl K, throughout — one bad file never takes the page down.
- No exception or stack trace appears in the console at any point.

**Fail if — any of these**

- Any reason is generic and does not name the offending field -> the user cannot diagnose their own file; the rejected block stops being useful.
- bad2.md (no frontmatter) loads as a Persona and shows a tile -> the required-field schema is not being enforced; files with no identity silently become agents.
- A reason names the wrong field (e.g. bad3 reported against `Name`) -> the message points the user at the wrong line.
- The app crashes, or the page stops rendering tiles, on any of these files -> a malformed file is being treated as an exception rather than as data.

**Inconclusive if**

A `bad*.md` file produces no rejected entry at all AND no tile -> confirm the file is really at `<data>\Agents\<folder>\<folder>.md` (the file name must equal its folder's name) and ends in `.md`; if it is a `.md` and it is neither loaded nor rejected, that IS a fail (a file must always be accounted for as one or the other).

> [!NOTE]
> A file that trips two INDEX-level rules (e.g. two files sharing both Name and Alias) shows both reasons on one line separated by a space — that case is covered in AGENTSLIBRARY-14. Parser-level faults short-circuit, so a file missing both `name:` and `title:` correctly reports `Name` only.

### AGENTSLIBRARY-14 — A duplicate Alias rejects BOTH files, each naming the other

**Free** · about 5 min

*Proves a collision takes out both sides rather than picking a winner — the rule that keeps editing the loser from silently doing nothing.*

**Before you start**

- Core fixture set in place; `Agents\Nova\Nova.md` has `alias: 'nov'`.

**Steps**

1. Create the folder `<data>\Agents\Twin\` and in it `Twin.md` containing exactly (the same alias as Nova, a different Name, so only the Alias rule fires):
---
name: 'Twin'
title: 'Duplicate Alias'
alias: 'nov'
---
You are Twin.
2. Wait about two seconds and watch the Business Agents tab without refreshing.
3. Note whether Nova's roster card is still present.
4. Read every entry in the `Files that didn't load` block.
5. Change `Twin.md`'s alias line to `alias: 'twi'` and save.
6. Wait about two seconds and watch the page again.
7. Delete the folder `<data>\Agents\Twin\`.

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
> Seeing two agents vanish for one typo is the design, not a bug — it is called out in the known limits.

### AGENTSLIBRARY-15 — A duplicate Name — including one differing only in case — rejects both

**Free** · about 5 min

*Proves Name collisions are caught case-insensitively, closing the documented bug where 'Jarvis' and 'jarvis' loaded as two agents sharing one database row.*

**Before you start**

- Core fixture set in place.

**Steps**

1. Create the folder `<data>\Agents\Jarvis-a\` and in it `Jarvis-a.md` containing exactly:
---
name: 'Jarvis'
title: 'Chief of Staff'
alias: 'jva'
---
You are Jarvis.
2. Create the folder `<data>\Agents\Jarvis-b\` and in it `Jarvis-b.md` containing exactly (same name, different case, different alias):
---
name: 'jarvis'
title: 'Deputy'
alias: 'jvb'
---
You are jarvis.
3. Wait about two seconds and read the Business Agents tab without refreshing.
4. Read both entries in the `Files that didn't load` block.
5. In `T-B` run `sqlite3 <data>\team.db "select * from persona_models;"` ([`O-DB`](common.md#o-db); skip if you have no sqlite3).
6. Delete both folders, `Jarvis-a` and `Jarvis-b`.

**Pass if — all of these**

- NO tile appears for Jarvis or jarvis — not two, and not one.
- The `Files that didn't load` block lists BOTH absolute paths.
- `jarvis-a.md`'s reason reads `Persona Name 'Jarvis' is also used by '<absolute path of jarvis-b.md>'.`
- `jarvis-b.md`'s reason reads `Persona Name 'jarvis' is also used by '<absolute path of jarvis-a.md>'.`
- `persona_models` holds no row for Jarvis or jarvis — a rejected file must never create one.

**Fail if — any of these**

- Two tiles appear, `Jarvis` and `jarvis` -> the case-insensitive Name collision rule has regressed; the two agents will silently share one `persona_models` row, and one of them will appear to change Model by itself.
- Only one is rejected -> a winner is being picked by enumeration order; edits to the loser do nothing.
- A `persona_models` row exists for either name after this test -> a rejected file is writing to the database.

**Inconclusive if**

No sqlite3 available -> run the test without the database step and record that the database half was not checked; the tile and reason observations still stand on their own.

### AGENTSLIBRARY-16 — One file's Alias equal to another file's Name rejects both, with two different sentences — and a self-matching alias is legal

**Free** · about 6 min

*Proves the cross-collision is caught in both directions with direction-specific wording, and that a file whose alias equals its own name is deliberately allowed.*

**Before you start**

- Core fixture set in place, including Nova.md with `name: 'Nova'`.

**Steps**

1. Create the folder `<data>\Agents\Shadow\` and in it `Shadow.md` containing exactly (alias equal to Nova's NAME):
---
name: 'Shadow'
title: 'Shadow Lead'
alias: 'Nova'
---
You are Shadow.
2. Wait about two seconds and read the Business Agents tab without refreshing.
3. Read both entries in the `Files that didn't load` block, word for word.
4. Delete the folder `<data>\Agents\Shadow\` and confirm the Nova tile returns.
5. Create the folder `<data>\Agents\Echoic\` and in it `Echoic.md` containing exactly (alias equals its OWN name):
---
name: 'Echoic'
title: 'Self Named'
alias: 'Echoic'
---
You are Echoic.
6. Wait about two seconds and read the Business Agents tab, then find `Echoic` with Ctrl K and open its card (`P-OPEN-AGENT`).
7. Delete the folder `<data>\Agents\Echoic\`.

**Pass if — all of these**

- With `Shadow.md` present, BOTH the Nova tile and any Shadow tile are gone, and both paths are listed in the rejected block.
- `Shadow.md`'s reason reads `Persona Alias 'Nova' matches the Name used by '<absolute path of Nova.md>'.`
- `Nova.md`'s reason reads `Persona Name 'Nova' matches the Alias used by '<absolute path of Shadow.md>'.`
- The two sentences are DIFFERENT and each points at the other file, so the direction of the clash is readable.
- With `Echoic.md` present, Ctrl K finds `Echoic` and its card shows the Title `Self Named` and `Alias: Echoic`; it is NOT in the rejected block.

**Fail if — any of these**

- `Echoic.md` is rejected -> a file whose alias equals its own name is being refused; that is a plausible authoring choice and is explicitly legal.
- Only one side of the `Shadow.md` clash is rejected -> the other file's owner gets no feedback at all.
- Both sides show identical wording -> which file holds the Name and which holds the Alias is unreadable, so the user cannot tell which one to change.
- Neither file is rejected and both tiles appear -> an Alias can now shadow another agent's Name, and mentions become ambiguous.

**Inconclusive if**

Nova's reason is missing but Shadow.md's is present -> re-read the block carefully (entries are sorted by path, so Nova's may be far from shadow's). Only record a fail once you have checked the whole list.

### AGENTSLIBRARY-17 — The rejected block is ordered, survives the filter, and coexists with the empty-library sentence

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-18 — Non-markdown files are ignored entirely — not loaded, not rejected

**Free** · about 4 min

*Proves stray non-markdown files in the Agents tree cause neither a rejected entry nor a reload burst (the scan reads only `*.md`, PersonaStore.cs:717,741; the watcher ignores other extensions, PersonaStore.cs:1102-1107).*

**Before you start**

- Core fixture set in place and loading cleanly (no rejected block on screen).

**Steps**

1. Open the Agents tab of the Team `Household` and note its exact contents: roster cards present, and no `Files that didn't load` block.
2. Create `<data>\Agents\notes.txt` with the text `scratch notes, not a persona` (directly in the `Agents` root).
3. Create an empty file `<data>\Agents\.gitkeep`.
4. Create `<data>\Agents\Rune\readme.rst` with the text `this is not markdown` (inside Rune's folder, beside `Rune.md`).
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
- The page repaints each time you add one -> stray files are churning the reload path; with ACP on this restarts every online agent and costs money for nothing.
- A tile appears for one of them -> the `*.md` scan filter has been widened.

**Inconclusive if**

You cannot tell whether a repaint happened -> repeat with the browser devtools open on the Elements panel, where a re-render highlights, or repeat this test in the `P-LAUNCH-UAT` lane, where Ada is Online and its badge gives you a visible marker of a page reload.

### AGENTSLIBRARY-19 — The watcher is live: an edit to an Agent's definition reloads it, and a write under work\ does not

**Free** · about 8 min

*Catches the headline silent failure of this area — a definition file inside an Agent folder that is found once at startup and then never reloads, with no error anywhere — and checks the opposite: files an Agent writes into its own `work\` folder must not reload anyone.*

**Before you start**

- Core fixture set in place, including `Agents\Rune\Rune.md`.
- The browser is on the Agents tab of the Team `Household` and you will not press F5 during the test.

**Steps**

1. On the `Household` Agents tab, press F5 once, and confirm Rune's roster card is there with an `Offline` badge; open its card (`P-OPEN-AGENT`) and confirm it reads `Rune`, `Home Steward` and `Alias: run`, then close it.
2. In your editor, open `<data>\Agents\Rune\Rune.md` and change `title: 'Home Steward'` to `title: 'House Captain'`. Save. Do not touch the browser.
3. Wait up to about two seconds, open Rune's card (`P-OPEN-AGENT`), read its Title, then close it.
4. In the same file, change `alias: 'run'` to `alias: 'rn'`. Save, wait, then open the card again and read its `Alias:` line.
5. In the same file, change `teams: ['Household']` to `teams: ['Business']`. Save, wait, and read which Team's Agents tab Rune's roster card is now on.
6. Open Rune's card and compare the text in the card's `Persona` block with your editor's buffer, then close the card.
7. Now repeat the first edit on another Agent: open `<data>\Agents\Nova\Nova.md`, change `title: 'Research Lead'` to `title: 'Research Chief'`, save, wait, open Nova's card and read its Title.
8. Create the folder `<data>\Agents\Rune\work\` and in it a file `notes.md` containing `scratch notes`. Wait about five seconds WITHOUT refreshing and read the Agents tab, including the `Files that didn't load` area.
9. Restore all four edited values (`House Captain` -> `Home Steward`, `rn` -> `run`, `Business` -> `Household`, `Research Chief` -> `Research Lead`) and delete the folder `<data>\Agents\Rune\work\`.

**Pass if — all of these**

- Within about a second of each save, and with NO page refresh and NO app restart, the card shows the new value.
- Rune's Title becomes `House Captain`, then its `Alias:` line reads `rn`.
- After the `teams:` edit, Rune's roster card leaves the `Household` Agents tab and appears on the `Business` one on its own.
- The card's `Persona` block shows the whole raw file text, matching the editor buffer exactly (frontmatter included).
- Nova's card reloads on edit in exactly the same way.
- After `work\notes.md` is created, the Agents tab does not change: no new roster card, no `Files that didn't load` entry (a `.md` under `work\` is neither a second definition nor scanned), and Rune keeps its tile.

**Fail if — any of these**

- The watcher is dead for the Agent folders -> the watcher must see one level below `Agents` (`IncludeSubdirectories`); every Persona is frozen at whatever it said when the app started, forever, with no error, no log and nothing on screen. This is the headline silent failure of the area.
- Neither reloads -> the watcher is dead altogether; every edit needs an app restart.
- `work\notes.md` is listed as rejected, or the page repaints for it -> the scan or the watcher reaches into `work\`; with ACP on, every file an Agent writes there would restart it and clear its memory.
- The tile updates but the card's `Persona` text stays stale -> cached file text is not being refreshed with the index; an agent's system prompt and its displayed prompt now disagree.
- The change appears only after several seconds and several saves -> the debounce has been set far too long; judge against AGENTSLIBRARY-22 before filing.

**Inconclusive if**

Your editor saves via a temp-file-and-rename dance and you see two repaints -> that is expected editor behaviour, not a defect; judge only whether the final state is correct. If nothing at all repaints AND the app console shows an exception, restart the app and rerun before recording.

> [!NOTE]
> Every definition now sits one folder below the watched root, so the Rune edit is the test; there is no root-level file to compare against. Only events one or two segments below `Agents` count (`PersonaStore.cs:1102-1107`), which is what keeps a write under an Agent's `work\` quiet.

### AGENTSLIBRARY-20 — The watcher notices creations and deletions while the page is open

**Free** · about 5 min

*Proves an Agent folder added or removed from outside the app appears or disappears — on its Team's Agents tab and in Ctrl K — with no refresh.*

**Before you start**

- Core fixture set in place; browser on the Agents tab of the Team `Business`, freshly refreshed once.

**Steps**

1. Note the roster cards on the tab, and that Ctrl K finds no `Pike`.
2. From outside the app, create the folder `<data>\Agents\Pike\` and in it a new file `Pike.md` containing exactly:
---
name: 'Pike'
title: 'Field Lead'
alias: 'pik'
teams: ['Business']
---
You are Pike. Answer in one short sentence.
3. Wait about two seconds and watch the Agents tab WITHOUT refreshing.
4. Press Ctrl K, type `Pike` and read the results.
5. Delete the folder `<data>\Agents\Pike\`.
6. Wait about two seconds and watch the Agents tab again, and search Ctrl K for `Pike` again.

**Pass if — all of these**

- A `Pike` roster card appears on its own on the Agents tab, within about a second.
- Ctrl K finds `Pike` without a refresh.
- After the delete, the Pike roster card disappears on its own and Ctrl K no longer finds `Pike`.

**Fail if — any of these**

- The roster card only appears after F5 -> a user copying in a Persona file sees nothing happen and will assume the app did not accept it.
- A deleted Persona's roster card lingers -> the card would then offer Edit and Open on a file that no longer exists.
- The roster card updates but Ctrl K does not (or the reverse) -> two surfaces are reading from two different moments in time.

**Inconclusive if**

Nothing happens at all for either step -> run AGENTSLIBRARY-19 first; if edits also do not reload, the watcher is dead and this test adds nothing new. Record it as blocked by -19.

### AGENTSLIBRARY-21 — Renaming or deleting an Agent folder from outside is noticed, and the card follows the file

**Free** · about 8 min

*Catches two documented watcher traps: a directory event's name is the directory, which never matches a *.md filter, so a folder rename can leave a Persona's path stale forever and a folder delete can leave its tile on screen forever. An Agent's definition must be named after its folder, so a folder rename alone rejects the file until the file is renamed too.*

**Before you start**

- Core fixture set in place.
- `Rune` and a second Household member (`Bram`, created below) are the two Agents the steps move around. Vale also declares `Household`, so that heading survives.

**Steps**

1. Create the folder `<data>\Agents\Bram\` and in it `Bram.md` containing exactly:
---
name: 'Bram'
title: 'Home Second'
alias: 'bra'
teams: ['Household']
---
You are Bram. Answer in one short sentence.
2. Open the Agents tab of the Team `Household`, press F5, and confirm both `Rune` and `Bram` have roster cards. Open each card (`P-OPEN-AGENT`) and write down the absolute path shown in its `Persona file` section. Close the card.
3. PART A — RENAME THE FOLDER ONLY. In File Explorer, rename the folder `<data>\Agents\Rune` to `<data>\Agents\Rune2`. Do not touch the browser.
4. Wait about two seconds and read the Agents tab: Rune's roster card and the `Files that didn't load` block.
5. PART B — RENAME THE FILE TO MATCH. Rename `<data>\Agents\Rune2\Rune.md` to `Rune2.md`. Wait about two seconds and read the Agents tab again.
6. Open Rune's card and read the `Persona file` path.
7. With that card open, click **Open** and note whether the correct file opens in your Markdown editor. Close the editor window.
8. Click **Edit**, add a space to the end of the body line, click **Save**, and note whether it succeeds or shows an error line on the card. Close the card.
9. PART C — DELETE. Move the two folders `<data>\Agents\Rune2` and `<data>\Agents\Bram` OUT of `<data>\Agents` (to your Desktop, say). Do not touch the browser.
10. Wait about two seconds and read the `Household` Agents tab (it must have lost both roster cards).
11. Move `Bram` back in only to finish: delete it. Move `Rune2` back in, rename `Rune2\Rune2.md` to `Rune.md` FIRST, then rename the folder to `Rune`, and confirm the core fixture set is restored.

**Pass if — all of these**

- PART A: within about a second the Rune tile is gone and Bram is untouched. The `Files that didn't load` block lists `...\Agents\Rune2\Rune.md` with the reason `An agent folder holds only its definition; use work/.` (the file is no longer named after its folder, so it is not Rune2's definition).
- PART B: after the file is renamed, Rune's roster card is back on the `Household` Agents tab, its card still reading `Home Steward` with `Alias: run`, and the rejected entry is gone.
- PART B: the card's `Persona file` path shows the NEW location (`...\Agents\Rune2\Rune2.md`), not the old one.
- PART B: **Open** launches the correct, existing file.
- PART B: **Edit** -> **Save** succeeds with no error line on the card, and the change is on disk at the new path.
- PART C: within about a second of moving the folders away, BOTH roster cards disappear from the `Household` Agents tab on their own.
  Vale's roster card REMAINS there: the core fixture set gives `Vale.md`
  `teams: ['Business', 'Household']`, so Household still has a member.
- PART C: no exception appears in the `dotnet run` console.

**Fail if — any of these**

- After the folder rename the Rune tile stays and the rejected block stays empty -> the directory event was dropped; the page serves a file that is no longer where it says. Silent until a user tries to save.
- After the file rename the card still prints the OLD path -> the card is not reading the rediscovered location; **Open** will fail and **Edit** -> **Save** will write to a path that no longer exists.
- **Edit** -> **Save** shows an error, or appears to succeed but the file on disk is unchanged -> the same stale-path trap, now losing a user's edit.
- After the folders are moved away, the tiles linger indefinitely -> a deleted directory raises an event named after the directory, which has no extension; missing it means the app serves stale prompt text for every Persona that was inside, forever.
- The app throws when a folder disappears -> a removed directory is an ordinary event, not an error.

**Inconclusive if**

Windows refuses the folder rename because a file inside is open in your editor -> close the editor and retry; a rename that never happened proves nothing. If **Open** does nothing at all on your machine, check AGENTSLIBRARY-33 first — you may have no application registered for .md.

> [!NOTE]
> The folder-name-differs warning (`Its folder is named 'Rune2', not 'Rune'.`, PersonaStore.cs:801) is computed but this page reads only the rejected files, so it shows nothing for Part B. That is not a failure here.

### AGENTSLIBRARY-22 — A burst of file writes becomes one update, not five

**Free** · about 5 min

*Proves the watcher debounces, so a multi-file copy or a branch switch does not repaint five times (and, with ACP on, does not restart five sessions).*

**Before you start**

- Core fixture set in place.
- `Team:Acp:Enabled` false (the default) — run it free first, before ever trying it with ACP on.

**Steps**

1. Prepare five Agent folders in a scratch folder OUTSIDE `<data>`, named `Burst1` … `Burst5`, each holding a file of the same name (`Burst1\Burst1.md` … `Burst5\Burst5.md`), each like this with the digit changed:
---
name: 'Burst1'
title: 'Load Tester'
alias: 'bu1'
teams: ['Business']
---
You are Burst1. Answer in one short sentence.
2. Open the Agents tab of the Team `Business` and press F5.
3. Select all five folders in the scratch folder and copy them into `<data>\Agents\` in ONE paste operation. Watch the Agents tab closely while it happens.
4. Count how many distinct repaints you see (a repaint is the roster visibly changing).
5. Check the `dotnet run` console and count the lines it logged for this burst.
6. Confirm that the Burst1 to Burst5 roster cards are all on the tab and that no `Files that didn't load` block appeared: this test is about a reload, so five rejected files would be a failed setup, not a pass.
7. Delete the five Burst folders from `<data>\Agents`.

**Pass if — all of these**

- All five roster cards appear together on the `Business` Agents tab, roughly half a second after the copy settles.
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

### AGENTSLIBRARY-23 — A dropped-event storm still converges on what is on disk

**Free** · about 8 min

*Checks the backstop for the OS silently dropping watcher events — a permanent, unannounced desync between the page and the disk.*

**Before you start**

- Core fixture set in place.
- Ability to write many files at once.

**Steps**

1. Open the Agents tab of the Team `Business` and press F5. (The Storm Agents are in the Team `Storm`, not `Business`, so the From other teams picker is where you count them.)
2. In the second PowerShell window, run this to write 50 valid Personas at once, each in its own folder (replace `<data>` with the real path):
1..50 | ForEach-Object { $dir = "<data>\Agents\Storm$_"; New-Item -ItemType Directory -Force $dir | Out-Null; "---`nname: 'Storm$_'`ntitle: 'Storm Tester'`nalias: 'st$_'`nteams: ['Storm']`n---`nYou are Storm$_." | Set-Content -Encoding utf8 "$dir\Storm$_.md" }
3. Wait about five seconds, then WITHOUT refreshing open **Add an Agent** → **From other teams** and count the Agents listed under `Storm`, and check that no `Files that didn't load` block appeared at the top of the tab.
4. Check the `dotnet run` console for a warning mentioning a FileSystemWatcher error.
5. Now delete them all at once: `Remove-Item -Recurse <data>\Agents\Storm*`
6. Wait about five seconds and, without refreshing, reopen **From other teams** and confirm `Storm` is gone.
7. Run `Get-ChildItem <data>\Agents -Directory | ForEach-Object { Get-ChildItem $_.FullName -Filter *.md }` and compare it against the Agents in the pickers and Agents tabs plus rejected entries on screen.

**Pass if — all of these**

- Step 3 shows 50 Agents under `Storm` and no rejected entries: the app converged by LOADING the Personas, which is the thing this test observes. A page that shows 50 rejections would also agree with the disk, and proves nothing about the reload.
- After the storm settles, the app agrees exactly with the disk: every definition file on disk is either an Agent you can find or a rejected entry, and nothing on screen has no file.
- If the OS dropped events, the console carries the warning `PersonaStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.` AND the page still converges.

**Fail if — any of these**

- Fewer than 50 Agents stay under `Storm`, or any `Storm` file sits in the rejected block -> events were lost or a folder was scanned before its file landed, and nothing rescanned.
- The page permanently disagrees with the disk — a missing tile or a lingering one that never resolves, even after a minute -> a silent, permanent desync; it clears only when something unrelated touches a file, so a user can be looking at a library that does not exist.
- The overflow warning appears and the page does NOT converge -> the error backstop is not scheduling a refresh.

**Inconclusive if**

No overflow warning ever appears (likely — the watcher buffer is raised to 64 KB specifically to make this rare) and the page converges correctly -> record as PASS-with-note: 'could not provoke a dropped-event storm; convergence verified for a 50-file burst'. Do NOT record a silent pass as if the overflow path had been exercised, and do not fail it for being unprovokable.

> [!NOTE]
> This is the hardest behaviour in the area to provoke deliberately. Its value is the convergence check, which is worth running even when no overflow occurs.

### AGENTSLIBRARY-24 — A tile click opens the card, and the card shows the real file path

**Free** · about 7 min

*Proves the card opens from both ways in (Ctrl K and **Edit** on a roster card), shows every View-mode field (including Work mode) and the discovered path, and closes the way a real dialog should. The card is now a `MudDialog` opened through `IDialogService` (Stage 4 of the MudBlazor migration), not a hand-rolled overlay `<div>` — this test is a good place to also confirm the dialog behaviour that migration bought: a focus trap on open, Escape-to-close, and focus returning to the control that opened it.*

**Before you start**

- Core fixture set in place, including `Agents\Rune\Rune.md`.

**Steps**

1. Open the Agents tab of the Team `Business` and press F5.
2. Click **Edit** on Nova's roster card. Immediately read `document.activeElement` in the DevTools Console. Note whether the card opens and whether focus moved into it.
3. Press Tab a few times and confirm focus never lands on something BEHIND the dialog (the sidebar, another roster card).
4. Close it by clicking the × at the top right. Read `document.activeElement` again — is it back on Nova's **Edit** button?
5. Open Nova's card again with Ctrl K (type `Nova`, select the Agent). Note whether the card opens.
6. Read the card from top to bottom and write down: the header text, the Name, the Title, the status line, the `Alias:` line, whether a `Teams:` line is present and what it says, the section headings, and the text under `Persona file`.
7. Click INSIDE the card body (on the Persona text). Note whether it closes.
8. Click the dimmed backdrop outside the card. Note whether it closes, and read `document.activeElement`.
9. Reopen the Nova card from its **Edit** button and press the Escape key. Note whether it closes, and where focus lands afterwards.
10. Open Quill's card (`P-OPEN-AGENT`; Quill has no Teams) and check whether a `Teams:` line appears.
11. Close the card, then open Rune's card and read its `Persona file` path.
12. In the second PowerShell window, run `Test-Path '<paste the exact path the card printed>'`.

**Pass if — all of these**

- Both ways in (**Edit** on the roster card, Ctrl K) open the card, and focus moves INTO the dialog on open (never left sitting on the opener).
- Tab never lets focus escape to the page behind while the dialog is open.
- The card header reads exactly `Agent` in View mode.
- The card shows: Name `Nova`; Title `Research Lead`; a status line with a dot and `Offline`; `Alias: nov`; `Teams: Business`.
- Section headings, in order, read `Persona`, `Model`, `Effort`, `Work mode`, `Persona file` (AgentProfileCard.razor:239-262).
- Under `Persona` is the whole raw file text including the `---` frontmatter; `Model` reads `Agent default`; `Effort` reads `Model default`; `Work mode` reads `Agent default` (AgentProfileCard.razor:1309).
- Clicking inside the card does NOT close it; clicking the dimmed backdrop DOES; Escape DOES.
- After EVERY way of closing the dialog (× icon, backdrop, Escape), focus returns to the **Edit** button that opened it.
- Quill's card has NO `Teams:` line at all (not an empty one).
- Rune's card prints a path ending `...\Agents\Rune\Rune.md`, and `Test-Path` on it returns `True`.

**Fail if — any of these**

- Focus is not moved into the dialog on open, or Tab lets it leak onto the page behind -> the focus trap is not working, which for a keyboard or screen-reader user means the dialog might as well not be modal.
- Escape does not close the dialog, or closing it by any route does not return focus to the opener -> a MudBlazor default (`CloseOnEscapeKey`, focus restore) has been turned off or broken, and a keyboard user is dropped at the top of the page.
- Rune's card prints a path that is not the Agent's own folder file (a stale `...\Teams\...` path, or the file at the `Agents` root) -> the path is being reconstructed instead of reporting where the file was actually discovered; **Open** and **Edit** will then work on the wrong file.
- `Test-Path` returns `False` -> the card is printing a path that does not exist.
- Quill shows an empty `Teams:` line -> an agent with no Teams reads as having a blank Team rather than none.
- After switching from one Agent's card to another, the previous agent's Persona text is still shown -> card state is not being cleared between opens.
- A `node` process appears when you open a card -> View mode is probing the adapter; opening a card to read it must never spawn anything.

**Inconclusive if**

The card does not open at all and other clicks on the page are also dead -> the Blazor circuit has dropped; reload and rerun. If a screen-magnifier or accessibility tool is intercepting Tab, verify the focus trap using `document.activeElement` in the Console instead of eyeballing the ring.

> [!NOTE]
> The focus trap, Escape-to-close and focus-return were all verified working in a browser for this migration — see the "Make the Agent card a real dialog" commit. Any regression here is a real defect, not a documented limit.

### AGENTSLIBRARY-25 — Offline is the honest default, with no invented reason

**Free** · about 4 min

*Proves that with ACP off every agent reads Offline with no invented reason, and that Restart (with its warning) is offered on an Offline card, as it is in every state except Starting.*

**Before you start**

- App started WITHOUT `--Team:Acp:Enabled=true` (the ship default).
- Core fixture set in place.

**Steps**

1. Open the Agents tab of the Team `Business` and press F5.
2. Read the avatar badge (glyph and tooltip) on every roster card, then on the `Household` tab too.
3. Hover the mouse over the badge on a roster card's avatar for three seconds and note what the tooltip says.
4. Open Nova's card (`P-OPEN-AGENT`) and read the lines under the Name.
5. Note which buttons are in the card's action row, and read any hint sentence below it. Then close the card, open the Chief of Staff's card and read its action row.
6. In `T-B` run `O-ADAPTERS` and, if you have sqlite3, `sqlite3 <data>\team.db "select id,name,kind from users;"` ([`O-DB`](common.md#o-db)).

**Pass if — all of these**

- Every tile reads `Offline` with a hollow grey ring (`agent-dot offline`).
- The badge tooltip reads just `Offline`, with no colon and no reason after it: no reason is known, so none is invented.
- The card shows the same `Offline` status line and NO red reason paragraph under it.
- Nova's action row contains `Edit`, `Open`, `Restart` and a red Remove icon button (tooltip `Remove`); a `Message` link appears only if that agent already has a Room. The Chief of Staff's row has `Reset to default` where the others have Remove (AgentProfileCard.razor:198-224).
- A hint below the action row reads `Like saving an edit, this restarts the agent, which clears what it remembers.`
- `O-ADAPTERS` prints `0`.
- In `users`, there is no `agent` row for Nova, Vale, Rune, Quill or Emily Lee (nothing has connected). A row for the Chief of Staff, if any, is not judged here.

**Fail if — any of these**

- A tile reads `Online` when nothing is running -> the badge is not derived from real presence; a user will message an agent that cannot answer.
- A tooltip with a reason appears on the label although nothing has ever started -> a reason is being fabricated.
- No `Restart` button appears on an Offline agent -> the user can see the failure but cannot act on it.
- The card has a red reason paragraph under `Offline` -> a reason is being fabricated.

**Inconclusive if**

Some tiles read Online -> check whether you left ACP enabled from a later test, or whether you launched with `P-LAUNCH-UAT` (the mock adapter brings Agents Online there); relaunch under `P-LAUNCH-FREE` and rerun.

> [!NOTE]
> Restart is offered in every state except Starting, Online included (F4: AgentProfileCard.razor:182). A Restart button on a healthy card is correct, not a defect.

> [!NOTE]
> Every tile reading Offline on a stock install is CORRECT and is listed as a known limit. Do not file it.

### AGENTSLIBRARY-27 — Badges repaint live on connect and disconnect, with no refresh

**Free** · about 6 min

*Proves the roster card's badge and the open card both follow presence changes by themselves, and that leaving the page does not throw — the shape of a leaked event subscription.*

**Before you start**

- App running with ACP OFF.
- Core fixture set in place.
- PowerShell available to run `tools/echo-bot.ps1`.

**Steps**

1. Create the folder `<data>\Agents\Scout\` and in it `Scout.md` containing exactly:
---
name: 'Scout'
title: 'Pipe Tester'
alias: 'sct'
teams: ['Business']
---
You are Scout. Answer in one short sentence.
2. In a third PowerShell window at the repo root, run `pwsh tools/echo-bot.ps1 -Name Scout` and confirm it prints that it connected.
3. Open the Agents tab of the Team `Business` and press F5 ONCE (this is the only refresh in this test; it lets the page learn the new agent's id).
4. Confirm the `Scout` roster card reads `Online` (check glyph on the avatar badge) (an external bot connected under the Name of an Agent that has a Persona file).
5. Open Scout's card with `P-OPEN-AGENT` so it is open, and leave it open.
6. In the third PowerShell window, press Ctrl+C to stop the echo bot.
7. Watch the page for up to five seconds WITHOUT refreshing: read the tile's avatar badge, and the open card's status line.
8. Run `pwsh tools/echo-bot.ps1 -Name Scout` again and watch the same two places, again without refreshing.
9. Close the card, click another page in the sidebar (e.g. **Settings**), then come back to the `Business` Agents tab. Check the `dotnet run` console for exceptions.
10. Stop the echo bot and delete the folder `<data>\Agents\Scout\`.

**Pass if — all of these**

- After the bot is stopped, both the tile AND the open card change from `Online` to `Offline` on their own, within a few seconds, with no refresh.
- The tile and the card never disagree — they change at the same moment and to the same value.
- After the bot reconnects, both go back to `Online` by themselves.
- Navigating away from the Agents tab and back produces no exception in the console and no yellow `An unhandled error has occurred.` banner.

**Fail if — any of these**

- You must press F5 to see the change -> the page is not subscribed to presence; a user watching an agent go down sees the check glyph indefinitely.
- The tile updates while the open card stays stale -> two surfaces disagree about one agent.
- An exception appears in the console when you navigate away -> a component failed to unsubscribe from a singleton event; that leak throws on every circuit disconnect and never dies.

**Inconclusive if**

The echo bot cannot connect (`Connecting to pipe...` then an error) -> the pipe name may differ from the default `team`; check `Team:PipeName` in appsettings.json and pass `-Pipe <name>`. Without a connecting client this test cannot be judged.

> [!NOTE]
> The very FIRST connection of a brand-new agent name may not flip the tile without one refresh, because the page learns the agent's id when it loads its data. That is why step 3 refreshes once; judge only the disconnect and reconnect that follow.

### AGENTSLIBRARY-41 — A definition file at the Agents root is rejected, naming its path

**Free** · about 4 min

*Proves the layout rule from the outside: a `.md` placed directly in `<data>\Agents\` is not a Persona, and the page says so instead of ignoring it.*

**Before you start**

- Core fixture set in place and loading cleanly (no rejected block on screen).

**Steps**

1. Create `<data>\Agents\Stray.md` (directly in the `Agents` folder, no sub-folder) containing exactly:
---
name: 'Stray'
title: 'Misplaced'
alias: 'str'
---
You are Stray.
2. Open the Agents tab of the Team `Business`, wait about two seconds and read it without refreshing; search Ctrl K for `Stray`.
3. Create the folder `<data>\Agents\Stray\` and move `Stray.md` into it.
4. Wait about two seconds and read the tab again; search Ctrl K for `Stray` again.
5. Delete the folder `<data>\Agents\Stray\`.

**Pass if — all of these**

- After step 2 Ctrl K finds no `Stray`, and the `Files that didn't load` block lists the absolute path `<data>\Agents\Stray.md` with the reason `A definition must be inside its agent's folder.` (PersonaStore.cs:717-720).
- The six core Agents are unchanged.
- After step 4 Ctrl K finds `Stray` (its card shows title `Misplaced`, `Alias: str`) and the rejected block is gone.

**Fail if — any of these**

- The root file is silently ignored -> a user who drops a Persona in the wrong place sees nothing happen.
- Ctrl K finds `Stray` for the root file -> the one-level layout rule is not enforced.
- The rejected entry stays after the move -> the watcher missed the move or the block is stale.

**Inconclusive if**

The block lists other files too -> a leftover from an earlier test; delete it and rerun.

### AGENTSLIBRARY-42 — A second .md in an Agent folder is rejected, and work\ is the place for notes

**Free** · about 5 min

*Proves an Agent folder holds only its own definition: an extra markdown file is named in the rejected block while the real definition keeps loading, and moving the extra file under `work\` clears it.*

**Before you start**

- Core fixture set in place and loading cleanly (no rejected block on screen).

**Steps**

1. Create `<data>\Agents\Nova\Nova-notes.md` containing `Some notes.`
2. Open the Agents tab of the Team `Business`, wait about two seconds and read it without refreshing.
3. Create the folder `<data>\Agents\Nova\work\` and move `Nova-notes.md` into it.
4. Wait about two seconds and read the tab again.
5. Delete the `work` folder you created.

**Pass if — all of these**

- After step 2 the `Files that didn't load` block lists `<data>\Agents\Nova\Nova-notes.md` with the reason `An agent folder holds only its definition; use work/.` (PersonaStore.cs:771-774).
- Nova's roster card is still on the Agents tab, unchanged: the extra file does not take the real definition down.
- After step 4 the rejected block is gone and Nova is unchanged.

**Fail if — any of these**

- Nova disappears while the extra file is present -> one stray note takes a working Agent offline.
- The extra file is silently ignored -> the user is never told that their note is not part of the Persona.
- The file under `work\` is still reported -> the scan reaches into `work\`.

**Inconclusive if**

Windows refuses the move because the file is open in an editor -> close the editor and retry.

### AGENTSLIBRARY-43 — Files in _unsorted are rejected as migrated leftovers; other underscore folders are skipped

**Free** · about 4 min

*Proves the layout-migration quarantine folder is visible rather than silent, and that an ordinary folder starting with an underscore is not scanned at all.*

**Before you start**

- Core fixture set in place and loading cleanly (no rejected block on screen).

**Steps**

1. Create `<data>\Agents\_unsorted\old-persona.md` containing `---` on the first line and any text after it.
2. Create `<data>\Agents\_unsorted\deeper\readme.txt` containing `leftover`.
3. Create `<data>\Agents\_scratch\ignored.md` containing `not scanned`.
4. Open the Agents tab of the Team `Business`, wait about two seconds and read it without refreshing.
5. Delete the folders `_unsorted` and `_scratch`.

**Pass if — all of these**

- The `Files that didn't load` block lists BOTH `_unsorted\old-persona.md` and `_unsorted\deeper\readme.txt` (every file at any depth, any extension), each with the reason `Moved here by the layout migration.` (PersonaStore.cs:726-731).
- Nothing is listed for `_scratch\ignored.md`, and Ctrl K finds no Agent for any of the three files (PersonaStore.cs:736-739).
- After step 5 the block is gone again.

**Fail if — any of these**

- The `_unsorted` files are not listed -> leftovers from a layout migration vanish without explanation.
- `_scratch\ignored.md` is listed or loads -> the underscore-folder skip has regressed.

**Inconclusive if**

The block lists other files too -> a leftover from an earlier test; delete it and rerun.


### AGENTSLIBRARY-28 — An edit to an Agent's file from outside changes only what the file says; the stored Model and Effort stay

**Free** · about 10 min

*The identity test of the area: an Agent's Model and Effort are keyed by its frontmatter Name, so editing the Title, Teams or body of its file from outside must never reset them. (The old form of this test moved a file between Team sub-folders; the scan now reads one folder level only, so there is no such move.) The card is a `MudDialog` and its Model/Effort dropdowns are `MudSelect` — neither changes what this test checks.*

**Before you start**

- Core fixture set in place.
- A Model and an Effort stored for the Persona under test. If the Model dropdown offers real models, set them through the card as in step 2. If it offers only the agent's default, use the sqlite3 fallback in the notes ([`O-DB`](common.md#o-db)).

**Steps**

1. Create the folder `<data>\Agents\Mira\` and in it `Mira.md` containing exactly:
---
name: 'Mira'
title: 'Field Analyst'
alias: 'mir'
teams: ['Business']
---
You are Mira. Answer in one short sentence.
2. Open the Agents tab of the Team `Business`, press F5, click **Edit** on Mira's roster card, choose the **Haiku** entry in the **Model** dropdown and **low** in the **Effort** dropdown, then click **Save**.
3. Reopen the Mira card in View mode and write down: Name, Title, alias, which Team Agents tab holds it, the `Model` line, the `Effort` line and the `Persona file` path. Close the card.
4. If you have sqlite3, run `sqlite3 <data>\team.db "select * from persona_models; select * from persona_efforts;"` and note the rows for Mira.
5. With the browser open on the `Business` Agents tab and untouched, open `<data>\Agents\Mira\Mira.md` in a text editor, change `title: 'Field Analyst'` to `title: 'Senior Analyst'` and `teams: ['Business']` to `teams: ['Household']`, and save. Change nothing else.
6. Wait about two seconds and read the Agents tab WITHOUT refreshing.
7. Open Mira's card (`P-OPEN-AGENT`) and compare the seven facts with what you wrote down.
8. If you have sqlite3, re-run the query and compare the rows.
9. Delete the folder `<data>\Agents\Mira\`.

**Pass if — all of these**

- The Agents tab repaints on its own within about a second of the save, and Mira's roster card leaves it.
- Mira's card now reads Title `Senior Analyst`, and her roster card is on the `Household` Agents tab, not `Business`.
- Name, alias, `Model`, `Effort` and the `Persona file` path are IDENTICAL before and after.
- The `persona_models` and `persona_efforts` rows are still keyed by the frontmatter Name `Mira` and hold the same values.

**Fail if — any of these**

- The `Model` reverts to `Agent default` or the `Effort` to `Model default` after the edit -> a path- or content-derived database key has been reintroduced; this is the exact silent identity change that Teams-as-a-field exists to prevent, and nothing on screen announces it.
- The tile disappears and reappears as a different agent -> identity is being rebuilt from the file content or path.
- The page needs F5 to show the edit -> the watcher missed the write (see AGENTSLIBRARY-19).

**Inconclusive if**

The Model dropdown offers only `Agent default`, so no Model can be stored through the UI -> use the sqlite3 fallback: stop the app, run `sqlite3 <data>\team.db "insert into persona_models(persona_name,model) values('Mira','claude-haiku-4-5'); insert into persona_efforts(persona_name,effort) values('Mira','low');"`, restart the app, and read the values off the card before editing the file. If you have no sqlite3 either, run the test without the Model/Effort half and say so — but that half is the part that catches the silent failure.

> [!NOTE]
> Rows left behind when the folder is deleted from disk (step 9) are not judged here. Honour the standing convention: Model = Haiku, Effort = low.

### AGENTSLIBRARY-29 — New agent writes its own folder and file under Agents, with the Teams field it was given

**Free** · about 6 min

*Proves the create path composes a file that round-trips through the parser, in `Agents\<Name>\<Name>.md`, with the Team it was created in on its `teams:` line. The New agent card is a real `MudDialog`; its fields are unaffected.*

**Before you start**

- Core fixture set in place, so the `Business` and `Household` Teams already exist.
- No Persona named `Wren` exists.

**Steps**

1. Open the Agents tab of the Team `Business`, press F5, point at **Add an Agent** and select **From scratch** (`P-NEW-PERSONA` steps 1-2).
2. Read the identity field labels. The card has no **Teams** field.
3. Type `Wren` into **Name**, `Ops Lead` into **Title** and `wre` into **Alias**.
4. Type `You are Wren. Answer in one short sentence.` into the **Persona text** textarea.
5. If the **Model** dropdown offers real models, choose the **Haiku** entry and choose **low** in **Effort**. If it only offers `Agent default`, leave both alone and note it.
6. Click **Save draft**, then **Save changes** (`P-NEW-PERSONA` steps 3-4).
7. Open Wren's card (`P-OPEN-AGENT`) and read its header, Name, and `Teams:` line. Close it.
8. Put Wren into the `Household` Team too with `P-ADD-TO-TEAM` (on the `Household` Agents tab, **From other teams**, pick Wren, **Save changes**), then read where Wren's roster card appears.
9. In `T-B` run `Get-ChildItem -Recurse <data>\Agents -Filter Wren.md` and then `Get-Content <data>\Agents\Wren\Wren.md`.
10. Confirm there is no `Files that didn't load` block at the top of either Agents tab.

**Pass if — all of these**

- The New agent card has no **Teams** field.
- After **Save changes** Wren's card (step 7) shows header `Agent`, Name `Wren`, and a line reading `Teams: Business`.
- After step 8 Wren has a roster card on BOTH the `Business` and the `Household` Agents tabs.
- The file is at `<data>\Agents\Wren\Wren.md` (the app created the `Wren` folder; PersonaStore.cs:338-390), and it is the only Wren.md anywhere in the tree.
- Its contents are lowercase, single-quoted frontmatter: `name: 'Wren'`, `title: 'Ops Lead'`, `alias: 'wre'`, `teams: ['Business', 'Household']` (the Team it was created in, then the one added), then `---`, then the body.
- No `Files that didn't load` entry appears for the file just created.

**Fail if — any of these**

- The file lands anywhere else (at the `Agents` root, or under `<data>\Teams\`) -> the app is writing a layout the scan rejects or never reads.
- `teams:` reads `['Business, Household']` (one Team) -> the two Teams are being joined into one nonsense Team.
- The newly created file appears in the `Files that didn't load` block -> what the writer composes does not parse with what the reader requires; loud, but a real round-trip break.
- Wren's card cannot be opened by Ctrl K after the save -> the user cannot read back what they just made.

**Inconclusive if**

The Model dropdown is empty of real models -> that means no adapter is installed; the rest of this test still stands. Record that the Model/Effort half was skipped.

> [!NOTE]
> Delete the `Wren` folder afterwards unless AGENTSLIBRARY-30 or -32 uses it.

### AGENTSLIBRARY-30 — A save that would create a rejected file is refused before anything is written

**Free** · about 8 min

*Proves the write path validates first — no file is written and then explained, and nothing the user typed is thrown away.*

**Before you start**

- Core fixture set in place: `Nova\Nova.md` (alias `nov`) and `Rune\Rune.md`.

**Steps**

1. In `T-B` run `(Get-ChildItem <data>\Agents -Directory | Get-ChildItem -Filter *.md | Measure-Object).Count` and write down the Count (one folder level deep, so notes under `work\` do not count). Also write down the number of folders: `(Get-ChildItem <data>\Agents -Directory).Count`.
2. On the `Business` Agents tab open the **New agent** card (**Add an Agent** → **From scratch**). Type Name `Nova`, Title `Clash`, Alias `cl1`, body `You are a clash test.` Click **Save draft**, then **Save changes** if the draft is accepted. Record the message shown and whether your typed text is still in the fields.
3. Change only the Name to `Rune` and click **Save draft** (then **Save changes**) again. Record the message.
4. Change only the Name to `rune` (lower case) and click **Save draft** (then **Save changes**). Record the message.
5. Change the Name to `Clash1` and the Alias to `nov` (Nova's alias) and click **Save draft** (then **Save changes**). Record the message.
6. Change the Alias to `cl1`, clear the **Persona text** textarea completely, and click **Save draft** (then **Save changes**). Record the message.
7. Type `You are a clash test.` back into the body, change the Name to `Emily  Lee` (TWO spaces), and click **Save draft** (then **Save changes**). Record the message.
8. Change the Name to `Clash1` and the Alias to `-nope`, and click **Save draft** (then **Save changes**). Record the message.
9. Click **Cancel** to close the card.
10. Re-run both count commands and compare with what you wrote down. Confirm no new entry appeared in a `Files that didn't load` block.

**Pass if — all of these**

- Every attempt keeps the card OPEN and shows a message in red at the top of the card body; nothing you typed is lost.
- Name `Nova` -> `Persona 'Nova' already exists.`
- Name `Rune` -> `Persona 'Rune' already exists.` — the create path checks first whether `Agents\Rune\Rune.md` exists (PersonaStore.cs:366-369).
- Name `rune` -> `Persona 'rune' already exists.` — on Windows `rune` resolves to the same `Agents\Rune\Rune.md`, so a case-only difference still collides.
- Alias `nov` -> `Persona Alias 'nov' is also used by '<absolute path of Nova\Nova.md>'.`
- Blank body -> `Persona text must not be blank.`
- Name `Emily  Lee` -> `'Emily  Lee' is not a valid Persona name.`
- Alias `-nope` -> `'-nope' is not a valid Persona alias.`
- The .md count AND the folder count are UNCHANGED across every failed attempt (a refused save creates no folder either), and the rejected block gained no entry.

**Fail if — any of these**

- The file or folder count goes up after any refusal -> the file was written and then explained; the write path is validating after the fact.
- A new entry appears in `Files that didn't load` immediately after a save -> same thing, the loud version.
- The card closes on error and discards everything typed -> the user retypes their Persona text from scratch.
- The `Rune` or `rune` attempt succeeds -> two agents now share one Name (and one database row) and one of them silently loses its Model.

**Inconclusive if**

A message appears but its wording differs slightly from the text above -> record the exact wording you saw and flag it as a wording drift rather than a functional failure, unless the message fails to name the offending field or the colliding file, which IS a failure.

### AGENTSLIBRARY-31 — Renaming through the frontmatter: the tile renames, Model and Effort follow, and the folder and file are renamed with it

**Free** · about 10 min

*Proves a Name change through the card's textarea carries the stored Model and Effort with it and moves the Agent's folder and file to the new Name (ADR-0011), leaving no ghost behind.*

**Before you start**

- A Persona with a stored Model and Effort. Reuse `Mira` from AGENTSLIBRARY-28 (recreate its folder and file, then set Haiku / low through the card or the sqlite3 fallback in -28), or set Model = Haiku and Effort = low on a fresh Persona.

**Steps**

1. Press F5, open Mira's card with `P-OPEN-AGENT` and write down its `Model` line, its `Effort` line and its `Persona file` path. Close the card.
2. Open Mira's card again and click **Edit**.
3. Read the hint under the textarea, and any note about renaming.
4. In the textarea (which holds the WHOLE file, frontmatter included), change `name: 'Mira'` to `name: 'Mira Prime'`. Change nothing else. Click **Save**.
5. Wait about five seconds (the folder move runs after the save). Close whatever card is on screen, then look at the Agents tab of the Team that holds her (and Ctrl K): find the renamed Agent and read its Name and monogram.
6. Open the renamed Agent's card (`P-OPEN-AGENT`) and read its `Model`, `Effort` and `Persona file` lines.
7. In `T-B` run `Get-ChildItem -Recurse <data>\Agents | Where-Object Name -like 'Mira*'`.
8. If you have sqlite3, run `sqlite3 <data>\team.db "select * from persona_models; select * from persona_efforts; select id,name,kind from users;"` ([`O-DB`](common.md#o-db)).
9. Look at the left sidebar and note whether any Room is still named after the OLD name.

**Pass if — all of these**

- The Edit hint reads `Markdown. This is the whole Persona file, front matter included, and becomes the agent's system prompt. Saving restarts it, which clears what it remembers.`
- A note in Edit mode says `Renaming brings this agent's chats, its history and its work folder with it.` (AgentProfileCard.razor:290).
- A tile now reads `Mira Prime` with monogram `MP`, and no tile reads `Mira`.
- The renamed agent's card shows the SAME `Model` and the SAME `Effort` as before the rename.
- On disk the folder is now `<data>\Agents\Mira Prime\` holding `Mira Prime.md`, no `Mira` folder is left, and the card's `Persona file` path ends `Mira Prime\Mira Prime.md` (PersonaRenameCascade.cs:363-426).
- `persona_models` and `persona_efforts` hold a row keyed `Mira Prime` and NO row keyed `Mira`.
- If the Agent had an Agent row in `users` or a direct Room, the row is renamed in place and no Room named after the old name remains (ADR-0011). A fresh Mira has neither, so that half is then not observable: say so.

**Fail if — any of these**

- The `Model` or `Effort` reverts to `Agent default` / `Model default` after the rename -> the stored rows were not moved, and a user's settings vanished silently.
- A row keyed under the OLD name is left behind -> recreating an agent with the old name later would silently resurrect a setting nobody chose.
- The file keeps the old name inside a renamed folder, or the folder keeps the old name -> the move half-completed, and the next scan rejects the file as `An agent folder holds only its definition; use work/.` or names a mismatch.
- Both an old and a new tile appear -> the index is not replacing the old entry.

**Inconclusive if**

After Save, the card on screen looks empty (no Title, no Persona text, no `Persona file` section) with the OLD name in it -> that is the page reopening the card by the name it was opened with, and is a known rough edge, not the failure this test is judging. Close it and reopen the renamed Agent with Ctrl K instead; judge from the roster card and the reopened card. Record the empty-card observation as a note.

> [!NOTE]
> The folder move follows the code (ADR-0011, `PersonaRenameCascade`), not a run. Delete the `Mira Prime` folder afterwards unless AGENTSLIBRARY-32 uses it.

### AGENTSLIBRARY-32 — Remove takes the file, the Model and the Effort — and not the chats

**Free** · about 8 min

*Proves removal is confirm-gated, deliberately non-cascading for Rooms and transcripts, and leaves no setting behind to resurrect.*

**Before you start**

- A Persona with a stored Model = Haiku and Effort = low (reuse Mira Prime from -31, or set them on a fresh one). The Chief of Staff cannot be used: its card has **Reset to default**, not Remove.
- Ideally the app is running in the `P-LAUNCH-UAT` lane, so Ada (a real Persona with a real Room) can be the one removed and the non-cascade half observed. There `<data>` is `C:\seeds\huddle\uat\data` and Ada's own file is `<data>\Agents\Ada\Ada.md`.

**Steps**

1. Press F5, open the agent's card (`P-OPEN-AGENT`) and note which Team's Agents tab holds it and whether that Team has any other member.
2. In the card, click the red Remove icon button (tooltip `Remove`). Read the button row that appears.
3. Click **Cancel**. Confirm the row returns to its previous buttons and nothing was deleted.
4. Click Remove again, then click **Remove** in the `Remove {name}?` prompt.
5. Read the Agents tab: is the card closed, is the roster card gone, and does Ctrl K still find the Name?
6. In `T-B` run `Get-ChildItem -Recurse <data>\Agents\<that name>` and note what is left.
7. If you have sqlite3: `sqlite3 <data>\team.db "select * from persona_models; select * from persona_efforts; select id,name,kind from users;"` and also check the `rooms` table.
8. Look at the left sidebar: is the removed agent's Room (if it had one) still listed, with its messages intact?
9. Now open the **New agent** card (`P-NEW-PERSONA`) and recreate a Persona with the SAME Name, Title and Alias, leaving Model and Effort untouched. Click **Save draft**, then **Save changes**.
10. Open the recreated agent's card and read its `Model` and `Effort` lines.

**Pass if — all of these**

- Remove does not delete on the first click: the row swaps to a `Remove {name}?` prompt with `Remove` and `Cancel`.
- `Cancel` restores the previous button row and deletes nothing.
- The prompt's `Remove` closes the card and removes the roster card from the Agents tab.
- `<that name>.md` is gone from disk. The Agent's folder and its `work\` notes remain: Remove deletes the definition file only (PersonaStore.cs:472-480).
- `persona_models` and `persona_efforts` hold NO row for that name.
- Any Room and transcript the agent had are STILL in the sidebar and still readable — removal is deliberately non-cascading for chats. (Its Avatar entry and image are deleted with it: PersonaRenameCascade removal handler.)
- The recreated Persona of the same Name reads `Model: Agent default` and `Effort: Model default`, and is created in the surviving folder.

**Fail if — any of these**

- One click deletes with no confirmation -> a destructive action with no guard.
- The recreated agent shows the OLD Model or Effort -> a setting nobody chose has been resurrected; the row survived the removal.
- The Room or the transcript disappears -> removal cascaded; chat history a user expected to keep is gone.
- The roster card lingers after Confirm -> the index is not being refreshed on the write path.

**Inconclusive if**

The agent never had a Room (nothing ever connected as it), so the non-cascade half cannot be observed -> run this against Ada instead in the `P-LAUNCH-UAT` lane: open its card, click **Message**, send `hi`, then rerun. Messaging Ada is free.

### AGENTSLIBRARY-33 — Open launches the Persona file on the SERVER, from its Agent folder

**Free** · about 4 min

*Proves the Open action opens the real discovered file, and fails with a message on the card rather than taking the page down.*

**Before you start**

- The app and the browser are on the same machine (the expected single-user setup).
- Core fixture set in place.

**Steps**

1. Press F5, open Nova's card (`P-OPEN-AGENT`), and click **Open**.
2. Note whether an editor opens, and read the path in its title bar.
3. Close the editor. Close the card.
4. Open Rune's card and click **Open**.
5. Read the path in the editor's title bar and compare it with the card's `Persona file` line.
6. Close the editor and the card, and check the `dotnet run` console and the page for errors.

**Pass if — all of these**

- **Open** launches whatever the machine has registered for `.md`, showing the real file.
- For Rune, the file opened is `<data>\Agents\Rune\Rune.md`.
- The path in the editor matches the card's `Persona file` line exactly (the card reads the path from the index: PersonaStore.cs:483-489).
- No yellow `An unhandled error has occurred.` banner appears, and the page stays usable.

**Fail if — any of these**

- Nothing happens and no message appears anywhere -> the user cannot tell whether the click registered.
- The wrong file opens -> the path is being reconstructed rather than read from the index.
- The yellow `An unhandled error has occurred.` banner appears -> an unhandled exception is taking down the circuit; a failed launch must show a line like `Could not open 'Nova': ...` on the card instead (AgentProfileCard.razor:2044,2053).

**Inconclusive if**

Nothing opens AND the card shows an error line beginning `Could not open '...'` -> that is the handled path working; your machine simply has no application registered for `.md`. Register one (or set Notepad as the default for .md) and rerun, or record the test as environment-blocked with the handled-error observation noted.

> [!NOTE]
> This action launches a process on the machine running the app, not the one running the browser. That is deliberate and documented.

### AGENTSLIBRARY-34 — Neither a page load, a filter change, nor a View card ever spawns an adapter

(Since 2026-11-18 "page load" is a Agents tab load and "filter change" is switching between Teams.)

**Free** · about 6 min

*Proves the model-catalog probe is confined to the Create/Edit card, so browsing the directory never starts a process.*

**Before you start**

- An ACP adapter is installed: `tools\acp\node_modules` exists. (If it does not, run `pwsh tools/acp/install.ps1` or record this test as inconclusive.)
- Launch the app fresh just before this test: the model list is probed once and then cached for the life of the app, so a probe an earlier test already made leaves nothing to observe in step 7 (F13: ModelCatalogProbe.cs:109-112).
- Core fixture set in place. Works with ACP on or off — the probe is deliberately not gated by that flag.

**Steps**

1. In `T-B` run `O-ADAPTERS-LIST` and note every PID (probably none).
2. In the browser, open the Agents tab of the Team `Business`.
3. Run `O-ADAPTERS-LIST` again.
4. Open the Agents tab of each other Team in the sidebar in turn.
5. Run `O-ADAPTERS-LIST` again.
6. Open an Agent's card in View mode (`P-OPEN-AGENT`). Read it, then run `O-ADAPTERS-LIST` again.
7. Now click **Edit** on that card. Wait for the Model dropdown's hint to settle, then run `O-ADAPTERS-LIST` again.
8. Check the `dotnet run` console for probe lines.
9. Click **Cancel** to close the card.

**Pass if — all of these**

- The set of `node` PIDs is unchanged after the page load.
- It is unchanged after every filter change.
- It is unchanged after opening a View card.
- It CHANGES (a new node process appears, or the console logs a probe) only after **Edit** (or the **New agent** card) is opened — that is the expected place for it.
- No probe log line (`No ACP adapter is installed; the model catalog is empty.`, or a probe warning) appears in the console before a Create/Edit card is opened.

**Fail if — any of these**

- A `node` process appears on page load or on a filter change -> the adapter is being probed for browsing; it spends no tokens, so this is silent and shows up only as latency and a process — which is exactly why it is worth watching by hand.
- A `node` process appears when a View card is opened -> reading an agent must never start anything.

**Inconclusive if**

No adapter is installed, so no probe could ever spawn -> this test cannot distinguish 'correctly never probes' from 'has nothing to probe'. Install the adapter (`pwsh tools/acp/install.ps1`) or record the test as inconclusive; do not record a pass. Likewise if nothing changes even at step 7, the model list was already cached: relaunch the app and rerun.

### AGENTSLIBRARY-35 — Restart with ACP switched off reports why and starts nothing

**Free** · about 5 min

*Restart on a card is a direct call, not the startup path, so it needs its own guard: with ACP off it must say so on screen and bring nothing Online (PersonaSupervisor.cs:508-512). This test pins that guard.*

**Before you start**

- App started under `P-LAUNCH-FREE` (ACP off, the ship default). Do not pass `--Team:Acp:Enabled=true`.
- Core fixture set in place; every tile reads `Offline`.

**Steps**

1. In `T-B` run `O-ADAPTERS` and note the count.
2. Press F5, open Nova's card (`P-OPEN-AGENT`) and read the hint under the action row.
3. Click **Restart**.
4. Read the card's status line and any red paragraph under it. Then close the card and hover the badge on Nova's avatar on the `Business` Agents tab.
5. Run `O-ADAPTERS` again and `Get-ChildItem <data>\Agents\Nova`.
6. Do NOT send the agent a message.

**Pass if — all of these**

- The hint under the action row reads `Like saving an edit, this restarts the agent, which clears what it remembers.`
- After **Restart** the card still reads `Offline` and shows the reason `Agents are switched off in this configuration, so nothing was started.`; the badge's tooltip carries the same text after `Offline: `.
- `O-ADAPTERS` is unchanged (still `0`) and `Nova` holds only `Nova.md`: nothing was started and no `work\` folder was created (the Work Dir is `Agents\Nova\work`, F1).
- The page does not crash and no yellow `An unhandled error has occurred.` banner appears.

**Fail if — any of these**

- The badge moves to `Starting` or `Online`, or an adapter process appears -> the Restart door ignores `Acp:Enabled` and brings a Persona fully Online with ACP configured off; every later Message to it is billable and nothing on screen said so.
- The button does nothing at all and no reason appears -> the user has a control that silently does nothing.

**Inconclusive if**

The app was launched with ACP on -> the guard is not in play; relaunch under `P-LAUNCH-FREE` and rerun.

### AGENTSLIBRARY-36 — Restart is busy-flagged and cannot be double-fired

**Free** · about 5 min

*Proves one agent cannot be handed two adapter processes by an impatient double click. It runs on the free mock adapter, where Restart actually starts something.*

**Before you start**

- App launched with [`P-LAUNCH-UAT`](common.md#p-launch-uat) (the mock adapter, ACP on, free); Ada's card (`P-OPEN-AGENT`) reads `Online`. Restart is offered in every state except Starting (F4: AgentProfileCard.razor:182), so an Online card is a valid subject.

**Steps**

1. In `T-B` run `O-MOCK-ADAPTERS` and note the count.
2. Open Ada's card.
3. Click **Restart** and immediately click it two more times as fast as you can.
4. Watch the button's label and whether it responds to the extra clicks.
5. When it settles, read the button row and the status badge again.
6. Run `O-MOCK-ADAPTERS` and compare the count with step 1.

**Pass if — all of these**

- While the restart is in flight the button reads `Restarting…` and is disabled — the extra clicks do nothing.
- When it finishes, the button returns to `Restart` (it is shown in every state except Starting; it does not disappear when the agent is Online) and Ada settles back to `Online`.
- The mock process count is at most ONE higher than in step 1 at any moment, not two or three.

**Fail if — any of these**

- Two or three new mock processes appear for one agent -> the busy flag is gone; a double click doubles an agent's adapter processes.
- The button stays disabled forever after a restart -> the user can never retry.
- A restart failure crashes the circuit instead of showing its message on the card -> an expected failure is being treated as fatal.

**Inconclusive if**

The restart completes too fast to see the `Restarting…` label -> judge on the process count alone and say that the label transition was too fast to observe.

### AGENTSLIBRARY-37 — Offline WITH a reason, in both of its homes

**Free** · about 5 min

*Proves that when a start genuinely fails, the exact reason reaches the user twice — as a tooltip on the tile's avatar badge and as a full line on the card — and clears when the cause is fixed.*

**Before you start**

- Core fixture set in place.
- You are willing to rename `tools\acp\node_modules` aside and back. With the adapter missing, no adapter process ever starts in this configuration, so nothing is spent even though ACP is switched on (the Chief of Staff cannot take its paid Greeting without an adapter).

**Steps**

1. Stop the app with Ctrl+C in the `dotnet run` terminal.
2. Rename the folder: `Rename-Item tools\acp\node_modules node_modules.off`
3. Restart the app with ACP ON: `dotnet run --project src/Huddle.App -- --Team:Acp:Enabled=true`
4. Open the Agents tab of the Team `Business` and press F5.
5. Read the avatar badge's tooltip on every roster card, then on the `Household` tab too.
6. HOVER the mouse over the badge on the Nova tile's avatar for three seconds and read the tooltip.
7. Click the Nova tile and read the red line directly under its status line.
8. Check the `dotnet run` console for a warning naming Nova.
9. Restore the adapter: `Rename-Item tools\acp\node_modules.off node_modules`
10. On the still-open Nova card, click **Restart** and watch the badge on both the tile and the card.

**Pass if — all of these**

- Every tile reads `Offline`.
- Hovering the badge shows a tooltip that begins `Offline: ` and then carries exactly: `No ACP adapter is installed for Persona 'Nova' on Adapter '<id>'. Run tools/acp/install.ps1 (or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.`, with `<id>` the Persona's Adapter id (DotAcpAgentHostFactory.cs:138-141).
- The card shows that SAME text as a full paragraph line under the status — not as a tooltip.
- The console logs a warning `Persona 'Nova' failed to start.` (PersonaSupervisor.cs:617).
- After restoring node_modules and clicking **Restart**, the badge moves `Offline` -> (briefly `Starting`) -> `Online`, and the reason disappears from both the tooltip and the card.
- No `node` process runs while the adapter is renamed aside — no tokens, no money.

**Fail if — any of these**

- A tile reads `Offline` with NO tooltip although a reason exists -> the diagnostic is the entire value of this state; without it the user cannot tell a never-started agent from a failed one.
- The reason appears only on the tile, or only on the card -> the two homes have drifted apart.
- The reason wording uses `bot`, `backend`, `LLM`, `rate limit` or `quota` -> the Reason is human-facing interface copy with a binding glossary; those words are banned.
- After restoring the adapter, Restart leaves the stale reason on screen -> the reason is not cleared when the condition clears.

**Inconclusive if**

You cannot rename `node_modules` because a process has it open -> stop the app first (step 1), and close any editor with files open under it. If the app fails to start with the `--Team:Acp:Enabled=true` argument, check the argument spelling — it must be exactly `--Team:Acp:Enabled=true` after a bare `--`.

> [!NOTE]
> Put `node_modules` back before moving on; AGENTSLIBRARY-38 needs it renamed aside again, so you may prefer to run -38 immediately after this one. Step 10 starts Nova on the real adapter, which spends nothing until a Message is sent: send none.

### AGENTSLIBRARY-38 — Health outranks pipe liveness: a live pipe with a failed start must read Offline

**Free** · about 8 min

*The one browser-visible proof of the precedence rule, and of the silent failure it prevents — a runner whose loops have died leaves its pipe open, so connectivity-first reporting shows a deaf agent as healthy.*

**Before you start**

- PowerShell available to run `tools/echo-bot.ps1` (`P-ECHO-BOT`); it stands in for a live pipe client.
- `tools\acp\node_modules` renamed aside so no adapter can be found (nothing is spent, as in AGENTSLIBRARY-37).

**Steps**

1. Stop the app with Ctrl+C.
2. Confirm the adapter is unfindable: `Test-Path tools\acp\node_modules` must return `False` (rename it aside if not: `Rename-Item tools\acp\node_modules node_modules.off`).
3. Create the folder `<data>\Agents\Scout\` and in it `Scout.md` containing exactly:
---
name: 'Scout'
title: 'Pipe Tester'
alias: 'sct'
---
You are Scout. Answer in one short sentence.
4. Start the app with ACP ON: `dotnet run --project src/Huddle.App -- --Team:Acp:Enabled=true`
5. In the console, confirm the warning `Persona 'Scout' failed to start.` appears.
6. In a third PowerShell window at the repo root, run `pwsh tools/echo-bot.ps1 -Name Scout` and confirm it prints `Sent hello. Listening for messages (Ctrl+C to exit)...`.
7. Open the Agents tab of a Team that holds `Scout` (add it with `P-ADD-TO-TEAM`, since this fixture has no `teams:` line) and press F5.
8. Read the `Scout` roster card's avatar badge (glyph and tooltip).
9. Hover the badge on its avatar and read the tooltip.
10. If you have sqlite3, run `sqlite3 <data>\team.db "select id,name,kind from users;"` and confirm the `Scout` agent row is there.
11. Stop the echo bot, delete the folder `<data>\Agents\Scout\`, restore the adapter (`Rename-Item tools\acp\node_modules.off node_modules`) and restart the app without the ACP flag when you are finished.

**Pass if — all of these**

- Both facts are true at once: the echo bot is connected (the pipe is live) AND the console shows `Persona 'Scout' failed to start.` (health says Offline).
- The `Scout` tile reads `Offline`, not `Online`.
- Hovering the chip shows the start-failure reason in its tooltip.
- The `users` table still holds the connected `Scout` agent row — the pipe really is up, which is the whole point.

**Fail if — any of these**

- The `Scout` tile reads `Online` -> precedence has been reversed: connectivity is being checked before health. This is the documented silent failure — an agent whose loops have died keeps a green badge for as long as its dead pipe stays open, it compiles, and it passes every test written before health existed.
- The tile reads Offline but carries NO reason -> half the signal is lost; the user sees a fault with no diagnosis.

**Inconclusive if**

Only one of the two facts holds -> the setup is not in the state this test needs. The echo bot not connecting means the pipe name differs (the key is `PipeName` in appsettings.json; pass `-Pipe <name>`); no `Persona 'Scout' failed to start.` means ACP is not actually enabled or the adapter is still findable. Fix the setup and rerun; do not judge the badge until both are present.

> [!NOTE]
> This is the only combination that can prove the ordering through a browser, and it is entirely free.

### AGENTSLIBRARY-39 — The whole page in Dark mode, with a card open and a rejected file present

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### AGENTSLIBRARY-40 — Degraded, with the reason — and the two things that must NOT badge

**💰 Spends money** · about 20 min

*Proves a failed Turn moves an agent to Degraded with a reason that grows from one failure to a streak of three, while a Human-stopped Turn and a spent Room Budget deliberately do not.*

**Before you start**

- Lane is [`P-LAUNCH-PAID`](common.md#p-launch-paid-), including its first step (set the Chief of Staff to Haiku / low). It sets `Team__AgentMessageBudget` to `4`; for this test set `$env:Team__AgentMessageBudget = '2'` before `dotnet run` so the Budget half is reachable cheaply.
- A working installed adapter and working provider credentials.
- One Persona configured Model = **Haiku** and Effort = **low** (the standing convention — do not use a larger model for this).
- The ability to interrupt the provider mid-Turn (disable the network adapter, or invalidate credentials briefly).

**Steps**

1. Start the app and confirm the agent under test reads `Online` (avatar badge on its Agents-tab roster card).
2. Open its Room from the card's **Message** link (or via **New room** in the sidebar) and send `Say hello in five words.`
3. While that Turn is running, cut the network (disable the adapter) so the Turn fails. Restore the network. Read the roster card's badge and open the card (`P-OPEN-AGENT`) now (one failed Turn).
4. Repeat the previous two steps twice more, so THREE Turns in a row have failed. Read the tile's avatar badge (glyph and tooltip), and open its card and read the line under the status and the action row.
5. Restore normal operation and let one ordinary Turn complete. Read the tile.
6. Now prove a stopped Turn does not badge: send `Write a very long story.` and click **Stop** while it is replying. Read the tile.
7. Now prove a spent Budget does not badge: with `Team__AgentMessageBudget` low, let the agent post until the Room refuses further agent messages. Read the tile again.
8. Read the reason strings you collected and check them against the banned vocabulary list.

**Pass if — all of these**

- After one failed Turn the tile moves to `Degraded` (dash glyph) and the reason `A Turn in Room '<room>' failed — <detail>` (PersonaRunner.cs:289-303). A single failure is reported, not hidden.
- After the third consecutive failed Turn the reason reads `3 consecutive Turns have failed; this is unlikely to be transient — the last, in Room '<room>': <detail>`.
- The tile's tooltip carries the reason, and the card shows that same reason as a full line, plus a **Restart** button.
- After one ordinary Turn completes, the tile returns to `Online` with no reason, and the streak restarts from zero (PersonaRunner.cs:272-277).
- After a Human clicks **Stop** on a live Turn, the tile does NOT read `Degraded` and no new reason appears — a stopped Turn is not a failure (RoomSession.cs:1078-1079).
- After a Room hits its Budget and refuses further agent messages, the tile is unchanged — a spent Room Budget is not a fault.
- No reason string we write contains `bot`, `backend`, `LLM`, `rate limit` or `quota`. (`<detail>` is the adapter's own error text; judge only the sentence around it.)

**Fail if — any of these**

- The second failure's reason still reads as the first, or the third does not switch to the `consecutive Turns` wording -> the streak is not being counted.
- A Human-stopped Turn marks the agent `Degraded` -> the app is badging an agent that did exactly what it was told.
- A spent Room Budget marks the agent `Degraded` -> the app is badging its own working safety cap as a fault.
- `Degraded` never appears after genuine failures -> the health signal is not reaching the badge, and a broken agent looks fine.

**Inconclusive if**

You cannot interrupt the provider reliably mid-Turn, so the failures do not land as failures -> record the test as not-executed rather than passing it. Likewise if the agent never comes Online at all, the whole test is blocked: fix that first (AGENTSLIBRARY-37 diagnoses it).

> [!NOTE]
> COSTS MONEY. Budget: a handful of short Haiku/low Turns, each a few hundred tokens — well under a cent in total. Do NOT attempt the per-Persona token-Budget route to Degraded (`Team__Acp__TokenBudget`, PersonaRunner.cs:258-270); it is a different cause and not worth buying. Quota, network and credential failures are indistinguishable to the app by design and are all reported the same way — do not file that as a wording bug.

---

Back to [the manual test script](../manual-tests.md).
