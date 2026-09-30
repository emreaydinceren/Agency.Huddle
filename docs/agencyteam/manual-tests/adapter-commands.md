# Adapter commands

Prove, in a real browser against a real Adapter, that `@Nova /compact` compacts a Teammate's conversation and says so in the Room, that the Teammate still remembers the conversation afterwards, that a command the profile does not allow is an ordinary Message, that a Teammate's card lists what it offers, and that Stop leaves a Teammate usable. The automated suite proves the parts; this page proves they hold together on a screen, against an Adapter that really compacts. **Written 2026-09-30, not run**: four of the five tests spend money, because nothing compacts without a Turn.

**5 tests** · 1 free, 4 paid 💰 · about 1 hour.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below. The
design is [the Adapter commands spec](../../Huddle.Commands-Specifications.md); its use cases are
C1, C5 to C7, C10, C13 and C15.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named in each test from
[Common procedures](common.md). This area adds:

1. Use a **scratch** Teammate on the stock Claude Adapter, never one that matters. A compaction
   deliberately throws away most of what the Teammate remembers of a conversation.
2. The stock profile allows `compact` only. Do not add a `Commands` entry for this page.
3. Keep the scratch Teammate's Model and Effort at the paid lane's Haiku / low, as the script says.
4. "A warm conversation" below means three or four ordinary exchanges in a direct Room with the
   scratch Teammate, including one fact to recall later: tell it `My lucky number is 7342.` Haiku's
   context is small at this point, so the compaction shrinks it only a little; that is enough to prove
   the path, and the figure in the reply is the Adapter's own.
5. "Restart the app" always means `P-STOP` then the `dotnet run` line for the lane the test names.

## Tests

### COMMANDS-01 — `@Nova /compact` compacts the conversation and says so

**Paid** 💰 · about 20 min

*Proves the whole path: the Mention parses as a command, the Adapter's `/compact` runs (not text), the outcome Message is posted as the Teammate from the Adapter's own figures, and the Teammate still knows the conversation afterwards.*

**Before you start**

- Paid lane (`P-LAUNCH-PAID`). The scratch Teammate is Online and a warm conversation is in its direct Room.
- `T-A` shows the app log (`O-LOG`).

**Steps**

1. Open the Teammate's card from `/teammates` and note the line under its status. Close it.
2. In the direct Room, type `@scratch /compact` (use the Teammate's real Name) and send it.
3. Watch the Room until the Teammate posts. Copy the Message text exactly.
4. Type `What is my lucky number?` and send it. Read the reply.
5. Send one more ordinary Message and read the reply.

**Pass if — all of these**

- Step 1 shows `/compact — Free up context by summarizing the conversation so far`.
- Step 3 posts exactly one Message from the Teammate, of the form `Compacted my conversation: {n} → {m} tokens in {s} s.` with `m` smaller than `n`, thousands separators as commas, and `s` a whole number of at least 1.
- Your own Message is shown as you typed it, with its Mention; nothing was posted in your name.
- Step 4 is answered from memory, in words, not as a blank or a refusal. Write down whether it says `7342`: a summary may keep it or drop it, and either is an Adapter fact, not a failure.
- Step 5 is answered normally.

**Fail if — any of these**

- The Teammate answers step 2 in prose about what `/compact` is -> the prompt reached the Adapter framed, not bare; check `O-LOG` for the prompt, which must be exactly `/compact`.
- Nothing is posted after step 2 -> the outcome line was not built; check `O-LOG` for a Turn that ended without a post.
- The message says `Ran /compact.` with no figures -> the tool output did not reach the runner in the expected shape; record the raw text from `O-WIRE`.
- The Teammate posts twice -> the command Turn posted its own text and the fixed line both.

### COMMANDS-02 — A bare slash is still Huddle's own command

**Free** · about 10 min

*Proves the two syntaxes do not collide: a leading slash is Huddle's own (`/invite` is the only one it knows), and the Mention form never starts with one. Also proves a Mention-shaped command to a Teammate that is not running is just a Message.*

**Before you start**

- Free lane (`P-LAUNCH-FREE`), so no Adapter is running. A scratch Teammate exists and is Offline.
- A direct Room with the scratch Teammate is open.

**Steps**

1. Type `/compact` with nothing else and send it.
2. Type `@scratch /compact` and send it.
3. Type `/compact keep the decisions` and send it.

**Pass if — all of these**

- Step 1 is refused with an *Unknown command* notice and posts nothing.
- Step 2 is posted as a normal Message, is not refused, and nothing answers it (the Teammate is Offline). No error appears.
- Step 3 is refused the same way as step 1: an argument after a bare `/compact` does not make it a command.

**Fail if — any of these**

- Step 1 posts a Message -> the composer is letting a leading slash through.
- Step 2 is refused -> the composer is treating a Mention-form command as Huddle's own.

### COMMANDS-03 — A command the profile does not allow is an ordinary Message

**Paid** 💰 · about 10 min

*Proves D-4 and D-9: an unknown or disallowed name falls through, and the framed prompt keeps the Adapter from reading it as a command, so `/config` or `/mcp` cannot be run from a Room.*

**Before you start**

- Paid lane. The scratch Teammate is Online and its card shows the `/compact` line.

**Steps**

1. Type `@scratch /config` and send it.
2. Wait for the reply and read it.
3. Type `@scratch /compact-everything` and send it. Read the reply.
4. Read `O-LOG` for both Messages.

**Pass if — all of these**

- Step 2 is ordinary prose that treats the text as a request or a question. It does **not** open, change or report any setting.
- Step 3 is also prose, and no compaction message is posted.
- `O-LOG` holds one Information line per Message, saying the command is not offered, and no Turn prompt that begins with `/`.

**Fail if — any of these**

- Either Message changes the Teammate's behaviour as a setting would (for example a different Model is reported) -> a disallowed command reached the Adapter bare; stop and record the prompt from `O-WIRE`.
- Nothing is logged for either Message -> the not-offered path is not reporting.

### COMMANDS-04 — The card lists what the Teammate offers, and only while it is running

**Paid** 💰 · about 10 min

*Proves the card line follows the Adapter's list: it appears once the Adapter advertises, is plain text, updates without reopening, and goes when the Teammate stops.*

**Before you start**

- Paid lane, app just restarted, scratch Teammate Offline or not yet started.

**Steps**

1. Open the scratch Teammate's card. Note whether any `/…` line shows.
2. Start the Teammate (send it any Message) and keep the card open. Watch the lines.
3. Hover the `/compact` line and read its tooltip.
4. Open the card's Edit view, change the Effort, and save. The Teammate restarts; watch the lines in the card, reopening it if it closed.
5. Open the card of a Teammate on a different Adapter, or on the free lane, if you have one.

**Pass if — all of these**

- Step 1 shows no command line and no empty heading.
- Step 2 shows exactly one line, `/compact — Free up context by summarizing the conversation so far`, without reopening the card.
- Step 3's tooltip reads `Write @scratch /compact in a Room to run it`.
- Step 4 removes the line until the restarted Teammate connects, then it returns.
- Step 5 shows no command line for a Teammate whose Adapter allows none.

**Fail if — any of these**

- More than one line shows -> the allowlist is not filtering the Adapter's full list, and the Human's own skills may be leaking into the card; record the lines.
- The line stays while the Teammate is stopped -> `PersonaCommands` is not forgetting a stopped runner.

### COMMANDS-05 — Stop during a compaction leaves the Teammate usable

**Paid** 💰 · about 15 min

*Proves verification V-4: whether Stop interrupts a compaction in flight, and that the session is still good afterwards. Also the only place the real behaviour of `session/cancel` on `/compact` is recorded.*

**Before you start**

- Paid lane. A scratch Teammate with as long a conversation as you are willing to pay for, so the compaction takes several seconds: ten exchanges, each asking for a paragraph.

**Steps**

1. Type `@scratch /compact` and send it.
2. As soon as the Draft appears, press **Stop** in the Room.
3. Wait ten seconds and record what the Room shows: a posted Message, or nothing.
4. Send `Reply with the word yes.` and read the reply.
5. Type `@scratch /compact` again and let it finish.

**Pass if — all of these**

- Step 3 shows no outcome Message after a Stop that landed while the Turn was running, or a normal completed message if the compaction finished first. Write down which.
- Step 4 is answered normally; the session is not wedged.
- Step 5 posts the usual `Compacted my conversation…` Message.

**Fail if — any of these**

- Step 4 never answers, or the Teammate reads Degraded -> a cancelled compaction left the session unusable.
- Step 3 shows a `/compact did not finish.` Message for a Stop -> a Stop is being read as a failure.

Record the observed behaviour of step 3 in the spec's Appendix C (V-4) and in [Known limits](../known-limits.md): it is the one fact this page exists to settle.
