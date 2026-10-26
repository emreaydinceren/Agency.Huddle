# Right sidebar

Prove, in a real browser, what no bUnit test can: that the Right sidebar lays out at 90% zoom, that a Room and a Markdown document each show their panels, that an Outline heading scrolls the document in every mode, and that a collapsed panel stays collapsed after a reload. The automated suite proves the panel rules, the catalog and each panel's behaviour against fakes.

**9 tests** · 8 free, 1 paid 💰 · about 0.9 hours. Written 2026-10-16; **not yet run by a person**.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat). This area adds:

1. Wait until **Ada** and **Bo** are both **Online** on `/teammates`.
2. Open the Room `Ada` and send `hello`, so it has a Transcript.
3. Open `Library` and make a Markdown file `Plan.md` with a heading `# Plan`, a heading `## Scope` and a link to a second file, `Notes.md`. Make `Notes.md` with the heading `# Notes`.
4. Open the browser at `http://localhost:5100/`, and set the zoom to 90%.

## Tests

### RSIDE-01 — A Room shows Members, Tasks and Documents

**Free** · about 5 min

*Proves the three Room panels and their order.*

**Before you start**

- Setup is done.

**Steps**

1. Open the Room `Ada`.
2. Open the Right sidebar from the Top bar toggle (the `dock_to_left` icon).
3. Read the panel headings from top to bottom.

**Pass if — all of these**

- The Right sidebar is 280px wide and sits at the right edge, beside the Workspace.
- The headings read `Members`, `Tasks in this chat` and `Documents in this chat`, in that order.
- Each heading has an icon and a count, and nothing overlaps the Workspace at 90% zoom.

### RSIDE-02 — A Message that references a Task appears, and its jump works

**Free** · about 8 min

*Proves the Tasks panel follows the Room's Tasks, and that the Message it names can be found.*

**Before you start**

- RSIDE-01 has passed. Create one Task in the Tasks page, with the Room `Ada` as its Room.

**Steps**

1. In the `Ada` Room, use the `#` picker to reference the Task and send `Please look at #` followed by the Task's title.
2. Look at the `Tasks in this chat` panel.
3. Click the Task's row in that panel.

**Pass if — all of these**

- After step 1 the Message shows the Task as a chip.
- At step 2 the panel lists the Task, with its state.
- At step 3 the Task opens in its own Tab (its Task detail), and the Room's Tab stays open in the strip.

### RSIDE-03 — A Markdown document shows Outline and Linked documents

**Free** · about 5 min

*Proves the two document panels appear for a Markdown document and not for other files.*

**Before you start**

- Setup step 3 is done.

**Steps**

1. Open `Plan.md` in the Library, in Read mode.
2. Read the panel headings.
3. Open an image file from the Library.
4. Look at the Right sidebar.

**Pass if — all of these**

- At step 2 the panels are `Outline` and `Linked documents`.
- The `Outline` panel lists `Plan` and `Scope`, the second one indented.
- At step 4 the Right sidebar shows the empty state, with no panel headings.

### RSIDE-04 — An Outline heading scrolls the document in Read, Split and Edit

**Free** · about 8 min

*Proves that a heading click reaches the document in each mode, and that the page moves.*

**Before you start**

- RSIDE-03 has passed for `Plan.md`. Make the page tall enough to scroll: add 60 lines of text under `## Scope`.

**Steps**

1. In Read mode, click `Scope` in the `Outline` panel.
2. Read where the document now starts.
3. Switch the document to Split mode, scroll to the top, and click `Scope` again.
4. Switch to Edit mode, scroll to the top, and click `Scope` again.

**Pass if — all of these**

- After each click the heading `## Scope` is at the top of the visible document, in all three modes.
- The Outline panel stays open, and no page reloads.

### RSIDE-05 — Typing a heading in Split updates the Outline

**Free** · about 4 min

*Proves that the Outline follows the document text while it is being edited.*

**Before you start**

- `Plan.md` is open in Split mode.

**Steps**

1. Click at the end of the file in the editor pane.
2. Type a new line `## Risks` and wait two seconds.
3. Look at the `Outline` panel.

**Pass if — all of these**

- The `Outline` panel lists `Risks` after `Scope`, within two seconds of typing.
- The new heading is not added twice when you type more text after it.

### RSIDE-06 — A linked note opens from the Linked documents panel

**Free** · about 4 min

*Proves the link panel lists the linked note and opens it.*

**Before you start**

- `Plan.md` links to `Notes.md` with `[[Notes]]`, and `Notes.md` links back with `[[Plan]]`, as set up in step 3. The panel lists outgoing `[[wikilinks]]` only, so it needs both links.

**Steps**

1. Open `Plan.md` in Read mode.
2. Look at the `Linked documents` panel.
3. Click `Notes.md`.

**Pass if — all of these**

- At step 2 the panel lists `Notes.md`.
- At step 3 `Notes.md` opens in the same pane, and its `Linked documents` panel shows `Plan.md` (the back link).

### RSIDE-07 — A collapsed panel stays collapsed after a reload

**Free** · about 3 min

*Proves that the collapsed state is remembered per panel.*

**Before you start**

- The Room `Ada` is open with the Right sidebar showing.

**Steps**

1. Collapse the `Tasks in this chat` panel with its chevron.
2. Reload the page.
3. Look at the three panel headings.

**Pass if — all of these**

- After step 2 `Tasks in this chat` is still collapsed, and `Members` and `Documents in this chat` are open.

### RSIDE-08 — Two panes follow focus

**Free** · about 6 min

*Proves the Right sidebar shows the focused pane's Subject, not the other pane's.*

**Before you start**

- The Room `Ada` is open in the left pane, and `Plan.md` is open in a second pane to its right.

**Steps**

1. Click inside the left pane, the Room `Ada`, and look at the Right sidebar.
2. Click inside the right pane, `Plan.md`, and look at the Right sidebar.
3. Click the left pane again.

**Pass if — all of these**

- At step 1 the Right sidebar shows `Members`, `Tasks in this chat` and `Documents in this chat`.
- At step 2 it shows `Outline` and `Linked documents`, and no Room panels.
- At step 3 it shows the Room panels again.

### RSIDE-09 — An Agent's context bar appears, grows during a Turn and falls after /compact

**Paid** 💰 · about 20 min

*Proves the Members panel's context meter on a real Adapter: an Agent that has not spoken in the Room reads "—", its percentage and bar appear and grow while it works, and the bar falls after a compaction.*

**Before you start**

- Paid lane (`P-LAUNCH-PAID`, which sets `Team:Acp:Enabled=true`). Two paid Teammates, `Scratch` and `Sidekick`, are Online and both are Members of one group Room.
- The Right sidebar is open on that Room, with `Members` showing.

**Steps**

1. Read the `Scratch` and `Sidekick` rows in `Members`.
2. Send `@Scratch list the files in your work folder and describe each one` and watch the `Scratch` row until the reply posts.
3. Read both rows again.
4. Send `@Scratch /compact` and wait for the outcome Message.
5. Read the `Scratch` row. Hover its percentage.

**Pass if — all of these**

- At step 1 both rows read `—` and neither has a bar.
- During step 2 the `Scratch` row gains a percentage and a bar, and the token count in its tooltip rises while the Turn runs (a short Turn can stay within one whole percent).
- At step 3 `Sidekick` still reads `—` with no bar.
- At step 5 the `Scratch` percentage is lower than it was at step 3, and its tooltip reads `... tokens used in this Room`.

**If it fails**

- Step 2 never shows a bar -> the Adapter sent no usage, or `RoomSession.CountUsage` did not report it; check the Adapter's profile first.
- Step 3 shows a bar on `Sidekick` -> a reading is keyed by the wrong Agent id.
- Step 5 is unchanged or higher -> the meter is summing or ignoring the fall.
