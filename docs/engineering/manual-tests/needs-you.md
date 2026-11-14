# Needs You

Prove, in a real browser, that the Needs You card in the sidebar lists a Room while it waits on the Human and stops listing it only when the Human posts there or dismisses it; that opening a Room does not clear it; that dismissing never answers the Question or Form behind it; and that a restart rebuilds the Mention reason from the Transcripts. The automated suite proves the tracker's rules against real events and the two components in bUnit; this page proves what only a browser shows: the card's place and look in the sidebar, the hover-revealed ✕, and the restart. The design is [the Needs You spec](../../Huddle.NeedsYou-Specifications.md) and [ADR-0050](../../adr/0050-needs-you-is-derived-from-room-events-and-held-in-memory.md).

**4 tests** · 4 free · about 0.4 hours. Written 2026-10-12; **not yet run by a person** (the same behaviour was checked once in the app against two seeded Rooms: the card, oldest-first order, Dismiss, and the Reply clearing one row).

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat). This area adds:

1. Wait until **Ada** and **Bo** are both **Online** on `/agents`. Two empty one-to-one Rooms, `Ada` and `Bo`, exist from that point.
2. Open each Room once and send `hello`, so each has a Transcript file. Note each Room's id: the 32 hexadecimal characters after `/rooms/` in the address bar.
3. In `T-B` set `$data = 'C:\seeds\huddle\uat\data'`, and `$ada = '<Ada's id>'`, `$bo = '<Bo's id>'`.
4. This area makes an Agent Mention you by writing a Message line into the Transcript and relaunching, because the mock Agents strip every `@` from what they echo. Define the helper once:

   ```powershell
   function Ask([string]$room, [string]$who) {
     $line = @{ id = [guid]::NewGuid().ToString('N'); timestamp = (Get-Date).ToUniversalTime().ToString('o'); senderId = "agent-$who"; senderName = $who; text = "Which option, @You? I need a decision." } | ConvertTo-Json -Compress
     Add-Content -Path "$data\rooms\$room.jsonl" -Value $line -Encoding utf8
   }
   ```

   After each `Ask`, stop the app and relaunch it with the `P-LAUNCH-UAT` launch command **without** re-running the seeder, so the data stays.

## Tests

### NEEDSYOU-01 — A Mention is rebuilt at startup, listed oldest first, and the card is hidden when nothing waits

**Free** · about 8 min

*Proves the startup scan, the card's placement, the order, and the empty state.*

**Before you start**

- Setup is done and both Rooms have a Transcript.

**Steps**

1. Look at the sidebar. Note whether any card sits above `Rooms`.
2. Run `Ask $bo 'Bo'`. Wait one minute. Run `Ask $ada 'Ada'`.
3. Stop and relaunch the app. Open `http://localhost:5100/`.
4. Look at the top of the sidebar.
5. Read the second line of each row.

**Pass if — all of these**

- At step 1 there is no card, and no empty box, above `Rooms`.
- At step 4 a card titled `Needs you` with a count of `2` sits above `Rooms`, holding `Bo` first and `Ada` second (the longer wait first).
- Each row has an `@` icon, the Room's name, and a second line of the form `Bo · HH:mm` or `Ada · HH:mm`.

### NEEDSYOU-02 — Opening a Room does not clear it; posting in it does

**Free** · about 5 min

*Proves D-4: only the Human's own post clears a Mention.*

**Before you start**

- NEEDSYOU-01 has just passed, with both rows showing.

**Steps**

1. Click the `Bo` row in the card. Read the Room, then click `Ada` in the Rooms group.
2. Look at the card.
3. Return to `Bo`, type `Option two.` and press Enter.
4. Look at the card.

**Pass if — all of these**

- After step 1 the card still shows both rows: reading `Bo` did not remove it.
- After step 3 the `Bo` row has gone and the count reads `1`; the `Ada` row is unchanged.

### NEEDSYOU-03 — Dismiss hides a Room without answering what is behind it

**Free** · about 7 min

*Proves D-5 and D-6: Dismiss is a reminder, not an answer.*

**Before you start**

- No Mention is outstanding in `Ada` (sending a Message in step 1 would clear one anyway).
- Ada is a mock Agent. A prompt containing `[elicit:one]` makes it put one Form to you before it echoes.

**Steps**

1. In the `Ada` Room send `[elicit:one]`. Do not answer the card that appears above the composer.
2. Look at the card.
3. Hover the `Ada` row and press its `✕`.
4. Look at the card, and at the Room.
5. Answer the Form in the Room.

**Pass if — all of these**

- At step 2 the card lists `Ada` with the Form icon (a clipboard) and no `@` icon: your own Message in step 1 ended any Mention.
- After step 3 the card is gone (it was the last row) and the Form card in the Room is still waiting.
- After step 5 the Form is answered and the card stays absent.

### NEEDSYOU-04 — Archiving hides a Room from the card, and unarchiving brings it back

**Free** · about 5 min

*Proves D-12: Archived is a display filter, so the tracker keeps the Room.*

**Before you start**

- A row for `Bo` is showing (`Ask $bo 'Bo'`, then relaunch).

**Steps**

1. Use the `⋯` menu on the `Bo` Room in `Rooms` and choose **Archive**.
2. Look at the card.
3. Open **Archived** (bottom of the Activity bar), select `Bo` on the **Rooms** tab and choose **Restore**.
4. Look at the card.

**Pass if — all of these**

- After step 1 the `Bo` row is gone from the card.
- After step 3 the `Bo` row is back, with the same time on its second line.
