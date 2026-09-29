# Teams: Sidebar, Members, Files, Tasks and Shared Memory

Prove the Teams feature end to end in a real browser: that the sidebar lists Teams and Projects
with the right actions, that Members, Files and Tasks tabs navigate and function correctly, that
creating Teams and Projects works with the right validation, and — the one case this page exists
to answer — whether a real Agent's writes to a Team's shared Memory surface in another member's
next Turn.

**18 tests** · 16 free, 2 paid 💰 · **none of them has been run yet.** This page documents what to
run, not a result — see [Known limits](../known-limits.md) for what that leaves unverified today.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

Background: [Huddle.TeamPages-Specifications.md](../../Huddle.TeamPages-Specifications.md) (§2
the Use cases T0–T15), [the Project Plan](../../Huddle.TeamPages-ProjectPlan.md) (D7 delivery),
and the routing and component layout in `src/Huddle.App/Components/Teams/` and
`src/Huddle.App/Components/Pages/TeamPage.razor`.

## Setup

Run [`P-BUILD`](common.md#p-build), then `P-LAUNCH-FREE` — Teams is purely navigational, with no
real Adapters needed for most tests. **Exceptions marked 💰 below spawn Agents and need real
`claude` login and `node` on PATH.** Setup adds:

1. **The sidebar Teams group is visible by default.** Confirm it appears between Chats and
   Teammates in the left sidebar with a label `Teams` and a `+` icon.

## Tests

### TP-T0 — Click Business team and open Members tab

**Free** · 1 min

*Proves a Team link in the sidebar opens the Team page and shows the Members tab by default,
with the Team row highlighted and a list of members with avatars, names, titles and aliases.*

**Steps**

1. Verify `Teams/Business/` exists with two members: Nova and Ada (the fixture creates them).
2. In the sidebar Teams group, click `Business`.

**Pass if**

- The URL is `/teams/Business`.
- The Members tab is active (first tab).
- The members list shows Nova and Ada, each with avatar, Name, Title (e.g. "VP Product") and Alias (e.g. "nova").
- The `Business` Team link in the sidebar is highlighted or underlined (selected state).

**Fail if**

- The page does not load or shows an error.
- Members are listed incorrectly or are missing.
- The sidebar selection is not visible.

### TP-T1 — Add member to Team with restart warning

**Free** · 2 min

*Proves the Add member button opens a dialog with an autocomplete for unassigned Personas, shows
a warning that adding restarts the Persona, and the member is added to the Team's definition and
appears in the list.*

**Steps**

1. On Business Members tab, click the `+ Add member` button above the members list.
2. In the dialog, type `ki` in the autocomplete field.
3. Click `Kim` in the dropdown.
4. Verify the warning text appears: "Adding Kim restarts it and clears its conversation memory."
5. Click the `Add` button.

**Pass if**

- The dialog warning reads exactly: "Adding Kim restarts it and clears its conversation memory."
- Kim appears in the Business members list after the dialog closes.
- `Kim`'s definition includes `teams: ["Business"]` (check in File explorer or Editor).

**Fail if**

- The warning text is missing, truncated or different.
- The Add button fails or the dialog remains open.
- Kim does not appear in the members list.

### TP-T2 — Remove member from Team with inline confirm

**Free** · 2 min

*Proves the row menu on a member opens with a Remove option, shows an inline confirmation with
restart warning, and the member is removed from the Team's definition and list.*

**Steps**

1. On Business Members tab, hover over the Ada row to reveal the row menu (⋯ button).
2. Click the row menu button.
3. Click `Remove from Team` in the popup menu.
4. Read the confirmation text that appears inline below the row.

**Pass if**

- The row menu appears only when hovering (not on open/click of the row itself).
- The popup menu contains `Remove from Team`.
- An inline confirmation appears reading exactly: "Remove Ada from Business? Ada restarts and loses its conversation memory."
- Clicking the confirmation removes Ada: she leaves the members list and her `teams` field no longer includes `Business`.

**Fail if**

- The menu or confirmation text is missing or different.
- The row remains in the list after confirming.
- Ada is not removed from the `teams` field in her definition.

### TP-T3 — Search members by name, alias or title

**Free** · 1 min

*Proves the search box filters the members list in real time, matching any part of Name, Alias or
Title, ignoring case.*

**Steps**

1. On Business Members tab, locate the search box labeled "Search members".
2. Type `no` (case insensitive, matches Name starting with "No").
3. Observe the members list.
4. Clear the search box.

**Pass if**

- Only Nova remains visible (Name matches "no").
- Ada is hidden while `no` is in the search box.
- Clearing the search shows all members again.

**Fail if**

- Both members remain visible or the list does not filter.
- The search is case-sensitive or does not match partial strings.

### TP-T4 — Open Files tab and create new note

**Free** · 2 min

*Proves the Files tab shows a folder tree and the New note button opens the Library's New note
dialog, creating a file in the Team's folder.*

**Steps**

1. On Business page (any tab), click the `Files` tab.
2. Verify a folder tree appears with the Team's files and folders.
3. Click the `+ New note` button.
4. Name the note `team-note` and confirm.

**Pass if**

- The Files tab shows a tree of folders and files under `Teams/Business/`.
- The New note dialog opens (the Library's standard dialog).
- A file `team-note.md` is created under `Teams/Business/` and appears in the tree.

**Fail if**

- The Files tab does not load or appears empty with no tree.
- The New note button fails or the dialog does not open.
- The file is not created or appears in the wrong folder.

### TP-T5 — Search files by name

**Free** · 2 min

*Proves the search box in the Files tab replaces the tree with a flat list of matching files and
folders with paths, and clearing the search restores the tree.*

**Steps**

1. On Business Files tab, locate the search box labeled "Search files".
2. Type `brief`.
3. Observe the list that replaces the tree.
4. Clear the search box.

**Pass if**

- The tree is replaced by a flat list of files and folders whose names contain "brief".
- Each result shows its path (e.g. "Team › Business › brief-notes").
- Clearing the search restores the original folder tree as it was.

**Fail if**

- The search does not filter or the tree is not replaced.
- The search is case-sensitive or does not find partial matches.
- The tree is not restored after clearing.

### TP-T6 — Open Project page scoped to project folder

**Free** · 1 min

*Proves clicking a Project in the sidebar navigates to the Project's page with the URL scoped to
the Project, showing the Files tab scoped to the Project's folder, with no Members tab.*

**Steps**

1. In the sidebar Teams group, expand Business (click the chevron if not expanded).
2. Click the `Marketing Project` project link under Business.

**Pass if**

- The URL is `/teams/Business/projects/Marketing%20Project`.
- The Files tab is shown by default.
- The file tree is scoped to `Teams/Business/Marketing Project/` (only files in that folder visible).
- The Members tab is not present (no tabs for adding/viewing members).
- The Files and Tasks tabs are present.

**Fail if**

- The URL is incorrect or the page does not load.
- The Files tab shows files outside the Project folder.
- The Members tab is visible.

### TP-T7 — Open Tasks tab on Team and create new task

**Free** · 2 min

*Proves the Tasks tab shows a Board with swimlanes by Project, including unassigned tasks, and
the New task button opens the Task dialog with Team defaulted.*

**Steps**

1. On Business page, click the `Tasks` tab.
2. Verify the Board displays swimlanes for each Project (e.g. "Marketing Project", "Unassigned").
3. Click the `+ New task` button.
4. In the Task dialog, verify the Team field defaults to `Business` and note the dialog structure.
5. Cancel the dialog.

**Pass if**

- The Tasks tab shows a Board with swimlanes grouped by Project.
- All Tasks in Business (including those in Projects) are visible across swimlanes.
- The New task dialog opens with Team pre-filled as `Business`.

**Fail if**

- The Tasks tab does not load or the Board is empty.
- The Team field is not pre-filled or shows a different Team.
- The New task button fails.

### TP-T8 — Open Tasks tab on Project and create new task

**Free** · 2 min

*Proves the Tasks tab on a Project page shows only that Project's Tasks, and the New task button
defaults the Team and Project correctly.*

**Steps**

1. On Business › Marketing Project page, click the `Tasks` tab (if not already active).
2. Verify the Board shows only Marketing Project Tasks (no other swimlanes).
3. Click the `+ New task` button.
4. In the Task dialog, verify both Team and Project fields: Team = `Business`, Project = `Marketing Project`.
5. Cancel the dialog.

**Pass if**

- The Board shows only Marketing Project Tasks (no "Unassigned" or other Project swimlanes).
- The New task dialog has Team = `Business` and Project = `Marketing Project`.

**Fail if**

- The Board shows Tasks from other Projects or is empty.
- The Team or Project field is not pre-filled or is incorrect.

### TP-T9 — Create new Team from sidebar

**Free** · 2 min

*Proves the New team action in the sidebar opens a dialog, creates a Team folder, lists the Team,
and opens its Members page.*

**Steps**

1. In the sidebar Teams group, click the `New team` link (the `+` icon).
2. In the dialog, type `Research`.
3. Click `Create`.

**Pass if**

- The dialog accepts the Team name.
- A folder `Teams/Research/` is created.
- The Team appears in the sidebar Teams list.
- The Research Team's Members page opens showing "No members yet" and an `+ Add member` button.

**Fail if**

- The dialog fails or the name is rejected.
- The folder is not created.
- The Team does not appear in the sidebar.

### TP-T10 — Create new Project from Team actions menu

**Free** · 2 min

*Proves the Team actions menu (⋯ button on a Team row) includes New project, opens a dialog,
creates a Project folder, lists the Project under the Team, and opens the Project's Files page.*

**Steps**

1. In the sidebar Teams group, hover over Business to reveal the `⋯` button.
2. Click the `⋯` button (Team actions menu).
3. Click `New project` in the menu.
4. In the dialog, type `Q4 Launch`.
5. Click `Create`.

**Pass if**

- The Team actions menu appears only when hovering over the Team row.
- The menu contains `New project`.
- A folder `Teams/Business/Q4 Launch/` is created.
- The Project appears under Business in the sidebar.
- The Project's Files page opens.

**Fail if**

- The menu does not appear or `New project` is not in it.
- The folder is not created.
- The Project does not appear under Business.

### TP-T11 — Refuse project name "memory"

**Free** · 1 min

*Proves the new Project dialog validates the name and rejects "memory" as reserved for the Team's
shared Memory, showing an inline error.*

**Steps**

1. On Business page, hover over Business in the sidebar to reveal the `⋯` button.
2. Click the `⋯` button.
3. Click `New project`.
4. Type `memory` in the dialog field.

**Pass if**

- An error appears inline below the field reading exactly: "\"memory\" is reserved for the Team's shared Memory."
- The `Create` button is disabled while the error is shown.
- The dialog does not close until a different name is entered or Cancel is clicked.

**Fail if**

- The error message is missing, truncated or different.
- The Create button remains enabled.
- A project named "memory" is created.

### TP-T12 — Agent writes to Team Memory and appears in another member's next Turn

**Paid 💰** · 15 min · one Agent Turn, one Human Turn

*Proves the headline case: that a real Agent (Nova) writes a file to the Team's shared Memory
under `Teams/Business/Marketing Project/memory/`, and another member (Ada) sees it listed in
File Changes on her next Turn, categorized under Team Memory.*

**Steps**

1. Set Model and Effort to Haiku / low on Nova and Ada (the fixture provides them).
2. Enter a Room with Nova and Human, send one Turn: "Write a file to the Team's shared Memory called `launch-date.md` under the Marketing Project folder, with the content `Date: Q4 2026`."
3. Confirm that `Teams/Business/Marketing Project/memory/launch-date.md` is created with that content.
4. Enter a different Room with Ada and Human, send one Turn to Ada: "Hi there" or a greeting.
5. In Ada's reply, look for a File Changes section that lists the new launch-date.md as added by Nova.

**Pass if**

- `Teams/Business/Marketing Project/memory/launch-date.md` exists with content "Date: Q4 2026".
- Ada's next Turn shows File Changes that include an entry like "added …\memory\launch-date.md (by Nova)" or similar.
- Ada's next new session lists the file under *Team Memory › Business › Marketing Project* in the Watched Folders list.

**Fail if**

- The file is not created or has wrong content.
- Ada's next Turn does not mention the file in File Changes.
- Ada's session does not list Team Memory files.

**Real browser only** · Cannot mock Agent writes; requires live Adapter.

### TP-T13 — Agent on adapter with ReadsFiles=false has no Team Memory

**Free** · 1 min · read config

*Proves that a Teammate with `ReadsFiles: false` configured in the Adapter has no Team Memory
block in its system prompt.*

**Steps**

1. Create a Teammate with an Adapter configured `ReadsFiles: false` (or use the test fixture's mock).
2. In the Teammate's system prompt or agent context, search for Team Memory references.

**Pass if**

- The system prompt contains no Team Memory block (neither Team Memory nor personal Memory blocks).
- The Adapter's `ReadsFiles: false` setting prevents the block from being added.

**Fail if**

- A Team Memory block appears in the prompt.
- The Teammate can list or read Team Memory files.

### TP-T14 — Team exists with no members but has Members page

**Free** · 1 min

*Proves that a Team folder exists but no Teammate carries that Team label (orphaned Team), the
Team is listed in the sidebar with a "No members" muted hint, its Members page opens and works,
and Add member makes it active.*

**Steps**

1. Create a Team folder `Teams/Legal/` via the file system (or use fixture setup).
2. Do **not** add any Teammate with `teams: ["Legal"]`.
3. In the sidebar, verify `Legal` appears in the Teams list with a muted "No members" hint.
4. Click `Legal` in the sidebar.
5. On the Members page, click `+ Add member` and add a Teammate.

**Pass if**

- The Team is listed and clickable even with no members.
- The "No members" hint appears muted (gray text, not an error).
- The Members page opens and functions normally.
- After adding a member, the "No members" hint disappears and the member is listed.

**Fail if**

- The Team does not appear in the sidebar.
- The Members page shows an error instead of "No members yet".
- Adding a member fails.

### TP-T15 — Team with no folder yet creates folder on first save

**Free** · 2 min

*Proves that a Teammate carries a Team label (e.g., `teams: ["Ops"]`) but the Team folder does
not exist yet, the Team is listed in the sidebar, its Files tab shows an empty tree, and the
first save operation creates the folder.*

**Steps**

1. Create a Teammate with `teams: ["Ops"]` but **do not** create `Teams/Ops/` folder.
2. Click the sidebar `Ops` Team link.
3. Click the `Files` tab and observe the empty tree.
4. Click `+ New note` and create a file `ops-note.md`.
5. Confirm the file is saved.

**Pass if**

- The Team `Ops` is listed in the sidebar.
- The Files tab shows an empty tree (no error).
- The New note dialog opens and succeeds.
- After saving, `Teams/Ops/ops-note.md` exists and the folder `Teams/Ops/` was created by the save.

**Fail if**

- The Team does not appear in the sidebar.
- The Files tab shows an error.
- The folder is not created on save.

### TP-REAL-01 — Row menu button does not open Teammate card

**Real browser only** · 1 min · cannot automate

*Proves that clicking the ⋯ (row menu button) on a member row opens only the popup menu and does
**not** also open the Teammate card. This requires the button to have `@onclick:stopPropagation`
to prevent bubbling to the row's own click listener.*

**Steps**

1. On any Team's Members tab, hover over a member row to reveal the ⋯ button.
2. Click the ⋯ button (do not click elsewhere on the row).
3. Observe whether only the menu opens or whether both the menu and the Teammate card appear.

**Pass if**

- Only the popup menu appears.
- No Teammate card sidebar opens.

**Fail if**

- The Teammate card opens alongside the menu, indicating `@onclick:stopPropagation` is missing or broken.

### TP-REAL-02 — Dark theme and narrow-window styling

**Real browser only** · 2 min · cannot automate

*Proves the Teams sidebar and Team page components render correctly in dark theme and respect the
narrow-window (mobile) layout, using scoped CSS in `TeamsNav.razor.css` and `TeamMembers.razor.css`.*

**Steps**

1. In the Settings Appearance section, switch to a Dark theme (e.g. "Huddle Dark").
2. Observe the sidebar Teams group and the Team page in the browser.
3. Resize the browser window to narrow (< 600px width, or DevTools mobile view).
4. Observe the Members tab and the form layout.

**Pass if**

- The Teams sidebar group is readable in dark theme (appropriate contrast, no lost text).
- The Team page tabs and content are readable in dark theme.
- The narrow window layout does not break: lists wrap appropriately, buttons are clickable, and text does not overlap.
- No errors appear in the DevTools console related to the Teams components.

**Fail if**

- Text is unreadable in dark theme due to poor contrast.
- The narrow layout breaks (overlapping buttons, unclickable elements, squashed text).
- CSS errors appear in the console.
