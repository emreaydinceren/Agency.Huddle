# Document history

Prove a Markdown document's History end to end in a real browser: that the Human's saves and Agents' `log_change` calls reach the byline, the Recent edits panel and the History at the bottom of the document; that a rename or move carries the History with it; and, the one open question this page exists to answer, how often real Agents actually log their edits. The automated suite proves the store, the tool, the view functions and the three components in bUnit, and a paid probe with a stand-in tool measures the same bet (`TEAM_E2E_DOCHISTORY=1`, `tests/Huddle.Tests.Agents/Live/DocHistory/`). This page proves what only a browser shows. The design is [the DocHistory spec](../../Huddle.DocHistory-Specifications.md) and [ADR-0060](../../adr/0060-a-documents-history-is-what-its-editors-report.md).

**6 tests** · 3 free, 3 paid 💰 · about 1.4 hours. Written 2026-10-18; **not yet run by a person.**

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat). The lane gives you **Ada** and **Bo** on the free mock adapter and `<data>` is `C:\seeds\huddle\uat\data`. This area adds:

1. A pinned Library root, as in [the Library page](library.md): in `T-B` create `$vault = 'C:\seeds\huddle\uat\vault'` and `Set-Content "$vault\findings.md" "# Findings`n`nThree findings.`n"`, then add `$vault` in **Settings → Library** with the Name `Vault`.
2. For the paid tests, a real Teammate made with [`P-NEW-PERSONA`](common.md#p-new-persona): Name `Keeper`, Title `Keeper`, **Model** Haiku, **Effort** `low`, **Teams** blank. Ask it to work in `$vault`.
3. History files live under `<data>\doc-history\<root id>\`, one `.jsonl` per document. Open them only to check a result; never edit one while the app runs.

## Tests

### DOCHISTORY-01 — Two Teammates create and edit a document; the byline and the panel show both

**Paid 💰** · about 12 min · two short Turns

*Proves the round trip from an Agent's `log_change` call to the byline and the Recent edits panel.*

**Before you start**

- Setup is done, including `Keeper`. Open `findings.md` in the Library in Read mode with the Right sidebar open.

**Steps**

1. In a Room with `Keeper`, send `@Keeper Create notes.md in the Vault folder with one paragraph about this week.`
2. Open `notes.md` in the Library.
3. In the same Room send `@Keeper Add a second paragraph to notes.md.`
4. Look at the byline under the toolbar and at the Recent edits panel.

**Pass if — all of these**

- At step 2 the byline reads `Written by Keeper` with Keeper's avatar and a time of the form `today HH:mm`.
- At step 4 the panel lists two rows, the `edited` one first, each with Keeper's avatar and a one-line summary.
- No entry is attributed to anyone but `Keeper`.

### DOCHISTORY-02 — A review that changes nothing is a "Reviewed by"

**Paid 💰** · about 8 min · one short Turn

*Proves `reviewed` appears in the byline for a document nobody edited.*

**Before you start**

- DOCHISTORY-01 has passed, or `notes.md` exists with an entry from `Keeper`.

**Steps**

1. Send `@Keeper Check notes.md for spelling mistakes. Change nothing unless one is wrong.`
2. Look at the byline.

**Pass if — all of these**

- The byline has a `Reviewed by Keeper` part, and `notes.md` is unchanged on disk.
- If Keeper did not log a review, record the result as Fail and the reply as evidence: this test measures the same risk as DOCHISTORY-05.

### DOCHISTORY-03 — Saving in the Library adds an entry under the Human's Name without a reload

**Free** · about 6 min

*Proves the Human's saves log themselves, and that a new entry appears without a reload.*

**Before you start**

- Setup is done. Open `findings.md` in Read mode and look at the bottom of the document.

**Steps**

1. Note the History section at the bottom. Switch to Edit, add a line and press Ctrl+S, then switch back to Read.
2. Look at the byline and at the History.

**Pass if — all of these**

- At step 1 the History says `No changes logged yet.` and there is no byline.
- At step 2 the byline shows `Written by You`, or your own Name, and a time of the form `today HH:mm`, and the History has one `Saved` row under the heading `Today`.
- Saving twice within five minutes shows one `Saved` row, not two.

### DOCHISTORY-04 — Renaming and moving a document carries its History

**Free** · about 8 min

*Proves a Library rename and move move the History file with the document.*

**Before you start**

- DOCHISTORY-03 has passed, so `findings.md` has a History.

**Steps**

1. Rename `findings.md` to `results.md` in the Library tree.
2. Open `results.md` and read its byline and History.
3. Move `results.md` into a new folder `archive`.
4. Open it again.

**Pass if — all of these**

- After step 2 and after step 4 the History still shows the `Saved` row, and the byline is unchanged.
- No `findings.md.jsonl` is left in `<data>\doc-history\`.

### DOCHISTORY-05 — How often do real Agents log their edits?

**Paid 💰** · about 40 min · ten short Turns

*Measures ADR-0060's main risk. There is no pass mark; the rate is the result.*

**Before you start**

- `Keeper` is ready and the Vault has ten short Markdown files named `a1.md` to `a10.md`.

**Steps**

1. For each file, send `@Keeper Fix any typos in a<n>.md and tighten the first sentence.` and wait for the reply.
2. After all ten, count the files whose History has an entry from `Keeper`.

**Pass if — all of these**

- You have recorded `<logged> of 10` in the tracker. At 8 or more, the design holds. Below 8, strengthen the tool description (a configuration change, ADR-0007) and run the paid probe again.

### DOCHISTORY-06 — The History is grouped by day, whole, and the byline leads to it

**Free** · about 10 min

*Proves the layout of a long History and the byline's scroll.*

**Before you start**

- Stop the app. In `<data>\doc-history\<root id>\` create `plan.md.jsonl` with seven lines, one per day for the last seven days, each of the form `{"v":1,"at":"<ISO time>","actor":"Mira","actorKind":"agent","kind":"edited","summary":"<a sentence over 100 characters>"}`, and `Set-Content "$vault\plan.md" "# Plan`n"`. Relaunch the app.

**Steps**

1. Open `plan.md` in Read mode and scroll to the bottom.
2. Scroll back to the top and click the byline.

**Pass if — all of these**

- The History has a heading for Today (or Yesterday) and then for each earlier date, newest first, and every summary is shown whole, wrapping onto more lines.
- Clicking the byline scrolls the History into view.
- In Edit mode neither the History nor the backlinks are shown.
