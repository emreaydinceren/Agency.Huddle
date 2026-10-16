# References in a Message: the @ picker, Copy link and the Referenced items header

Prove, in a real browser against the running app, what the Reference picker writes, how a reference renders and resolves, what an Agent is told, and how the picker behaves at the default 100% render and on a narrow window. The automated suite covers the pure text functions, the providers and the prompt against fakes; it cannot see the caret, the zoom, the keyboard, the screen reader or the wire, so each of those is checked here.

**12 tests** · all free · about 2.1 hours.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, states and procedures named below.

## Setup

1. Run [`P-BUILD`](common.md#p-build), then [`P-LAUNCH-FREE`](common.md#p-launch-free) in `T-A` and wait for `Now listening on: http://localhost:5100`. No Adapter is needed except for ATTAG-07.
2. Make the data the tests reference, once, before ATTAG-01: a Team named `Marketing` with a Team Room; a root Room named `Nova` and a Room named `Nova` scoped to `Marketing`; a Room named `findings` with the Message `login fails`; a Library file named `plan.md` under the Teams root; a Task with a visible id. Use two Teammates, `Nova` and `Emily Lee`, both Members of a three-member Room.
3. Use the browser you will test in, at its normal zoom unless a test says otherwise. Never test through a private window.

## Tests

### ATTAG-01 — The picker opens on an at sign at a word start (AT-M1)

**Free** · about 10 min

*Proves `@` opens the picker at the start of the text and after a space or an opening parenthesis, that an address such as `me@example.com` does not open it, and that `#` still opens Tasks only.*

**Steps**

1. Click the composer of *findings*. Type `@` at the start and read whether the picker opens.
2. Type `x`, then a space, then `@`. Read the picker.
3. Clear the text. Type `me@example.com` and then `x@@y`. Read the picker after each.
4. Clear the text. Type `#` and read the list.

**Pass if**

- Steps 1 and 2 open the picker. Step 3 does not open it in either address.
- Step 4 lists Tasks only, and no Teammate, Room or Library row.

**Fail if**

- The picker opens inside an address, or `#` lists anything but Tasks.

### ATTAG-02 — Picking a Teammate writes its Name and wakes only that Teammate (AT-M2)

**Free** · about 10 min

*Proves a pick writes the Teammate's Name, a multi-word Name and a search by Alias both work, the picked Teammate replies and the others do not, and a non-Member is dimmed and not woken.*

**Steps**

1. In *findings*, type `@Emily` and pick `Emily Lee`. Read the composer.
2. Type `@Ni` and search by the Alias of `Nova`. Pick it.
3. Send a Message that mentions one Teammate. Read which Teammates reply.
4. Open the picker in a Room that does not hold `Nova` and read its row.

**Pass if**

- Step 1 writes `@Emily Lee ` with a trailing space. Step 2 writes the Name, not the Alias typed.
- Only the picked Teammate replies in step 3.
- Step 4 shows the non-Member dimmed with `Not in this Room` and it is not woken by a send.

**Fail if**

- The picker writes the Alias, or a Teammate that was not picked replies.

### ATTAG-03 — A Task pick writes the plain id and the hash key gives the same text (AT-M3)

**Free** · about 10 min

*Proves a Task pick writes the plain id, that the id links in the posted Message, and that `#` produces the same text.*

**Steps**

1. In *findings*, type `#` and pick a Task. Read the composer.
2. Send the Message and read the rendered line.
3. Type `#` and pick the same Task again in a new Message. Compare the two texts.

**Pass if**

- The composer holds the plain id followed by one space, with no brackets.
- The posted Message shows the id as a Task link with its status dot.
- Both Messages hold the same text.

**Fail if**

- The id is written as a link, or the two Messages differ.

### ATTAG-04 — Room and Team picks write links and labels tell same-named Rooms apart (AT-M4)

**Free** · about 10 min

*Proves a Room pick and a Team pick write a link that opens the Room or the Team page, a Room named with an at sign writes a full-width at sign and Mentions nobody, and the two Rooms named Nova are told apart by their labels.*

**Steps**

1. Type `@Marketing` and read the rows. Pick the Team's row and read the composer.
2. Type `@Nova` and read the two Room rows, which carry their scope in their notes. Pick the Marketing one.
3. Create a Room named `Q@A`. Pick it from another Room and send.
4. Pick the Team Room under `Marketing` and send.
5. Click each written link.

**Pass if**

- Step 1 lists the Team and its Team Room as two rows and writes `[Marketing] (/teams/Marketing)`.
- Step 2 writes `[Nova (Marketing)] (/rooms/...)`, and the root Room writes `[Nova] (/rooms/...)`.
- Step 3 writes `Q＠A` with a full-width at sign and Mentions nobody.
- Step 4 writes `[Marketing Team Room] (/rooms/...)`, and each link opens its Room or Team page in the Tab.

**Fail if**

- A link opens the wrong Room, or a Room named `Q@A` wakes an Agent.

### ATTAG-05 — A Library pick writes a code span path and the upload line matches (AT-M5)

**Free** · about 10 min

*Proves a Library pick writes the path in a code span that is the same text an upload sends, that the Agent lists the file once under the Library documents, that a pasted screenshot opens no picker, and that a deleted Room shows the unavailable chip.*

**Steps**

1. In Room A, attach `plan.md` with the paperclip and send. Read the Message's path line.
2. In Room B, type `@lib:plan` and pick the file. Send. Read the path line.
3. Paste a screenshot into Room B's composer. Read whether a picker opens.
4. Delete Room A from the sidebar. Reopen Room B and read the first chip.

**Pass if**

- Both path lines are the identical code span holding the absolute path.
- Step 3 adds an upload chip and no picker opens.
- Step 4 shows the reference as `(unavailable)`.

**Fail if**

- The two path lines differ, or a picker opens on paste.

### ATTAG-06 — Renames and deletes show the chips and an old Transcript opens unchanged (AT-M6)

**Free** · about 10 min

*Proves the snapshot shows what the Message said, the chips follow the renames and deletes as the design states, and an old Transcript opens unchanged.*

**Steps**

1. Note the Room link in a Message that references *findings*, and a Task.
2. Rename *findings*. Retitle the Task. Delete another Room and a Task file.
3. Reload the page and read each chip.
4. Open a Transcript written before this feature, if you have one.

**Pass if**

- The renamed Room shows the changed chip, the retitled Task shows its new title, the deleted items show `(unavailable)`.
- The old Transcript opens with its Messages as they were, and no error appears.

**Fail if**

- A Message's text changes, or an old Transcript fails to open.

### ATTAG-07 — The Agent prompt carries the Referenced items header (AT-M7)

**Free** · about 15 min

*Proves that with the `mock-acp` Adapter and the wire trace on, the prompt carries the Referenced items header, that it is fresh after a retitle, that a Catch-up reference does not add it, and that `Team:References:ExpandInPrompt` set to `false` removes it.*

**Setup**

1. Give the Teammate the Adapter named `mock-acp`, then set the log key `Logging:LogLevel:Agency.Huddle.Acp.Wire` to `Trace` and restart the app. Read the console for the wire lines.

**Steps**

1. In *findings*, send a Message with a Task reference. Find the prompt line in the console.
2. Retitle the Task and send again. Find the new prompt line.
3. Send a Message that references a Room only after a Catch-up.
4. Set `Team:References:ExpandInPrompt` to `false`, restart, send a Message with a reference, and read the prompt.

**Pass if**

- Step 1 shows the header as it is written in the design, fenced as data.
- Step 2 shows the new title, not the old one.
- Step 3 shows no header, and step 4 shows none.

**Fail if**

- The header holds a title from before the retitle, or step 4 still shows it.

### ATTAG-08 — Keyboard alone picks, sends nothing while open and announces the count (AT-M8)

**Free** · about 10 min

*Proves the picker works from the keyboard and that a screen reader hears the count and the highlighted row.*

**Steps**

1. Type `@` and press Down, Down, then Up. Read the highlighted row each time.
2. Press Up on the first row, then Down on the last. Read the highlight.
3. Press Enter to pick, then Tab on another row to pick it.
4. Type `@`, then press Enter on the list with no pick. Read whether the Message was sent.
5. Type `@`, press Esc, and read whether the picker reopens.
6. Type `@`, click the Send button. Read what is sent.
7. Turn on a screen reader and move through the list.

**Pass if**

- Up and Down wrap at both ends. Enter and Tab pick.
- Enter never sends while the picker is open.
- Esc keeps the text and does not reopen the picker.
- The Send button sends the literal token.
- The screen reader announces the count and the highlighted row.

**Fail if**

- A Message is sent while the picker is open, or the count is not announced.

### ATTAG-09 — The picker height and a narrow window (AT-M9)

**Free** · about 10 min

*Proves the picker opens above the composer, fits the height rule (the smaller of 320 px and 40% of the window, plain `vh`, no page zoom), scrolls inside itself and never overflows sideways on a 400 px window. Chrome and Edge.*

**Steps**

1. Leave the browser zoom at 100% (the app applies no zoom of its own, ADR-0056). Type `@` with a long list and open DevTools.
2. In the console, run `document.querySelector('.composer-picker').getBoundingClientRect().height` and `Math.min(320, 0.4 * window.innerHeight)`.
3. Compare the two numbers.
4. Narrow the window to 400 px and read the picker's right edge.

**Pass if**

- The measured height equals the computed minimum within a few pixels when the list is long.
- The list scrolls inside the picker.
- On 400 px the picker does not overflow sideways.

**Fail if**

- The measured height is larger than the minimum, or the picker is wider than the window.

### ATTAG-10 — Undo after a pick restores the typed token (AT-M10)

**Free** · about 10 min

*Proves a pick is one undoable edit, and that a reference deleted before send leaves nothing in the snapshot.*

**Steps**

1. Type `@nov`, pick `Nova`, then press Ctrl+Z.
2. Pick a Task, delete its text with Backspace, then send the Message.
3. Read the Message's references in the Transcript.

**Pass if**

- Step 1 restores the typed token `@nov`.
- Step 3 shows no reference for the deleted Task.

**Fail if**

- Ctrl+Z removes more than the pick, or a deleted reference is still shown.

### ATTAG-11 — The picker lists Rooms and Library files by name only (AT-M11)

**Free** · about 10 min

*Proves the picker matches Room and Library names and never the text inside a Message or a file.*

**Steps**

1. In *findings*, post `login fails`. In a Room named *login-review*, post nothing.
2. Type `@room:login` and read the rows.
3. Pick the *login-review* row and read the composer.
4. Type `@room:findings` and read the rows.
5. Type `@lib:login` where a Library file's text holds `login` but whose name does not.
6. Use the Top Bar search for `login`.

**Pass if**

- Step 2 lists *login-review* only, as a Room row with no count and no snippet.
- Step 3 writes `[login-review] (/rooms/...)` with no `?message=`.
- Step 4 lists *findings*.
- Step 5 does not list the file whose text holds `login`.
- Step 6 still shows *findings* with its Message match.

**Fail if**

- A Room is listed because of a Message's text, or a file is listed by its contents.

### ATTAG-12 — Copy link from the header, the sidebar and a Team Room (AT-M12)

**Free** · about 10 min

*Proves Copy link works from the Room header, from a sidebar row by its menu and by right-click, and from a Team Room under its Team, and that it still copies when the app is opened over a LAN address.*

**Steps**

1. Open *findings*. Click the link button in the Room header. Read the snackbar.
2. Open the sidebar row's `…` menu and choose Copy link. Then right-click the row and choose Copy link.
3. Open the Team Room under `Marketing` and copy its link from its row.
4. Paste into another Room's composer and send. Read the rendered Message.
5. Rename *findings*, reopen the other Room, and read the chip.
6. Open the app over a LAN address and copy a link again.
7. Paste the address bar's URL into a composer.

**Pass if**

- Each copy shows `Copied link to <Name>`.
- The pasted text is `[findings] (/rooms/...)`, and it renders as a link that opens *findings*.
- The renamed chip shows the changed name.
- Step 6 copies, or shows `Couldn't copy the link.`
- Step 7 opens the Room but is not a reference: no chip, and no reference row in the Message.

**Fail if**

- The pasted text is not the Markdown reference, or the address-bar URL is taken for a reference.
