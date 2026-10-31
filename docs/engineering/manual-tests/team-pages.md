# Teams: Sidebar, Members, Files, Tasks and Shared Memory

Prove the Teams feature end to end in a real browser: that the sidebar lists Teams and Projects
with the right actions, that Members, Files and Tasks tabs navigate and function correctly, that
creating Teams and Projects works with the right validation, and — the one case this page exists
to answer — whether a file in a Team's shared Memory surfaces in another member's next Turn.

**15 tests** · 15 free, 0 paid · about 0.7 hours (plus about 15 minutes to build the fixture in Setup) · **none of them has been run yet.** This page documents what to
run, not a result — see [Known limits](../known-limits.md) for what that leaves unverified today.
TP-T13 was removed: no free oracle can read a Persona's system prompt (see Setup).

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

Background: [Huddle.TeamPages-Specifications.md](../../Huddle.TeamPages-Specifications.md) (§2
the Use cases T0–T15), the Project Plan (D7 delivery, removed after delivery and in the git history),
and the routing and component layout in `src/Huddle.App/Components/Teams/` and
`src/Huddle.App/Components/Pages/TeamPage.razor`.

## Setup

Every test on this page is free. Run [`P-BUILD`](common.md#p-build), then
[`P-LAUNCH-UAT`](common.md#p-launch-uat): it gives the page **Ada** and **Bo**, two mock-adapter
Agents in no Team (data folder `<data>` = `C:\seeds\huddle\uat\data`). Nothing on this page
spends tokens. The tests share one fixture and run in order: TP-T1 adds Bo to Business and TP-T2
removes Ada, and later tests rely on both.

**Build the fixture once, through the UI** (no Team, Project or Task is seeded; the
`software-co` seeder scenario uses other names):

1. Create `Nova` with [`P-NEW-PERSONA`](common.md#p-new-persona): Title `VP Product`, Alias `nova`,
   **Teams** = `Business`. Saving it creates the folder `<data>\Teams\Business\` (the Teams
   provisioner, `TeamFolderProvisioner.cs:10-12`).
2. Open **Agents** → **Ada** → **Edit**, type `Business` in **Teams** and click **Save**. Ada's
   Title stays `Test Agent`. Leave Bo with no Team.
3. In the sidebar Teams group hover `Business`, click its `Team actions` button (⋯), then `New project`, type `Marketing Project` and click `Create`.
4. On the Marketing Project page open **Tasks** → `New task`, set Title `Seed task` (Team
   `Business` and Project `Marketing Project` are pre-filled) and save it. Then go to
   Business → **Tasks** → `New task`, set Title `Loose task`, clear **Project** and save it.
5. On Business → **Files** click `New note` and name it `brief-notes`.

Confirm the sidebar **Teams** group sits between the Rooms list and the main menu, is labelled
`Teams`, and shows the Groups icon on its header; the `+` icon belongs to the link at the
bottom of the group, which is **Build a team** (`TeamsNav.razor`).

**Why TP-T9 is gone.** It created a Team from the sidebar's **New team** link, which was removed
together with **New team from a type**; a Team is now made with **Build a team**, which has its own
tests. The Team a fixture needs can be made by creating its folder under `<data>\Teams`.

**Why TP-T3 and TP-REAL-01 are gone.** The Members tab is now Build a team's Hire section for
the Team (role cards and a roster). It has no search box and no member row menu, so there is
nothing to search (TP-T3) and no row click to keep the menu from (TP-REAL-01).

**Why TP-T13 is gone.** It asked whether a Persona on an Adapter with `ReadsFiles=false` gets no
Team Memory block in its system prompt. Nothing in the UI shows a system prompt, and the mock
adapter echoes the Turn prompt, not the system prompt, so no free manual oracle exists. The
`ReadsFiles` gate is pinned by the automated suite (`AdapterCatalogTests`); the paid half of the
Team Memory story is listed under Known limits.

## Tests

### TP-T0 — Click Business team and open Members tab

**Free** · about 1 min

*Proves a Team link in the sidebar opens the Team page and shows the Members tab by default,
with the Team row highlighted and a list of members with avatars, names, titles and aliases.*

**Steps**

1. Verify `<data>\Teams\Business\` exists and that Nova and Ada are the Agents whose **Teams**
   field holds `Business` (the Setup fixture).
2. In the sidebar Teams group, click `Business`.

**Pass if**

- The URL is `/teams/Business`.
- The Members tab is active (first tab).
- The members list shows Nova and Ada, each with a status dot, an avatar, the Name and a second line
  `Title · Alias` (Nova: `VP Product`, `nova`; Ada: `Test Agent` and her alias).
- The `Business` Team link in the sidebar is highlighted (selected state).

**Fail if**

- The page does not load or shows an error.
- Members are listed incorrectly or are missing.
- The sidebar selection is not visible.

### TP-T1 — Members tab shows the roster with status and Message

**Free** · about 2 min

*Proves the Members tab is Build a team's Hire section for the Team: the role cards, then the
roster with each member's presence badge on the avatar, the *Member* meta text, Edit, Message (for a member that has
connected) and a Remove menu on one row of actions; and that an Agent joins through its `teams:` line.*

**Steps**

1. Open **Agents** → **Bo** → **Edit**, add `Business` to the `teams:` line of the text and
   click **Save**. Wait until Bo is **Online** again.
2. Open Business, then the **Members** tab.
3. Click **Remove** on Bo's card and look at the menu, then press Escape.

**Pass if**

- The tab shows the role cards (**Roles**, *Available roles*) and, under them, **Roster** with
  Ada, Bo and Nova, each with a presence badge on the 40px avatar (no status dot beside the Name) and *Member* as meta text.
- Each member has **Edit** and **Message**; the Remove menu has **Remove from team** and
  **Delete agent**.
- There is no **Save changes** bar yet: the save bar (sticky at the bottom of the pane) appears only once something is changed.
- There is no Add member button and no search box.

**Fail if**

- The tab still shows the old member list, or Bo is missing from the roster.
- A member has no presence badge on its avatar, or Message is missing for an Online member.

### TP-T2 — Remove from team keeps the Agent

**Free** · about 2 min

*Proves Remove from team marks a member until Save changes, then takes away only the Team label:
the Agent stays.*

**Steps**

1. On Business Members tab, click **Remove** on Ada's card, then **Remove from team**.
2. Read the card, then click **Save changes**.

**Pass if**

- Ada's card reads "Will leave the Team when you save", her Name is struck through, and it has
  **Undo**; the roster summary reads "3 members · 1 leaving".
- Saving asks nothing (nobody is deleted), and Ada then leaves the roster.
- Ada is still on the **Agents** page, and her `<data>\Agents\Ada\Ada.md` `teams` field no
  longer includes `Business`.

**Fail if**

- Ada is deleted as an Agent, or still lists `Business` after saving.
- Anything changes before **Save changes** is clicked.

### TP-T4 — Open Files tab and create new note

**Free** · about 2 min

*Proves the Files tab shows a folder tree and the New note button opens the Library's New note
dialog, creating a file in the Team's folder.*

**Steps**

1. On Business page (any tab), click the `Files` tab.
2. Verify a folder tree appears with the Team's files and folders.
3. Click the `New note` button.
4. Name the note `team-note` and confirm.

**Pass if**

- The Files tab shows a tree of folders and files under `<data>\Teams\Business\` (at least `brief-notes` and the `Marketing Project` folder).
- The New note dialog opens (the Library's standard dialog).
- A file `team-note.md` is created under `<data>\Teams\Business\` and appears in the tree.

**Fail if**

- The Files tab does not load or appears empty with no tree.
- The New note button fails or the dialog does not open.
- The file is not created or appears in the wrong folder.

### TP-T5 — Search files by name

**Free** · about 2 min

*Proves the search box in the Files tab replaces the tree with a flat list of matching files and
folders, each with its folder path, and clearing the search restores the tree.*

**Steps**

1. On Business Files tab, locate the search box labelled "Search files".
2. Type `brief`.
3. Observe the list that replaces the tree.
4. Clear the search box.

**Pass if**

- The tree is replaced by a flat list of files and folders whose names contain "brief" (the Setup note `brief-notes`).
- Each result shows its name and, beside it, the folder path relative to the Team's folder with `/` separators and without the file name (`LibraryExplorer.razor:106,307-315`). `brief-notes` sits directly under Business, so its path is empty.
- Clearing the search restores the original folder tree as it was.

**Fail if**

- The search does not filter or the tree is not replaced.
- The search is case-sensitive or does not find partial matches.
- The tree is not restored after clearing.

### TP-T6 — Open Project page scoped to project folder

**Free** · about 1 min

*Proves clicking a Project in the sidebar navigates to the Project's page with the URL scoped to
the Project, showing the Files tab scoped to the Project's folder, with no Members tab.*

**Steps**

1. In the sidebar Teams group, expand Business (click the chevron `Expand Business` if collapsed).
2. Click the `Marketing Project` project link under Business.

**Pass if**

- The URL is `/teams/Business/projects/Marketing%20Project`.
- The Files tab is shown by default.
- The file tree is scoped to `<data>\Teams\Business\Marketing Project\` (the `brief-notes` and `team-note.md` files from Business are not shown).
- The Members tab is not present.
- The Files and Tasks tabs are present.

**Fail if**

- The URL is incorrect or the page does not load.
- The Files tab shows files outside the Project folder.
- The Members tab is visible.

### TP-T7 — Open Tasks tab on Team and create new task

**Free** · about 2 min

*Proves the Tasks tab shows a Board with swimlanes by Project, including Tasks with no Project,
and the New task button opens the Task dialog with Team defaulted.*

**Steps**

1. On Business page, click the `Tasks` tab (switch to the Board if it opens as a List).
2. Verify the Board displays one swimlane per Project: `Business / Marketing Project` and `No project`.
3. Click the `New task` button.
4. In the Task dialog, verify the **Team** field defaults to `Business` and note the dialog structure.
5. Cancel the dialog.

**Pass if**

- The Tasks tab shows a Board with swimlanes grouped by Project, labelled `Business / Marketing Project` (holding `Seed task`) and `No project` (holding `Loose task`) (`TaskQuery.cs:228-237`).
- All Tasks in Business (including those in Projects) are visible across swimlanes.
- The New task dialog opens with Team pre-filled as `Business`.

**Fail if**

- The Tasks tab does not load or the Board is empty.
- The Team field is not pre-filled or shows a different Team.
- The New task button fails.

### TP-T8 — Open Tasks tab on Project and create new task

**Free** · about 2 min

*Proves the Tasks tab on a Project page shows only that Project's Tasks, and the New task button
defaults the Team and Project correctly.*

**Steps**

1. On Business › Marketing Project page, click the `Tasks` tab (if not already active).
2. Verify the Board shows only Marketing Project Tasks (`Seed task`, not `Loose task`).
3. Click the `New task` button.
4. In the Task dialog, verify both Team and Project fields: Team = `Business`, Project = `Marketing Project`.
5. Cancel the dialog.

**Pass if**

- The Board shows only Marketing Project Tasks (no `No project` swimlane and no `Loose task`).
- The New task dialog has Team = `Business` and Project = `Marketing Project`.

**Fail if**

- The Board shows Tasks from other Projects or is empty.
- The Team or Project field is not pre-filled or is incorrect.

### TP-T10 — Create new Project from Team actions menu

**Free** · about 2 min

*Proves the Team actions menu (⋯ button on a Team row) includes New project, opens a dialog,
creates a Project folder, lists the Project under the Team, and opens the Project's Files page.*

**Steps**

1. In the sidebar Teams group, hover over Business to reveal the `Team actions` (⋯) button.
2. Click the `⋯` button.
3. Click `New project` in the menu.
4. In the dialog, type `Q4 Launch` in the **Project name** field.
5. Click `Create`.

**Pass if**

- The Team actions button is revealed by hovering the Team row or by keyboard focus (it stays in the page; `app.css:221-230`).
- The menu contains `New project`.
- A folder `<data>\Teams\Business\Q4 Launch\` is created.
- The Project appears under Business in the sidebar.
- The Project's Files page opens.

**Fail if**

- The menu does not appear or `New project` is not in it.
- The folder is not created.
- The Project does not appear under Business.

### TP-T11 — Refuse project name "memory"

**Free** · about 1 min

*Proves the new Project dialog validates the name and rejects "memory" as reserved for the Team's
shared Memory, showing an inline error.*

**Steps**

1. In the sidebar Teams group, hover over Business to reveal the `Team actions` (⋯) button.
2. Click the `⋯` button.
3. Click `New project`.
4. Type `memory` in the **Project name** field.

**Pass if**

- An error appears inline below the field reading exactly: "\"memory\" is reserved for the Team's shared Memory." (shown as `"memory" is reserved for the Team's shared Memory.`; `TeamNames.cs:13`).
- The `Create` button is disabled while the error is shown.
- The dialog does not close until a different name is entered or Cancel is clicked.

**Fail if**

- The error message is missing, truncated or different.
- The Create button remains enabled.
- A project named "memory" is created.

### TP-T12 — Team Memory file appears in another member's next Turn

**Free** · about 6 min

*Proves the headline case on the free lane: a file written into a Team's shared Memory folder is
listed in the File Changes block of a member's next Turn. Bo is a member of Business after TP-T1;
the mock adapter echoes the whole Turn prompt, so the block is readable in Bo's reply. (The File
Changes list is Turn prompt text, not UI — ADR-0023.)*

**Steps**

1. Open Bo's Room (**Agents** → **Bo** → `Message`) and send `first`. Wait for the reply; this fixes Bo's baseline.
2. Create the folder `<data>\Teams\Business\memory\` if it is missing, and in it a file `launch-date.md` whose content is `Date: Q4 2026` (a text editor, [`O-FILE`](common.md#o-file)). The Human writes it; nothing here spawns a real Agent.
3. Send `second` in Bo's Room and wait for the reply.
4. Read the reply, which is the echoed prompt.

**Pass if**

- `<data>\Teams\Business\memory\launch-date.md` exists with content `Date: Q4 2026`.
- The reply to `second` contains the header "Since your last Turn in this Room, these files changed." followed by a line `added <data>\Teams\Business\memory\launch-date.md` (`prompts.default.json` `turn.fileChangesHeader`, `turn.fileAdded`), with no "by" suffix because the Human, not Bo, wrote it.
- The reply to `first` has no such block for that file.

**Fail if**

- The reply to `second` has no File Changes block, or the block omits `launch-date.md`.
- The block appears in the reply to `first`.

**Inconclusive if** Bo is not Online, or Bo's reply to `first` never arrived. The paid half (a real Agent
writing the file itself with its own tools, judged by the model) is not covered by any test on this page.

### TP-T14 — Team exists with no members but has Members page

**Free** · about 2 min

*Proves that a Team folder with no Agent carrying its label (an orphaned Team) is listed in the
sidebar with a count of `0` (its tooltip reads "No members"), its Members page opens and works, and hiring a draft
there makes it active.*

**Steps**

1. Create the folder `<data>\Teams\Legal\` in a file explorer. Add no Agent to it.
2. In the sidebar, verify `Legal` appears in the Teams list with a trailing count of `0`; hover it and the tooltip reads "No members" (wait a few seconds; reload the page if it has not appeared).
3. Click `Legal` in the sidebar.
4. On the Members page, click **Add** on any role card, then **Save changes**.

**Pass if**

- The Team is listed and clickable even with no members.
- The count beside `Legal` is `0` in plain caption text, its tooltip reads "No members", and it is not an error.
- The Members page opens with the role cards and an empty roster ("No Agents yet. Add a role above.") before the add.
- After saving, the count rises to `1` and the tooltip no longer reads "No members", and the new Agent is on the roster as a member.

**Fail if**

- The Team does not appear in the sidebar.
- The Members page shows an error instead of the empty roster.
- Saving fails.

### TP-T15 — Team with no folder yet creates folder on first save

**Free** · about 3 min

*Proves that an Agent carries a Team label (`Ops`) whose folder does not exist, the Team is still
listed in the sidebar, its Files tab shows an empty tree, and the first save creates the folder.
The Teams provisioner makes the folder the moment the Agent is saved
(`TeamFolderProvisioner.cs:10-12`), so the test removes it by hand first.*

**Steps**

1. Create `Ivy` with [`P-NEW-PERSONA`](common.md#p-new-persona): Teams = `Ops`.
2. Delete the folder `<data>\Teams\Ops\` by hand (the app keeps running; do not save any Agent afterwards, since that re-creates the folder).
3. Click the sidebar `Ops` Team link, then the `Files` tab, and observe the empty tree.
4. Click `New note` and create a file `ops-note`.
5. Confirm the file is saved.

**Pass if**

- The Team `Ops` is listed in the sidebar.
- The Files tab shows an empty tree (no error).
- The New note dialog opens and succeeds.
- After saving, `<data>\Teams\Ops\ops-note.md` exists and the folder `<data>\Teams\Ops\` exists again.

**Fail if**

- The Team does not appear in the sidebar.
- The Files tab shows an error.
- The folder is not created on save.

### TP-T16 — Details tab saves the purpose; the Members tab hires and deletes

**Free** · about 4 min

*Proves that the **Details** tab shows Build a team's type and details for an existing Team with
its type and name fixed and no Hire step, and writes "What it's for" to `_team.md`; and that the Members tab hires a new
draft and deletes a removed member (the Agent, not only the label).*

**Steps**

1. Open **Build a team**, choose **Blank Team**, name it `Garden`, add two roles from the role
   cards and select **Approve & hire**. Note the two Names.
2. On the Garden Team page, open the last tab, **Details**. Confirm the header has no **Edit team** button.
3. Confirm the tab has no steps and no role cards, shows only the Blank Team card, and the Team name
   field cannot be typed in.
4. Change **What it's for** to `Grows vegetables.` and click **Save changes**.
5. On the Garden **Members** tab, add any role from the role cards, then click **Remove** on the
   first of the two members and choose **Delete agent**.
6. Click **Save changes**, then **Delete** in the confirmation.

**Pass if**

- After step 4 the Details tab stays open with no save bar showing and `<data>\Teams\Garden\_team.md` has `owns: Grows vegetables.`.
- The roster showed the two members with *Member* meta text and the new draft with *New* meta text,
  and the removed member's card said "Will be deleted when you save" with an **Undo** button.
- The confirmation named the removed member.
- After saving, the roster lists the second member and the new Agent as members, and the
  removed member is gone from the **Agents** page.

**Fail if**

- The type cards or the Team name can be changed, or the Details tab shows role cards.
- The removed member is still on the Agents page, or only lost the `Garden` label.
- The new draft was not hired, or a member was hired a second time.

### TP-REAL-02 — Dark theme and narrow-window styling

**Real browser only** · about 2 min · cannot automate

*Proves the Teams sidebar and Team page components render correctly in dark theme and stay usable in
a narrow window, using scoped CSS in `TeamsNav.razor.css` and the roster rules in `app.css`. The drawer is
persistent at 240 px (`MainLayout.razor:17`), so a narrow window shrinks the page content, not the sidebar.*

**Steps**

1. In the Settings Appearance section, switch to a Dark theme (e.g. "Huddle Dark").
2. Observe the sidebar Teams group and the Team page in the browser.
3. Resize the browser window to narrow (< 600px width, or DevTools mobile view).
4. Observe the Members tab: the role cards and the roster cards.

**Pass if**

- The Teams sidebar group is readable in dark theme (appropriate contrast, no lost text).
- The Team page tabs and content are readable in dark theme.
- The narrow window layout does not break: lists wrap appropriately, buttons are clickable, and text does not overlap.
- No errors appear in the DevTools console related to the Teams components.

**Fail if**

- Text is unreadable in dark theme due to poor contrast.
- The narrow layout breaks (overlapping buttons, unclickable elements, squashed text).
- CSS errors appear in the console.
