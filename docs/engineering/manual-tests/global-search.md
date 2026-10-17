# Global Search: the box, the dropdown, filters and the jump to a Message

Prove, in a real browser against the running app, everything the Global Search box does: the three ways it opens, the browser keys it must win, the order and fill of the groups, the content hits and their cut-off, the jump to a Message in a Room, the dimmed Archived side, a locked folder, the keyboard path, the box's place in the Top Bar and the dropdown's height at the default 100% render. The automated suite covers the pure parser, the ranker and the providers against fakes; it cannot see the browser keys, the zoom, the fade or the screen reader, so each of those is checked here. The tests are all free: search reads files and the database and never calls an Agent.

**10 tests** · all free · about 2.25 hours.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, states and procedures named below.

## Setup

1. Run [`P-BUILD`](common.md#p-build), then [`P-LAUNCH-FREE`](common.md#p-launch-free) in `T-A` and wait for `Now listening on: http://localhost:5100`. No Adapter is needed.
2. Make the data the tests search for, once, before GLOBALSEARCH-01: one Team named `Marketing`; one Teammate named `Nova` in that Team; one Library file under the Teams root whose text contains the word `login` (for example `Teams/Marketing/plan.md`); one Task whose title contains `login`; one Room `findings` with a Message that contains `login`; one archived Room and one closed Task that also contain `login`. Record the Message's Room URL for GLOBALSEARCH-05.
3. Use the browser you will test in, at its normal zoom unless a test says otherwise. Never test through a private window.
4. A Library root that is pinned in the tree is needed for GLOBALSEARCH-03 and GLOBALSEARCH-07. Pin a folder with many files for GLOBALSEARCH-03 only.

## Tests

### GLOBALSEARCH-01 — Ctrl K, slash and a click open the box (GS-M1)

**Free** · about 10 min

*Proves the three ways to open the box work, that `/` never steals a slash typed in the composer, and that `Esc` gives focus back.*

**Before you start**

- The app is running and `http://localhost:5100` is open on a Room page.

**Steps**

1. Press `Ctrl+K`.
2. Read where the caret is and whether the dropdown is open. Press `Esc`.
3. Click on the page body outside the box. Press `/`.
4. Click the composer, type `/compact`, and read the composer.
5. Click the Room page's message list, press `/`, then read whether the box opened.
6. Click the box directly. Press `Esc`.
7. Click a Team's text in the left menu to move focus, then click the box again.

**Pass if — all of these**

- `Ctrl+K` focuses the box and opens the dropdown with the chips and the quiet line `Type 3 or more characters, or pick a filter.`
- `/` opens the box only when no text field has focus. In step 4 the composer shows `/compact` and the box does not open.
- In step 5, `/` on the message list opens the box.
- A click on the box always opens the dropdown.
- `Esc` closes the dropdown and focus returns to the element that had it before the box opened (step 2: the page body; step 6: the box's previous holder).

**Fail if — any of these**

- `/` opens the box while the composer has focus, or the composer loses the typed slash.
- `Esc` leaves the dropdown open or drops focus to the top of the page.

**Inconclusive if**

If the browser's own find bar takes `/` (Firefox quick find), record that and repeat from step 3 with the page focused by a click.

### GLOBALSEARCH-02 — Ctrl K reaches the app in Edge and not the address bar (GS-M2)

**Free** · about 10 min

*Proves the `Ctrl+K` handler's `preventDefault()` keeps Edge's address-bar search from taking the key. If it does not, the test records that and names the fallback, `Ctrl+Shift+K` (D-4).*

**Before you start**

- Edge is the browser under test. The app is open on a Room page.

**Steps**

1. Click in the Room's message list so the page has focus, not the address bar.
2. Press `Ctrl+K`.
3. Read whether the address bar gained focus, and whether the Global Search box did.
4. If the address bar took the key, press `Esc`, click the page, and press `Ctrl+Shift+K`. Record whether the box opened.

**Pass if — all of these**

- `Ctrl+K` puts the caret in the Global Search box and the address bar does not take focus.

**Fail if — any of these**

- `Ctrl+K` opens Edge's address-bar search and the box does not open. Record the result in [known-limits.md](../known-limits.md), name `Ctrl+Shift+K` as the working key if it works, and raise a Gitea issue.

**Inconclusive if**

If the Edge build under test is not the stable channel, record its version and rerun on the stable channel.

### GLOBALSEARCH-03 — Groups fill one by one on a large pinned root, and stale results never show (GS-M3)

**Free** · about 20 min

*Proves a group is drawn as soon as its result arrives, not all at once, that typing fast never leaves an old result on screen, and records how long a search takes on the largest real install (OP-18).*

**Before you start**

- A Library root with at least 2,000 files is pinned in the tree. Note its path and how many Transcripts the install holds.

**Steps**

1. Type `login` slowly, one character at a time, and watch the dropdown headings.
2. Clear the box. Type `loginx` quickly, then `login`, then `loginxy`, pausing for under one second between them.
3. Read the dropdown after the last keystroke settles.
4. Measure how long the search takes: note the time you type the last character and the time the last group's progress circle disappears (a stopwatch or a screen recording).

**Pass if — all of these**

- Each group's heading appears with a small progress circle, and its cards appear when its result arrives, not all at the end.
- After step 2 the dropdown shows results only for `loginxy`, never for `login` or `loginx`, once the search has settled.
- The elapsed time is recorded against the Transcript count in the test notes (OP-18).

**Fail if — any of these**

- A result for an earlier query is still on screen after the last keystroke settles.
- The dropdown stays on the progress circle with nothing below it after a search finishes.

**Inconclusive if**

If the pinned root is too small to take longer than a second, note the time and say so; the timing is the point, so the test is only conclusive on a large install.

### GLOBALSEARCH-04 — Content hits show the bold match, the 1 MB cut-off and the incomplete note (GS-M4)

**Free** · about 15 min

*Proves a Library file's contents are searched, the match is bold in its snippet, a file over the 1 MB limit matches by name only, and a limit that was hit says `Results may be incomplete.`*

**Before you start**

- A Library file under the Teams root contains `login` in its body (Setup step 2). Create a second file, `big.txt`, of more than 1 MB under the same root whose body contains `login`, and a third file named `loginname.md` whose body does not.

**Steps**

1. Type `login` in the box with the filter set to All.
2. Read the Library group.
3. Set the filter to Library by clicking its chip, and read the Library group again.
4. Type `loginname` and read the Library group.

**Pass if — all of these**

- The Library group shows `plan.md` with a one-line snippet in which the word `login` is bold.
- `big.txt` appears only if its name matches. Its contents are not searched, so a hit on `login` in its body does not appear.
- `loginname.md` appears by its name in step 4.
- When a limit was hit, `Results may be incomplete.` is drawn under the Library cards.

**Fail if — any of these**

- A file over 1 MB is read for its body, or a body match appears for `big.txt`.
- The match in a snippet is not bold, or the bold word differs from the typed word in case.

**Inconclusive if**

If no file in the root is over 1 MB, make one as in the Before list and rerun the test.

### GLOBALSEARCH-05 — A Room hit jumps to its Message (GS-M5)

**Free** · about 15 min

*Proves a Room hit opens the Room at the matching Message, centred and highlighted, both when the Room is closed and when it is already open, and that a new Message during the highlight does not move the view.*

**Before you start**

- The Room `findings` has many Messages, and one of them contains `login` far above the bottom. Note its text.
- The browser zoom is 100%.

**Steps**

1. Close the Room `findings` tab if it is open. Type `login` in the box, pick the Rooms chip, and press `Enter` on the Room hit.
2. Read the row that contains the matching Message: is it centred in the list, and is the highlight over it?
3. Wait for the highlight to finish, then press `Esc`.
4. Open the Room `findings` again by its tab, scroll to the bottom, then repeat steps 1 to 2 with `Ctrl+Enter`.
5. While the highlight is on, have an Agent or the pipe post a new Message into the Room (or type one yourself).

**Pass if — all of these**

- The matching row is centred in the list and the fading highlight covers it, in both the closed and the open Room.
- The Room's address bar does not keep `?message=` after the jump (it is dropped once the jump has happened).
- A new Message during the highlight is drawn but the view does not move.

**Fail if — any of these**

- The view stays at the bottom, or the row is at the edge of the list rather than centred.
- The new Message yanks the view to the bottom while the highlight is on.

**Inconclusive if**

If the Room has no Message above the fold, scroll so it is, and record the scroll position used.

### GLOBALSEARCH-06 — Archived Rooms and Closed Tasks come last and dimmed (GS-M6)

**Free** · about 10 min

*Proves the Archived side is a second, dimmed group after the active groups, and that it never shows under the wrong heading.*

**Before you start**

- Setup step 2's archived Room and closed Task exist and contain `login`.

**Steps**

1. Type `login` with the filter set to All.
2. Read the order of the headings from the top to the bottom.
3. Set the filter to Tasks and read the list. Set it to Rooms and read the list.

**Pass if — all of these**

- The headings appear in this order: Teams, Teammates, Library, Tasks, Closed Tasks, Rooms, Archived Rooms, with empty groups not drawn in All.
- The Closed Tasks and Archived Rooms cards are dimmed, and their group headings say so in words (the state does not rest on colour alone).
- In the Tasks filter, the closed Task appears under Closed Tasks. In the Rooms filter, the archived Room appears under Archived Rooms.

**Fail if — any of these**

- An archived card appears above an active one, or is drawn at full strength.

**Inconclusive if**

If there are no archived items, archive a Room and close a Task first, then rerun.

### GLOBALSEARCH-07 — A locked folder in a pinned root does not hide the other groups (GS-M7)

**Free** · about 15 min

*Proves a provider that fails shows its own message and the other groups still render.*

**Before you start**

- A folder inside a pinned root is locked for the account running the app (on Windows: deny read to the account in the folder's Security tab). Note the folder path, and unlock it at the end.

**Steps**

1. Type `login` with the filter set to All.
2. Read every group.
3. Unlock the folder, then rerun step 1.

**Pass if — all of these**

- The Library group shows `Couldn't search Library.`, or skips the locked folder silently (no `Results may be incomplete.`, spec §8.3) and shows the rest of the files, and the other groups render as normal.
- Once the folder is unlocked, the Library group shows its files again.

**Fail if — any of these**

- One failing provider blanks the whole dropdown, or the message is not in the Library group's place.

**Inconclusive if**

If the locked folder cannot be made, record that the test is not performed; do not mark it passed.

### GLOBALSEARCH-08 — The keyboard alone reaches every chip and card (GS-M8)

**Free** · about 15 min

*Proves a person who cannot use a mouse can reach every chip and card, and that a screen reader announces the highlighted card and the result count.*

**Before you start**

- A screen reader is running (Narrator, NVDA or JAWS). The search results are present for `login`.

**Steps**

1. Press `Ctrl+K`, type `login`.
2. Press `Ctrl+Right` and `Ctrl+Left` to move between the chips, then `Alt+4` to pick Library.
3. Press `Down` and `Up` to move through the cards, skipping headings.
4. Press `Ctrl+Enter` on a Team card, then `Esc`.
5. Read what the screen reader announces at each step, and the result count when the search finishes.

**Pass if — all of these**

- Every chip is reachable and `Alt+1` to `Alt+6` pick All, Teams, Teammates, Library, Tasks and Rooms.
- `Up` and `Down` skip the headings and reach every card.
- The screen reader announces the highlighted card's name and kind, and then the result count (`<n> results` or `1 result`).

**Fail if — any of these**

- A card cannot be reached by the keyboard, or the screen reader reads no count when the search ends.

**Inconclusive if**

If no screen reader is available, record the keyboard steps only and mark the announcement check not performed.

### GLOBALSEARCH-09 — The search box sits in the Top Bar beside the breadcrumb (GS-M9)

**Free** · about 10 min

*Proves the box is in the Top Bar's middle cell, in one pane and in a split, and that the breadcrumb still follows focus.*

**Before you start**

- Two Tabs are open and the window is wide enough for the Top Bar to show the box and the breadcrumb.

**Steps**

1. Look at the Top Bar. Read the breadcrumb on the left and the box in the middle.
2. Use the Tab right-click menu on one Tab and choose Move right to make two panes. Click into the right pane's page.
3. Read the breadcrumb again.

**Pass if — all of these**

- The box sits in the middle of the Top Bar, beside the breadcrumb, in one pane and in a split.
- The breadcrumb follows the focused pane's active Tab after the split.

**Fail if — any of these**

- The box overlaps the breadcrumb, or moves below the Top Bar in the split.

**Inconclusive if**

If the window cannot show both, resize it and record the width.

### GLOBALSEARCH-10 — The dropdown height and width (GS-M10)

**Free** · about 15 min

*Proves that at the default 100% render (no page zoom) the dropdown is anchored flush under the box at its left edge, with a long list its height is 80% of the window and no more, and it is no wider than a narrow window.*

**Before you start**

- The browser zoom is 100% and the window is at its normal width. Open the browser's developer tools (F12) on the app page, Console tab.

**Steps**

1. Type `login` with the filter set to All. Wait for the search to finish.
2. In the Console, run `document.querySelector('.search-dropdown').getBoundingClientRect().height` and `0.8 * window.innerHeight`.
3. Read the two numbers. They should be equal within a few pixels.
4. Narrow the window to about 400 px wide. In the Console, run `document.querySelector('.search-dropdown').getBoundingClientRect().width` and `window.innerWidth`.
5. Scroll the list inside the dropdown with the mouse wheel and check the rest of the page does not scroll with it.
6. Check the dropdown's top edge against the bottom edge of the box and its left edge against the box's left edge.
7. Repeat steps 1 to 6 in the other browser (Chrome or Edge).

**Pass if — all of these**

- The dropdown's height equals `0.8 * window.innerHeight` within a few pixels, and never more, when the list is long.
- The dropdown's width is no more than `window.innerWidth`.
- The list scrolls inside the dropdown.
- The dropdown sits flush under the box at its left edge.

**Fail if — any of these**

- The height is above 80% of the window, or the dropdown runs off the right edge on a narrow window.
- The list does not scroll and the dropdown overflows the window.

**Inconclusive if**

If the search returns fewer results than fill 80% of the window, record the heights and the result count, and rerun with the `team:` or `lib:` filter to get a longer list.
