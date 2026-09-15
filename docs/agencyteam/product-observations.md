# Product observations

Things noticed while using Agency.Huddle that no manual test asks about. This is a
user's-seat view — what was confusing, noisy or misleading in practice — not a defect list
and not a review of the code. Defects found *by* the tests live in the
[tracker](manual-tests/tracker.md) and on the issue board.

Each entry says what happened and why it matters. Several describe behaviour that is
working exactly as designed; the observation is about what it feels like to use, not about
whether it is correct.

## From the manual test run of 2026-09-15

The run drove the app for several hours across every area of the chat surface: rooms,
teammates, budgets, hooks, personas and the app tools.

### 1. Silence is the product's main failure mode, and it is never explained

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

Of three **Continue** presses across the run, two granted the budget correctly and then
produced no visible effect whatsoever. The reason is reasonable — the message being
re-delivered mentioned nobody, so no teammate was woken — but the button gives no hint of
this either before or after.

The button is the human's only control for un-pausing a room, and pressing it can
plausibly look like it did nothing. Either name what it will re-deliver, or say afterwards
that the re-delivered message woke nobody.

### 6. Rooms are hard to tell apart in the sidebar

At one point the sidebar held two rooms named `Nova, echo, Jarvis` and two named
`Nova, Jarvis`. Rooms are named after their members, so any two rooms with the same
members are indistinguishable — no timestamp, no last-message preview, no distinguishing
mark of any kind. I navigated by URL id for the rest of the run.

A last-message snippet or a created date under the name would settle it.

### 7. What a teammate remembers is invisible and unmanageable

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
- **The Model picker is incomplete just after startup.** Opening **Edit** early showed
  only Haiku; the full list (Sonnet, Fable, Opus) appeared once the adapter had been
  probed. Silent and timing-dependent, so whether you see the real choice depends on how
  fast you clicked.
- **A model change silently drops your Effort setting.** Correct — the new model may not
  offer the same levels — but the selection disappears with no notice that it happened or
  why.
- **Restarting a teammate means editing it.** There is no Restart control on an Online
  teammate's card (issue #32), so the only route is to change the Persona text and save —
  making an edit you do not want in order to get an effect you do, and losing the
  teammate's memory on the way.

## Notes

Observations 1, 2 and 3 compound. Each is individually small; together they mean a user's
first encounter with a quiet teammate has at least four plausible explanations, no
on-screen evidence for any of them, and a built-in demo agent that behaves inconsistently
with the documented rule. If only one thing here gets attention, make it observation 1 —
it is the cheapest to fix and it removes most of the confusion the other two cause.
