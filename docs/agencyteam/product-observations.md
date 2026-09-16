# Product observations

Things noticed while using Agency.Huddle that no manual test asks about. This is a
user's-seat view — what was confusing, noisy or misleading in practice — not a defect list
and not a review of the code. Defects found *by* the tests live in the
[tracker](manual-tests/tracker.md) and on the issue board.

Each entry says what happened and why it matters. Several describe behaviour that is
working exactly as designed; the observation is about what it feels like to use, not about
whether it is correct.

> [!IMPORTANT]
> This page was written against `docs/manual-test-run-2026-09-14`, which forked from `main`
> before the MudBlazor migration. Every entry was re-checked against `main` at `774a472` on
> 2026-09-15 and now carries its status and, where one was opened, its issue. Three are
> fixed. Read the status line before acting on an entry.

## From the manual test run of 2026-09-15

The run drove the app for several hours across every area of the chat surface: rooms,
teammates, budgets, hooks, personas and the app tools.

### 1. Silence is the product's main failure mode, and it is never explained

**Still true on `main` (`774a472`). Filed as #40**, with observation 5 folded in.

This is the biggest thing I would fix. Four different mechanisms cause a teammate to
correctly say nothing, and **none of them shows anything on screen**:

| What happened | Why the teammate was right to stay quiet |
| --- | --- |
| Plain message in a room with three or more members | Nobody was `@`-mentioned, so it is context-only |
| Message arriving in a room whose budget is spent | The turn is declined before any prompt is built |
| **Continue** pressed on a paused room | The re-delivered message mentioned nobody |
| Plain message to the demo agent `echo` | See observation 2 |

In every case the app knows precisely why it stayed quiet, writes a clear line about it to
the server log, and shows the human nothing at all. From the user's seat, "working as
designed" and "broken" look identical.

This is not hypothetical friction. Twice during the run I concluded the app was wedged and
went hunting for the cause — once restarting it three times on a wrong theory — when
nothing was wrong. I had the server log open and still lost time. A user without the log
has no way through it at all.

The fix does not need to be heavy. A single quiet line in the transcript would do it:
*"Nova read this but was not addressed"*, *"Paused — Nova did not take this turn"*.

### 2. The built-in demo agent contradicts the product's own headline rule

**FIXED on `main`.** `echo` now answers a plain, unmentioned message in a two-member room - re-tested on `774a472`. The account below is kept as the record of what it cost while it was true; no issue was opened.

The room rule the product states everywhere — in `get_help`, in the system prompt, in the
docs — is that a two-member room has nobody else the message could be for, so the agent
answers **every** message.

`echo` does not. In a two-member room it ignored four consecutive plain messages and
replied instantly to `@echo hello there`.

`echo` and `alpha` are what a new user meets first — they are the reason a fresh install
demonstrates anything at all. Having them visibly break the rule the product just
explained is a poor first impression, and it is the single most expensive thing that
happened in this run: I spent roughly fifteen minutes diagnosing a delivery pipeline that
was healthy the whole time, because the free agent that should have proved it was quietly
following a different rule.

### 3. A room can change its own interaction rules without the human doing anything

**Still true on `main` (`774a472`). Filed as #41.** Re-reproduced directly: the same plain message drew a reply at two members and silence at three, with the invite announcing only the rename.

My two-member room with Nova silently became a three-member room, because Zellandine used
`invite_agent` to add itself so it could post there. That is legitimate — the tool exists
and membership was enforced properly.

The consequence is not obvious: the room crossed the two-to-three member boundary, so it
switched from *answers everything* to *mention-gated*. My next ordinary message got
silence. I had not invited anyone, had not noticed a membership change, and had no reason
to think the rules had moved under me.

Two members and three members are genuinely different products in the same window. When a
room crosses that line, say so in the transcript — *"Zellandine joined. Messages here now
need an `@mention`."*

### 4. Teammates routinely reply twice to one message

**Filed as #42.** Not re-verified since - it needs paid turns - but nothing merged since touches it.

Nova frequently produced two message rows for a single turn — its answer, plus a
`post_message` of roughly the same thing:

```
Nova :: Got it—I've saved "pumpkin" to memory.
Nova :: Remembered: pumpkin. 🎃Done—pumpkin is now in my persistent memory.
```

```
Nova :: PLUM OK.PLUM Remembered—ZEBRA is saved.
```

Nothing is broken — these are two deliberate messages, not a double-delivery bug, and the
budget counts them correctly. But it reads as the teammate talking to itself, it burns
budget twice as fast as the user expects, and it happened often enough across the run to
look like the norm rather than the exception. Worth a line in the tool guidance telling a
teammate not to post what it is already about to say.

### 5. Continue behaves like a coin flip

**Still true on `main` (`774a472`). Folded into #40**, because it is the same defect reached through a different control.

Of three **Continue** presses across the run, two granted the budget correctly and then
produced no visible effect whatsoever. The reason is reasonable — the message being
re-delivered mentioned nobody, so no teammate was woken — but the button gives no hint of
this either before or after.

The button is the human's only control for un-pausing a room, and pressing it can
plausibly look like it did nothing. Either name what it will re-deliver, or say afterwards
that the re-delivered message woke nobody.

### 6. Rooms are hard to tell apart in the sidebar

**Addressed. Filed as #43** - a later count found 14 rooms with `Nova, Jarvis` appearing five times.
A Room can now be renamed from its header, and the chosen name **survives later Invitations**:
`ChatService.InviteAsync` compares the Room's current name against what `RoomNaming.Derive` would
have produced before re-deriving, so it only re-names a Room that is still carrying its auto-name.
Without that comparison the fix would have been worthless here, since these Rooms acquire their
duplicate names through exactly the invite path - the next `mcp__team__invite_agent` call would have
thrown the chosen name away. What this does **not** add is the other half suggested below: there is
still no timestamp and no last-message preview, so two Rooms nobody has renamed remain as
indistinguishable as they were.

At one point the sidebar held two rooms named `Nova, echo, Jarvis` and two named
`Nova, Jarvis`. Rooms are named after their members, so any two rooms with the same
members are indistinguishable — no timestamp, no last-message preview, no distinguishing
mark of any kind. I navigated by URL id for the rest of the run.

A last-message snippet or a created date under the name would settle it.

### 7. What a teammate remembers is invisible and unmanageable

**Partly addressed. Filed as #44.** PR #38 closed the specific vector - a tool call naming a path inside `~/.claude` is now refused (#22) - but the general complaint stands: a `Bash` redirect is not caught, and the human still has no view of what a teammate holds.

Asked what word it had been asked to remember, a freshly restarted Nova answered with a
word from a **different room, two tests earlier** — because it had written that word to
disk during an unrelated turn.

The sandbox side of this is already filed (issue #22). The product gap is separate and
survives whatever that fix does: a teammate accumulates memory across rooms and across
restarts, and the human has no view of what it holds and no way to clear it. "Restart
clears what it remembers" is what the UI promises on the Edit card, and it is no longer
quite true once a teammate can write itself notes.

### 8. Smaller things

- **A refused reply vanishes without trace.** When a teammate's reply is refused for
  budget, you watch a draft row type out a full answer and then disappear, leaving
  nothing. Documented as correct, and it is — but it looks like the app lost the message.
  *Still true; part of #40.*
- **The Model picker is incomplete just after startup.** Opening **Edit** early showed
  only Haiku; the full list (Sonnet, Fable, Opus) appeared once the adapter had been
  probed. Silent and timing-dependent, so whether you see the real choice depends on how
  fast you clicked. *Re-measured on `774a472`: two entries at 265 ms, six at 8.5 s, and the
  control shows the raw id `sonnet` until the catalog lands. ***FIXED** by #39: both pickers
  are now disabled while their own catalog is being probed, so the card no longer offers a
  two-item list, or a provisional label, as though either were the agent's answer. Two of the
  seconds were not probe latency at all - the Model select used to repaint when the EFFORT
  probe answered rather than when its own catalog landed, because a Blazor handler renders only
  at its first yield and its completion; it now repaints between the two. The incomplete list
  and the raw-id label were one object, not two defects: an unread catalog is an empty one, so
  the only entry is the synthesised one for the stored id, whose display name IS that id.*
- **A model change silently drops your Effort setting.** Correct — the new model may not
  offer the same levels — but the selection disappeared with no notice that it happened or
  why. ***FIXED** by #45: the Edit card now carries a `role="status"` note between the Model
  and Effort selects saying the Effort was reset and that each model advertises its own
  levels. The reset itself is deliberate and unchanged — PERSONALIFECYCLE-29 asserts it, and
  a bUnit test now does too, so a future "fix" that preserved the Effort across a Model
  change fails the build rather than reintroducing this.*
- **Restarting a teammate means editing it.** There was no Restart control on an Online
  teammate's card, so the only route was to change the Persona text and save — making an
  edit you do not want in order to get an effect you do, and losing the teammate's memory
  on the way. ***FIXED** on `main` by PR #38 (#32): Restart is now offered in every settled
  state. `Starting` stays excluded, because a restart there races the start it would
  cancel.*

## Notes

Observations 1, 2 and 3 compounded, and fixing 2 has already removed a third of it: a new
user's first encounter with a quiet teammate no longer includes a built-in demo agent
quietly following a different rule from the one the product just explained.

What remains is 1 and 3, and they are still worth taking together. A user's first
encounter with a quiet teammate has several plausible explanations and no on-screen
evidence for any of them. If only one thing here gets attention, make it observation 1
(#40) — it is the cheapest to fix and it removes most of the confusion 3 causes. Entry 3
(#41) is the one case where the human can be told *before* the confusing silence rather
than after it, which is why the issue argues for building them together.
