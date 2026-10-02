# Room Sessions: per-Room context, resume and Stop

Prove, in a real browser, that a Room Session actually holds one Room's context and nothing
else: that a long, interleaved run across two Rooms never resolves a terse follow-up against
the wrong one, that a preference only crosses Rooms through Memory, that Stop in one Room
leaves another's Turn running, that an idle Room Session resumes with its memory intact, that
a shared-session Adapter Profile still behaves exactly as it did before Room Sessions, and the
two open questions Appendix B leaves for a real Adapter to answer: whether `session/resume`
survives a rename, and whether `agency-acp` holds several sessions per process at all. None of
this has a mock-adapter substitute that means anything — the whole point is what a real model
does with 20+ interleaved Turns — which is why every test here spends money and none of it is
in the automated suite.

**12 tests** · 0 free, 12 paid 💰 · **none of them has been run yet.** This page documents what
to run, not a result — see [Known limits](../known-limits.md) for what that leaves unverified
today.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

Background: [Huddle.RoomSessions-Specifications.md](../../Huddle.RoomSessions-Specifications.md)
§2 (the use cases these tests prove), §6.5 (Catch-up), §6.8 (Stop, the streak, the Budget), §6.9
(the system-prompt texts), §6.12 (`agency-acp`), §10 (the RS-M table itself) and Appendix B (V-1
through V-5), and [ADR-0024](../../adr/0024-an-agent-holds-one-session-per-room.md).

## Setup

Run [`P-BUILD`](common.md#p-build), then `P-LAUNCH-PAID` — every test here spends money, so
there is no free lane to start from. This area adds:

1. **A Teammate whose Model and Effort you keep fixed across a *before* and an *after* run.**
   Several tests here (RS-M1, RS-M9) name a before/after comparison; keep the Model and the
   Effort identical between the two runs and record both, per [§0.3](../manual-tests.md#03-the-model-and-effort-convention).
2. **`Team:Acp:Adapters:*:SessionPerRoom` defaults `true`.** RS-M7 needs a second Adapter
   Profile with `SessionPerRoom: false` — see the [configuration table](../../Huddle.EngineeringGuide.md#configuration)'s
   `agency-acp` example. `Team__Acp__SessionIdleMinutes` is set per test (RS-M5) rather than
   left at its default `30`, which is too long to sit and wait for.
3. **`Team__Acp__TraceWire`** is required for RS-M5 and V-3 — the pass condition is the
   `TraceWire` log showing `session/close` and `session/resume`, not only the reply's content.
   Follow the [§0.2](../manual-tests.md#02-the-cost-guard) warning: a throwaway terminal,
   cleared afterwards, unset before any other test.
4. **RS-M8** needs a second terminal running `Get-CimInstance Win32_Process -Filter "Name='node.exe'"`
   (the same shape as [`O-ADAPTERS`](common.md#o-adapters)) so a process count can be read
   without disturbing the app, and, if you can get it, a memory reading per `node.exe` PID from
   Task Manager or `Get-Process -Id <ProcessId> | Select-Object WorkingSet64`.

## Tests

### RS-M1 — The two-trip stress test

**Paid** · about 2 hours · 24+ Turns across two Rooms, twice (before and after)

*The headline test. Proves RS §1's whole claim: a Room Session's context is one Room's, so a
terse decision in one trip Room never resolves against the other's options, a compaction never
merges them, and a recap never invents what it cannot see (RS §2 U0).*

**Steps**

1. Make two Rooms with Nova (or any Teammate), named "Lisbon trip" and "Porto trip".
2. In each, ask for three options — a hotel, a flat, an apartment — so both Rooms end up with
   an option 1, 2 and 3.
3. Run 24 Turns, alternating Rooms, each a small refinement: "cheaper", "nearer the river",
   "parking?", and so on.
4. Decide tersely: "go with option 2" in Porto, "no, option 3" in Lisbon, then "yes, book it"
   in each — where booking means Nova writes `trips\<city>.md` in its Work Dir.
5. Push towards compaction: send `/compact` in each Room if the Adapter lists it among its
   commands (check the `TraceWire` log), otherwise paste long filler text until `UsageUpdated.Used`
   visibly falls.
6. Ask each Room "recap what we decided".

**Expect**

Count two things across every reply, the recaps and both files: **misattributed decisions** (a
decision from one Room appearing in the other's reply, recap or file) and **leak mentions** (any
reference to the other city). Record both counts for a **before** run on Phase 0 (before Room
Sessions ships, or with `SessionPerRoom: false`) and an **after** run with Room Sessions on.
**After: both zero.** Before is expected to be nonzero — that gap is the test.

---

### RS-M2 — A Room-scoped preference crosses Rooms only through Memory

**Paid** · about 20 min · two short Turns in two Rooms

*Proves RS §2 U1/U2: a stated preference stays in the Room it was said in unless the Agent
chooses to write it to Memory, and then it reaches other Rooms by the visible File Changes
route, attributed.*

**Steps**

1. In Room A: "answer in French from now on", then "I prefer C# for any code".
2. Ask a coding question in Room B.

**Expect**

Room B answers in English, and in C#. Room B's prompt lists the memory file File Changes
carried, marked *by you, in Room 'A'* — check the `TraceWire` log, not only the reply.

---

### RS-M3 — Asked about another Room, the Agent says it cannot see it

**Paid** · about 10 min · one Turn

*Proves RS §2 U3: a Room Session tells the truth about what it cannot see, rather than
inventing an answer or silently reusing another Room's context.*

**Steps**

In Room B: "what did we decide in the Lisbon Room?"

**Expect**

**After:** it says it cannot see that conversation, and offers what its memory holds instead.
**Before** (Phase 0 or a shared session): it does not deny seeing other Rooms — record what it
actually says, since it may answer correctly by accident at this scale.

---

### RS-M4 — Stop in one Room leaves another's Turn running

**Paid** · about 15 min · two concurrent Turns

*Proves RS §2 U8 and P-6: Stop is scoped to the Room Session it was sent for, never to the
Persona.*

**Steps**

1. Start a long Turn in Room A (ask for something that takes a while).
2. While it runs, Mention the same Teammate in Room B.
3. Press Stop in Room A.

**Expect**

Room A posts nothing. Room B's Turn runs to completion and posts, undisturbed.

---

### RS-M5 — Idle close, resume, and an app restart, both remembering

**Paid** · about 20 min · two follow-ups, one app restart

*Proves RS §2 U5/U6 and D-5: an idle Room Session closes and resumes by its stored id, keeping
what it knew, both across an idle window and across the app itself restarting.*

**Steps**

1. Set `Team__Acp__SessionIdleMinutes` to `1`.
2. Talk in a Room, establishing something to follow up on.
3. Wait two minutes, then ask a follow-up that needs the earlier answer.
4. Restart the app (`P-STOP`, relaunch), then ask another follow-up needing the same context.

**Expect**

The `TraceWire` log shows `session/close` then `session/resume`, both times. Both follow-ups
answer correctly, as if the session had never closed.

---

### RS-M6 — Restart forgets; the next Turn is fresh with Catch-up

**Paid** · about 15 min · one Restart, one follow-up

*Proves RS D-14: the Restart button on a Teammate's card is not the same thing as an app
restart — it forgets every Room Session on purpose.*

**Steps**

1. Press Restart on the Teammate's card.
2. Follow up in a Room it was previously talking in.

**Expect**

A fresh session — no `session/resume` in `TraceWire` — whose first prompt carries that Room's
recent Messages as Transcript Catch-up (`turn.transcriptHeader`), not a blank context.

---

### RS-M7 — A shared-session Adapter Profile is unchanged

**Paid** · about 15 min · two Rooms, one session

*Proves RS §6.12 and E-8: `SessionPerRoom: false` is Phase 0 plus nothing new — the pre-Room-Sessions
shape stays available and correct for an Adapter Room Sessions is not yet verified against.*

**Steps**

Configure a Teammate on an Adapter Profile with `SessionPerRoom: false` (the `agency-acp`
example in the [configuration table](../../Huddle.EngineeringGuide.md#configuration)). Talk to it in two
Rooms.

**Expect**

One session serves both Rooms (`TraceWire` shows one `session/new`, no `session/resume`), and
its system prompt carries the shared-session text (`systemPrompt.sharedSession`), not the
per-Room one.

---

### RS-M8 — Process and memory cost at the live cap

**Paid** · about 25 min · four Rooms, `Get-Process` after each first Turn

*Answers RS §8.4's "unknown, measured by RS-M8": memory per CLI child, and open versus resume
timing.*

**Steps**

Put one Teammate in four busy Rooms; Mention it in each in turn, so its first Turn in each Room
opens a Room Session. After each, run `Get-CimInstance Win32_Process -Filter "Name='node.exe'"`.

**Expect**

One Adapter process, and at most `MaxLiveSessions` (default 3) live Claude Code CLI children —
the fourth Room's open evicts the least recently used idle one first. Record memory per child
and the `session/new`-versus-`session/resume` timings (the Adapter's own `SessionTiming` phases
in `TraceWire`) in the live findings.

---

### RS-M9 — The Human's own Claude Code settings do not leak in

**Paid** · about 15 min · one Turn, with a marker set beforehand

*Proves RS §6.10's "Recommended" isolation actually isolates — the live check Appendix B calls
V-1 and V-2, run here against a Room Session rather than a single shared one.*

**Steps**

Set a distinctive output style in your own `~/.claude/settings.json`, plus a marker line in
`~/.claude/CLAUDE.md`. Ask any Teammate anything.

**Expect**

No trace of either in the reply — neither the output style's voice nor the `CLAUDE.md` line —
and File Changes' FM-6 ([file-changes.md](file-changes.md)) passes under the same launch.

---

### RS-M10 — A coordinator's overview, deliberately reassembled

**Paid** · about 30 min · two work Rooms plus the coordinator's Room with the Human

*Proves RS §6.11 and D-18: a coordinator that used to get an overview by accident, from one
shared session, gets it back on purpose, from Memory, reports and seeds — and invents nothing
it cannot see.*

**Steps**

Have the Chief of Staff run two work Rooms it follows (`follow_room`). In its Room with the
Human, ask "where are we on both?"

**Expect**

It answers from its memory files and the reports posted to it, names which Room each fact came
from, and does not claim to have watched either work Room's conversation directly.

---

### V-3 — Resume survives a rename

**Paid** · about 15 min · one rename, one follow-up

*Answers RS Appendix B V-3: whether `session/resume` survives the Work Dir (`cwd`) moving on a
Persona rename, and whether it re-applies the `_meta` system prompt, Model and Effort.*

**Steps**

1. Talk to a Teammate in a Room, establishing context to follow up on.
2. Rename the Teammate (its card's edit form).
3. Follow up in the same Room.

**Expect**

Record what actually happens: either `session/resume` succeeds across the moved `cwd` and the
follow-up answers correctly, or it answers "not found" (`TraceWire`) and the Room opens fresh
with Transcript Catch-up (RS §9 E-1) — both are acceptable outcomes per the spec; this test
exists to learn which one is true, not to pass or fail against an assumption.

---

### V-5 — Does `agency-acp` hold several sessions, and advertise resume?

**Paid** · about 20 min · `initialize` plus two `session/new` calls, read from the log

*Answers RS Appendix B V-5, which RS §6.12 already treats as unresolved: until this passes, a
Persona configured on `agency-acp` keeps `SessionPerRoom: false`.*

**Steps**

Launch a Teammate on `agency-acp` with `SessionPerRoom: true` set experimentally (accepting the
risk this test exists to check). Talk to it in two Rooms. Read its `initialize` response and
both `session/new` calls from `TraceWire`.

**Expect**

Record, rather than assume: whether `agentCapabilities.sessionCapabilities.resume` is
advertised, and whether the second `session/new` succeeds as a second concurrent session on one
process or the first is torn down. Report the finding in
the live findings; set `SessionPerRoom` back to `false`
for `agency-acp` afterwards regardless of the result, until this test's finding is reviewed.
