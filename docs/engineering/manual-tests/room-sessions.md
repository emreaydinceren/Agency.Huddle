# Room Sessions: per-Room context, resume and Stop

**12 tests** · 0 free, 12 paid 💰 · about 5.7 hours · **none of them has been run yet.** This page
documents what to run, not a result — see [Known limits](../known-limits.md) for what that leaves
unverified today.

Prove, in a real browser, that a Room Session actually holds one Room's context and nothing
else: that a long, interleaved run across two Rooms never resolves a terse follow-up against
the wrong one, that a preference only crosses Rooms through Memory, that Stop in one Room
leaves another's Turn running, that an idle Room Session resumes with its memory intact, that
a shared-session Adapter Profile still behaves exactly as it did before Room Sessions, and the
two open questions Appendix B leaves for a real Adapter to answer: whether `session/resume`
survives a rename (RS-V3), and whether `agency-acp` holds several sessions per process at all
(RS-V5). None of this has a mock-adapter substitute that means anything — the whole point is what
a real model does with 20+ interleaved Turns, and the mock adapter's process cannot resume a
session its predecessor opened — which is why every test here spends money and none of it is in
the automated suite.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

Background: [Huddle.RoomSessions-Specifications.md](../../Huddle.RoomSessions-Specifications.md)
§2 (the use cases these tests prove), §6.5 (Catch-up), §6.8 (Stop, the streak, the Budget), §6.9
(the system-prompt texts), §6.12 (`agency-acp`), §10 (the RS-M table itself) and Appendix B (its
V-3 and V-5, which this page numbers RS-V3 and RS-V5 so they do not collide with the V-n ids of
[file-changes.md](file-changes.md)), and
[ADR-0024](../../adr/0024-an-agent-holds-one-session-per-room.md).

## Setup

Run [`P-BUILD`](common.md#p-build), then `P-LAUNCH-PAID` — every test here spends money, so
there is no free lane to start from. This area adds:

1. **One Agent, Nova, created through [`P-NEW-PERSONA`](common.md#p-new-persona)** with Model
   Haiku and Effort `low` (§0.3), alias `nova`. The built-in Chief of Staff is already Haiku / low
   after `P-LAUNCH-PAID`'s first step; RS-M10 uses it. Several tests name a before/after
   comparison (RS-M1, RS-M3): keep the Model and the Effort identical between the two runs and
   record both, per [§0.3](../manual-tests.md#03-the-model-and-effort-convention).
2. **Every Room here is built the same way.** *New chat* with a single Agent returns that Agent's
   existing direct Room (`ChatService.cs:477-480`), so it can never make a second Room with only
   Nova in it. Make each extra Room with Nova **and** a pipe bot as the third Member
   (`pwsh tools/echo-bot.ps1 -Name "pipe"`, [`P-ECHO-BOT`](common.md#p-echo-bot)), and prefix
   **every** prompt with `@Nova`: a Room of three or more answers only a Mention
   (`ReplyGate.cs`). The bot answers only when mentioned, so it stays silent. "Room A" and
   "Room B" below are two such Rooms, named as the test says.
3. **A Room with no baseline lists nothing.** The first Turn an Agent takes in a Room reports
   no File Changes at all (`FileChangeTracker.cs:38-40, 66-69`), so a test that reads a File
   Changes line takes one throwaway Turn (`@Nova hello`) in that Room first.
4. **`Team:Acp:Adapters` replaces the built-in `claude` Adapter Profile, it does not add to it**
   (`AdapterCatalog.cs:55-79`), and a configured entry defaults `IsolateUserSettings` to `false`
   and `Commands` to none (`AdapterProfileOptions.cs:65, 82`). RS-M1, RS-M3 and RS-M7 need a
   second Profile with `SessionPerRoom: false`, so give the whole array, `claude` entry first
   (the first is the default), in `appsettings.Development.json` — never in `appsettings.json`:

   ```jsonc
   "Team": { "Acp": { "Enabled": true, "Adapters": [
     { "Id": "claude", "DisplayName": "Claude", "Command": "node",
       "UsesToolNamePrefix": true, "IsolateUserSettings": true,
       "SessionPerRoom": true, "Commands": ["compact"] },
     { "Id": "claude-shared", "DisplayName": "Claude (shared session)", "Command": "node",
       "UsesToolNamePrefix": true, "IsolateUserSettings": true,
       "SessionPerRoom": false, "Commands": ["compact"] }
   ] } }
   ```

   An Agent picks its Adapter on its card's **Adapter** select, which renders only when more
   than one Profile exists (`AgentProfileCard.razor:310`). Keep the stock `claude` Profile's settings
   in both entries (`AdapterCatalog.cs:62-75`) so the only difference between them is
   `SessionPerRoom`. `agency-acp` is a separate binary this repository does not supply; RS-V5
   needs it on `PATH`.
5. **`TraceWire` is not used on this page.** It prints the tool server's bearer token and common.md
   forbids it ([Standing conventions](common.md#standing-conventions)). The oracles instead are
   the Room Session store, `<data>\room-sessions\<Name>.json` (`RoomSessionStore.cs:51, 225`),
   one file per Agent holding, per Room id, the Adapter's `SessionId`, the `AdapterId` and
   `LastTurnUtc`; `O-TOOLCALLS` for tool calls; the Rooms themselves; and `O-ADAPTERS-LIST`. A
   `SessionId` that is unchanged across an idle close, a restart or a rename means the session
   was **resumed**; a different one means a **fresh** session replaced it. Copy the file's
   contents out before and after, and treat the file as read-only. The room id is the 32 hex
   characters after `/rooms/` in the address bar.
6. **Settings per test.** `Team__Acp__SessionIdleMinutes` is set per test (RS-M5) rather than
   left at its default `30`, which is too long to sit and wait for; `Team__Acp__MaxConcurrentTurns`
   defaults to `1` (`AcpOptions.cs:166`) and RS-M4 runs once at that default and once at `2`.
   Changing either needs `P-STOP` and a relaunch (`common.md`, P-STOP).
7. **RS-M8** needs a second terminal for a process listing (see the test) and, if you can get it,
   a memory reading per `node.exe` PID from Task Manager or
   `Get-Process -Id <ProcessId> | Select-Object WorkingSet64`.

## Tests

### RS-M1 — The two-trip stress test

**Paid** · about 120 min · 24+ Turns across two Rooms, twice (before and after)

*The headline test. Proves RS §1's whole claim: a Room Session's context is one Room's, so a
terse decision in one trip Room never resolves against the other's options, a compaction never
merges them, and a recap never invents what it cannot see (RS §2 U0).*

**Before you start**

- Nova exists (Setup 1), on the `claude-shared` Profile for the **before** run and on `claude`
  for the **after** run (Setup 4), with the same Model and Effort in both.
- Two Rooms named "Lisbon trip" and "Porto trip", each with Nova and the pipe bot (Setup 2).
- Nova's Work Dir is `<data>\Agents\Nova\work`.

**Steps**

1. In each Room send `@Nova give me three options — a hotel, a flat, an apartment`, so both
   Rooms end up with an option 1, 2 and 3.
2. Run 24 Turns, alternating Rooms, each a small refinement and each starting `@Nova`:
   "cheaper", "nearer the river", "parking?", and so on.
3. Decide tersely: `@Nova go with option 2` in Porto, `@Nova no, option 3` in Lisbon, then
   `@Nova yes, book it` in each — where booking means Nova writes `trips\<city>.md` in its Work
   Dir.
4. Push towards compaction: send `@Nova /compact` in each Room (the leading `/` must follow the
   Mention; a bare `/compact` is refused with a red `Unknown command`, `ChatService.cs:414-419`).
   The **Adapter's commands** list on Nova's card names `/compact` if the Profile offers it
   (`AgentProfileCard.razor:135`). The oracle is the Message Nova posts back, starting
   `Compacted my conversation` (`AdapterCommandOutcome.cs:57`). If the Adapter reports no token
   figures that Message may not appear; then paste long filler text instead and record that
   compaction was not observed.
5. Ask each Room `@Nova recap what we decided`.

**Pass if — all of these, for the after run**

- The count of **misattributed decisions** (a decision from one Room appearing in the other's
  reply, recap or `trips\<city>.md`) is zero.
- The count of **leak mentions** (any reference to the other city anywhere in those replies,
  recaps and files) is zero.
- Both counts are recorded for the **before** run as well, and the before run's total is
  strictly greater than the after run's, or the page says the before run was also clean and the
  test could not discriminate.

**Fail if**

- The after run has any misattributed decision or leak mention.

**Inconclusive if**

- Nova is not Online, `/compact` is refused, or fewer than 24 Turns completed in either run. A
  before run that is clean is not a failure of Room Sessions; record it and say it did not
  discriminate.

---

### RS-M2 — A Room-scoped preference crosses Rooms only through Memory

**Paid** · about 25 min · two short Turns in two Rooms, one baseline Turn

*Proves RS §2 U1/U2: a stated preference stays in the Room it was said in unless the Agent
chooses to write it to Memory, and then it reaches other Rooms by the visible File Changes
route, attributed.*

**Before you start**

- Nova on the `claude` Profile; Rooms "Room A" and "Room B" (Setup 2).
- In Room B take the baseline Turn first: `@Nova hello` (Setup 3).

**Steps**

1. In Room A: `@Nova answer in French from now on`, then `@Nova I prefer C# for any code`.
2. Open Nova's Work Dir, `<data>\Agents\Nova\work`, and read whichever memory file Nova
   wrote or changed, if any ([`O-FILE`](common.md#o-file)). Copy what it says about French and C#.
3. In Room B ask a coding question: `@Nova write a function that reverses a string`.

**Pass if — judged against step 2**

- If the memory file records the French preference, Room B's reply is in French, and the File
  Changes line on Room B's Turn names that file marked *by you, in Room 'Room A'*
  (`FileChangeTracker.cs`, the `" (by you, in Room '{roomName}')"` suffix).
- If the memory file records the C# preference, Room B's code is C#.
- Any preference the memory file does **not** record is absent from Room B: English, or not C#.

**Fail if**

- Room B follows a preference the memory file does not record, or Room A's preference reaches
  Room B with no memory file change.

**Inconclusive if**

- Nova wrote no memory file at all: then the expected behaviour is English and no C#, and a
  Room B reply in French or C# is the finding. Whether Nova writes Memory is the model's choice,
  so a run with no memory file proves only the first half.

---

### RS-M3 — Asked about another Room, the Agent says it cannot see it

**Paid** · about 15 min · one Turn, twice (before and after)

*Proves RS §2 U3: a Room Session tells the truth about what it cannot see, rather than
inventing an answer or silently reusing another Room's context.*

**Before you start**

- A Room "Lisbon trip" in which Nova has made a decision (for example `@Nova we pick the hotel`),
  and Room B, both with Nova and the pipe bot (Setup 2).
- For the **before** run Nova is on `claude-shared`, for the **after** run on `claude`.

**Steps**

1. In Room B: `@Nova what did we decide in the Lisbon Room?`

**Pass if — all of these, for the after run**

- Nova says it cannot see that conversation.
- It offers what its memory holds instead, rather than inventing a decision.

**Fail if**

- It states a decision from the Lisbon Room as if it had seen it.

**Inconclusive if**

- For the **before** run: record what it actually says. A shared session may answer correctly
  by accident at this scale, and that is not a failure.

---

### RS-M4 — Stop in one Room leaves another's Turn running

**Paid** · about 25 min · two Turns, run at `MaxConcurrentTurns` 1 and again at 2

*Proves RS §2 U8 and P-6: Stop is scoped to the Room Session it was sent for, never to the
Persona. It is two runs because at the default `MaxConcurrentTurns = 1` (`AcpOptions.cs:166`)
Room B's Turn queues behind Room A's and nothing runs at the same time: run 1 proves the queue
survives a Stop, run 2 proves the Turns are independent.*

**Before you start**

- Nova on the `claude` Profile; Rooms A and B (Setup 2), each with a baseline `@Nova hello`.
- Run 1 under `P-LAUNCH-PAID` as it is. Run 2 after `P-STOP` and a relaunch with
  `$env:Team__Acp__MaxConcurrentTurns = '2'` added to the launch block.

**Steps**

1. In Room A: `@Nova write a long essay on the history of Lisbon` (something that takes a
   while).
2. While Room A's Draft is streaming, in Room B: `@Nova say hello in one word`.
3. Press **Stop** on Room A's Draft.
4. Wait for Room B's Turn to finish.

**Pass if — all of these**

- Room A posts nothing after Stop.
- Run 1: Room B's reply is posted after Stop, having waited behind Room A. Run 2: Room B's
  Draft appeared while Room A's was still streaming, and its reply is posted undisturbed.
- Room B's Turn is never stopped, discarded or marked as stopped.

**Fail if**

- Room B's Turn is dropped, stopped or never answers.

**Inconclusive if**

- Room A's Turn finished before you could press Stop.

---

### RS-M5 — Idle close, resume, and an app restart, both remembering

**Paid** · about 20 min · two follow-ups, one app restart

*Proves RS §2 U5/U6 and D-5: an idle Room Session closes and resumes by its stored id, keeping
what it knew, both across an idle window and across the app itself restarting.*

**Before you start**

- Nova on the `claude` Profile, in a Room with the pipe bot (Setup 2).
- `P-STOP`, then relaunch under `P-LAUNCH-PAID` with `$env:Team__Acp__SessionIdleMinutes = '1'`
  added to the launch block.

**Steps**

1. `@Nova remember that the code word is kingfisher`, and wait for the reply.
2. Copy `<data>\room-sessions\Nova.json` and note the Room's `SessionId` (**id 1**).
3. Wait two minutes (the idle sweep runs every 30 seconds, `RoomSessionPool.cs:18`), then
   `@Nova what is the code word?`
4. Copy the file again (**id 2**).
5. `P-STOP`, relaunch the same way, then `@Nova what is the code word?` again.
6. Copy the file once more (**id 3**).

**Pass if — all of these**

- Both follow-ups answer `kingfisher`, as if the session had never closed.
- **id 1**, **id 2** and **id 3** are identical.

**Fail if**

- A follow-up does not know the code word, or the `SessionId` changed at step 4 or 6 (a fresh
  session replaced the stored one).

**Inconclusive if**

- The file has no entry for the Room after step 1. Whether an application shutdown closes the
  session first is unverified; the test judges only the stored id and the answers, not a log line.

---

### RS-M6 — Restart forgets; the next Turn is fresh with Catch-up

**Paid** · about 15 min · one Restart, one follow-up

*Proves RS D-14: the Restart button on an Agent's card is not the same thing as an app
restart — it forgets every Room Session on purpose.*

**Before you start**

- Nova on the `claude` Profile, with a Room in which it has already taken three or four Turns
  (including `@Nova remember that the code word is kingfisher`).
- Copy `<data>\room-sessions\Nova.json` (**id before**).

**Steps**

1. On Nova's card press **Restart** (shown in every state except Starting,
   `AgentProfileCard.razor:182, 234`) and wait for Online.
2. Copy `<data>\room-sessions\Nova.json`: the Room's entry should be gone.
3. In the Room: `@Nova what is the code word?`
4. Copy the file again (**id after**).

**Pass if — all of these**

- After step 2 the Room has no `SessionId` (`RoomSessionPool`'s forget on Restart,
  `PersonaSupervisor.cs:483`).
- **id after** differs from **id before**.
- Nova still answers `kingfisher`, from the Room's last 20 Messages carried as Transcript
  Catch-up (`turn.transcriptHeader`, `PromptCatalog.cs:281`), not from a blank context.

**Fail if**

- The old `SessionId` survives the Restart, or Nova answers as if the Room were empty.

**Inconclusive if**

- The Room had no Messages before the Restart to carry.

---

### RS-M7 — A shared-session Adapter Profile is unchanged

**Paid** · about 20 min · two Rooms, one session

*Proves RS §6.12 and E-8: `SessionPerRoom: false` is Phase 0 plus nothing new — the pre-Room-Sessions
shape stays available and correct for an Adapter Room Sessions is not yet verified against.*

**Before you start**

- The two-Profile array of Setup 4 is configured and the app relaunched.
- A second Agent **Wren** (`P-NEW-PERSONA`, Haiku / low) with **Adapter** set to
  `Claude (shared session)` on its card, and Nova still on `Claude`.
- Two Rooms each with Wren and the pipe bot (Setup 2), each with a baseline `@Wren hello`.

**Steps**

1. `@Wren remember that the code word is kingfisher` in Room 1.
2. `@Wren what is the code word?` in Room 2.
3. Copy `<data>\room-sessions\Wren.json` and `<data>\room-sessions\Nova.json` after Nova has
   taken a Turn in the same two Rooms.

**Pass if — all of these**

- Wren answers `kingfisher` in Room 2: one session serves both Rooms.
- Nova does **not** know the code word in Room 2 (it is on per-Room sessions).
- The Room Session store holds per-Room `SessionId`s for Nova and does not hold two distinct ones
  for Wren. If the store is silent for Wren, record that.

**Fail if**

- Wren does not know the code word in Room 2, or Nova does.

**Inconclusive if**

- The prompt text itself (`systemPrompt.sharedSession` versus `systemPrompt.roomSessions`) is
  not observable: the mock adapter's echo does not carry the system prompt and `TraceWire` is
  forbidden. The automated prompt tests are that evidence; do not mark this test on it.

---

### RS-M8 — Process and memory cost at the live cap

**Paid** · about 25 min · four Rooms, a process listing after each first Turn

*Answers RS §8.4's "unknown, measured by RS-M8": memory per CLI child, and the cost of opening
versus resuming a Room Session.*

**Before you start**

- Nova on the `claude` Profile and four Rooms each with Nova and the pipe bot (Setup 2). In a
  second terminal, this listing, which separates the Adapter process from the Claude Code CLI
  processes by parent:

  ```powershell
  Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
    Select-Object ProcessId, ParentProcessId, CommandLine | Format-Table -Wrap
  ```

  The Adapter row is the one `O-ADAPTERS` counts (`CommandLine` contains `claude-agent-acp`);
  rows whose `ParentProcessId` is its `ProcessId` are the CLI children. That parentage is
  expected, not verified.

**Steps**

1. Mention Nova in Room 1, wait for the reply, run the listing.
2. Repeat for Rooms 2, 3 and 4, listing after each.
3. Note the wall-clock time from sending to the first Draft for each Room's first Turn.
4. Mention Nova again in Room 1 (its session was the least recently used and was closed to make
   room), and note the time to first Draft.

**Pass if — all of these**

- One Adapter process throughout.
- After Rooms 1-3 there are three CLI children; after Room 4 there are still no more than
  `MaxLiveSessions` (default 3, `AcpOptions.cs:159`): the fourth Room's open closed the least
  recently used Idle one first.
- Memory per child and the first-Draft timings for open versus resume are recorded in the live
  findings.

**Fail if**

- More than three CLI children are alive at once, or a second Adapter process appears.

**Inconclusive if**

- The child rows cannot be told from other `node.exe` rows; record the listing as it was.
  (`SessionTiming` is the adapter's own phase log and is not reachable without `TraceWire`; the
  wall-clock times replace it.)

---

### RS-M9 — The Human's own Claude Code settings do not leak in

**Paid** · about 15 min · one Turn, with a marker set beforehand

*Proves RS §6.10's "Recommended" isolation actually isolates — the live check Appendix B calls
V-1 and V-2, run here against a Room Session rather than a single shared one. This duplicates
the markers of [file-changes.md](file-changes.md) (FM-6); one run can serve both.*

**Before you start**

- Nova on the stock `claude` Profile, which isolates user settings by default
  (`AdapterCatalog.cs:72`); with an `Adapters` array configured, `IsolateUserSettings: true` must
  be on that entry (Setup 4).
- Your own `~/.claude/settings.json` has a distinctive output style, and `~/.claude/CLAUDE.md`
  has a marker line (for example `always end with the word pelican`). Back both up first.

**Steps**

1. Ask Nova anything: `@Nova what is 2 plus 2?`

**Pass if — all of these**

- No trace of either in the reply: neither the output style's voice nor the marker line.
- FM-6 in [file-changes.md](file-changes.md) passes under the same launch.

**Fail if**

- The reply shows the output style or the marker.

**Inconclusive if**

- Nova answers with neither, but you cannot tell the output style apart from its normal voice;
  use a more distinctive style.

---

### RS-M10 — A coordinator's overview, deliberately reassembled

**Paid** · about 30 min · two work Rooms plus the coordinator's Room with the Human

*Proves RS §6.11 and D-18: a coordinator that used to get an overview by accident, from one
shared session, gets it back on purpose, from Memory, reports and seeds — and invents nothing
it cannot see.*

**Before you start**

- The Chief of Staff (alias `cos`) Online and set to Haiku / low (`P-LAUNCH-PAID`'s first step).
- Rooms "Work A" and "Work B", each with the Chief of Staff and the pipe bot (Setup 2), and in
  each some Messages about a different piece of work.

**Steps**

1. In each work Room: `@cos follow this Room` (it calls `follow_room`, `FollowRoomTool.cs`).
   Read the newest line of `O-TOOLCALLS` for the Chief of Staff and confirm two `follow_room`
   calls.
2. Post a different status line in each work Room (`@cos Work A is blocked on the API key`).
3. In the Chief of Staff's own direct Room with the Human ask `where are we on both?`

**Pass if — all of these**

- It answers from its memory files and the reports posted to it, naming which Room each fact came
  from.
- It does not claim to have watched either work Room's conversation directly.

**Fail if**

- It reports a fact present in neither its memory, a report nor a seed, or claims to have
  watched a Room.

**Inconclusive if**

- Step 1 shows no `follow_room` calls; the setup, not Room Sessions, failed.

---

### RS-V3 — Resume survives a rename

**Paid** · about 15 min · one rename, one follow-up

*Answers RS Appendix B V-3: whether `session/resume` survives the Work Dir (`cwd`) moving on a
Persona rename. This is a measurement, not a pass or fail against an assumption.*

**Before you start**

- Nova on the `claude` Profile, in a Room with the pipe bot, having taken a Turn
  (`@Nova remember that the code word is kingfisher`).
- Copy `<data>\room-sessions\Nova.json` (**id before**).

**Steps**

1. Rename Nova to Terra from the card's edit form. A rename moves the folder, renames the file
   and the `users` row and direct Room in place (`PersonaRenameCascade.cs`), and moves the
   store file.
2. When Terra is Online, copy `<data>\room-sessions\Terra.json`.
3. In the Room: `@Terra what is the code word?`
4. Copy the file again (**id after**).

**Pass if — one of these outcomes is recorded**

- **Resumed:** **id after** equals **id before** and Terra answers `kingfisher`.
- **Fresh with Catch-up:** **id after** differs, Terra still answers `kingfisher` from the Room's
  recent Messages (RS §9 E-1). Both outcomes are acceptable per the spec; this test exists to
  learn which one is true.

**Fail if**

- Terra does not answer, or the store file is missing under the new name.

**Inconclusive if**

- Whether the `_meta` system prompt, Model and Effort were re-applied cannot be observed without
  `TraceWire`; the question stays open and goes in the live findings as such.

---

### RS-V5 — Does `agency-acp` hold several sessions, and advertise resume?

**Paid** · about 20 min · two Rooms on one process, read from the store and the Rooms

*Answers RS Appendix B V-5, which RS §6.12 already treats as unresolved: until this passes, a
Persona configured on `agency-acp` keeps `SessionPerRoom: false`.*

**Before you start**

- `agency-acp` is installed and on `PATH` (this repository does not supply it); if it is not,
  the test is Inconclusive.
- A Profile for it with `SessionPerRoom` set to `true` experimentally (accepting the risk this
  test exists to check), the stock `claude` entry kept first (Setup 4):

  ```jsonc
  { "Id": "agency-local", "DisplayName": "Agency (local)", "Command": "agency-acp",
    "UsesToolNamePrefix": false, "IsolateUserSettings": false,
    "SessionPerRoom": true }
  ```

- An Agent on that Profile, in two Rooms each with the pipe bot (Setup 2).

**Steps**

1. `@<Name> remember that the code word is kingfisher` in Room 1, then `@<Name> what is the code
   word?` in Room 2.
2. Copy `<data>\room-sessions\<Name>.json`.
3. Run `O-ADAPTERS-LIST`-style listing for `agency-acp`:
   `Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*agency-acp*' }`.
4. Ask in Room 1 again, `@<Name> what is the code word?`, after the idle window or a **Restart**
   to see whether resume is offered.

**Pass if — a finding is recorded for each**

- Whether the second `session/new` succeeded as a second concurrent session on one process
  (two `SessionId`s in the store and one `agency-acp` process) or the first was torn down.
- Whether resume works (step 4 returns the same `SessionId`). The adapter's `initialize`
  response (`sessionCapabilities.resume`) is not observable without `TraceWire`; the store is.

**Fail if**

- Never a test failure; set `SessionPerRoom` back to `false` for `agency-acp` afterwards
  regardless of the result, until the finding is reviewed.

**Inconclusive if**

- `agency-acp` is not installed or the Persona never goes Online.
