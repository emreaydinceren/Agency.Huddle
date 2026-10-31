# Adapter commands

Prove, in a real browser against a real Adapter, that `@Scratch /compact` compacts an Agent's conversation and says so in the Room, that the Agent still remembers the conversation afterwards, that a command the profile does not allow is an ordinary Message, that an Agent cannot run a command, that a compaction is not charged to the token Budget, that an Agent's card lists what it offers, and that Stop leaves an Agent usable. The automated suite proves the parts; this page proves they hold together on a screen, against an Adapter that really compacts. **Written 2026-09-30, repaired 2026-10-09, not run**: six of the seven tests spend money, because nothing compacts without a Turn.

**7 tests** · 1 free, 6 paid 💰 · about 1.8 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below. The
design is [the Adapter commands spec](../../Huddle.Commands-Specifications.md); its use cases are
C1, C5 to C7, C10, C13 and C15.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named in each test from
[Common procedures](common.md). This area adds:

1. Use a **scratch** Agent on the stock Claude Adapter, never one that matters, made with
   [`P-NEW-PERSONA`](common.md#p-new-persona): Name `Scratch`, Alias `scratch`, **Model** Haiku,
   **Effort** `low`. A compaction deliberately throws away most of what the Agent remembers of a
   conversation.
2. The stock profile allows `compact` only. Do not add a `Commands` entry for this page. A configured
   profile with no `Commands` offers nothing (`AdapterCatalog.cs:74,94`), which COMMANDS-04 step 6 uses.
3. Keep the scratch Agent's Model and Effort at Haiku / low, as the script says, in every test.
4. "A warm conversation" below means three or four ordinary exchanges in a direct Room with the
   scratch Agent, including one fact to recall later: tell it `My lucky number is 7342.` Most of a
   fresh session's context is fixed overhead, so the compaction shrinks it by far more than the
   conversation (the spec observed 50,624 to 2,964 tokens); the figure in the reply is the Adapter's
   own. If the Adapter reports a rise, the Message reads `Ran /compact: {n} → {m} tokens.` instead
   (`AdapterCommandOutcome.cs:50-53`): record it as an Adapter fact, not a failure.
5. "Restart the app" always means `P-STOP` then the launch line for the lane the test names.
6. This area has no oracle for the prompt text or the raw tool output: the app never logs a prompt, and
   `TraceWire` is forbidden. Where a test needs to know whether a Message reached the Adapter as a
   command, it reads the Room (what was posted) and `O-LOG`, and the free test reads the mock
   Agent's echo.

## Tests

### COMMANDS-01 — `@Scratch /compact` compacts the conversation and says so

**Paid** 💰 · about 25 min

*Proves the whole path: the Mention parses as a command, the Adapter's `/compact` runs (not text), the outcome Message is posted as the Agent from the Adapter's own figures, and the Agent still knows the conversation afterwards.*

**Before you start**

- Paid lane (`P-LAUNCH-PAID`). `Scratch` is Online and a warm conversation is in its direct Room.
- `T-A` shows the app log (`O-LOG`).

**Steps**

1. Open `Scratch`'s card from `/agents`. The command line sits under the status and, once the conversation is warm, under the Spend line. Read it, then close the card.
2. In the direct Room, type `@Scratch /compact` and send it.
3. Watch the Room until the Agent posts. Copy the Message text exactly.
4. Type `What is my lucky number?` and send it. Read the reply.
5. Send one more ordinary Message and read the reply.

**Pass if — all of these**

- Step 1 shows `/compact — Free up context by summarizing the conversation so far`.
- Step 3 posts exactly one Message from the Agent, of the form `Compacted my conversation: {n} → {m} tokens in {s} s.` with `m` smaller than `n`, thousands separators as commas, and `s` a whole number of at least 1. If the Message instead reads `Ran /compact: {n} → {m} tokens.`, write down both figures: the Adapter reported no fall.
- Your own Message is shown as you typed it, with its Mention; nothing was posted in your name.
- Step 4 is answered from memory, in words, not as a blank or a refusal. Write down whether it says `7342`: a summary may keep it or drop it, and either is an Adapter fact, not a failure.
- Step 5 is answered normally.

**Fail if — any of these**

- The Agent answers step 2 in prose about what `/compact` is -> the text was read as an ordinary Message although the card lists the command; check that the card line was present before you sent, and that you wrote the Agent's Name or Alias first.
- Nothing is posted after step 2 and `O-LOG` shows no error -> the outcome line was not built; check `O-LOG` for a Turn that ended without a post.
- The Message says `Ran /compact.` with no figures -> the Adapter's output did not carry the figures in the expected shape; copy the Message and record the Adapter version.
- The Agent posts twice -> the command Turn posted its own text and the fixed line both.

### COMMANDS-02 — A bare slash is still Huddle's own command

**Free** · about 10 min

*Proves the two syntaxes do not collide: a leading slash is Huddle's own (`/invite` is the only one it knows), and the Mention form never starts with one. Also proves a Mention-shaped command to an Agent whose Adapter advertises no commands is just a Message.*

**Before you start**

- Free lane with someone to talk to (`P-LAUNCH-UAT`). Ada is Online (mock Adapter, which advertises no commands, so its card has no `/…` line).
- `T-A` shows the app log (`O-LOG`). Ada's direct Room is open.

**Steps**

1. Type `/compact` with nothing else and send it.
2. Type `@Ada /compact` and send it. Wait for the reply.
3. Type `/compact keep the decisions` and send it.

**Pass if — all of these**

- Step 1 is refused with an *Unknown command* notice and posts nothing.
- Step 2 is posted as a normal Message and is not refused. Ada answers with the mock's echo of the whole prompt, which contains the text `/compact`; no `Compacted my conversation` Message appears. That the text arrives inside a longer framed prompt, not as a bare `/compact`, is the point.
- `O-LOG` holds one Information line for step 2 saying that Ada read `/compact` as an ordinary message because the command is not offered to it.
- Step 3 is refused the same way as step 1: an argument after a bare `/compact` does not make it a command.

**Fail if — any of these**

- Step 1 posts a Message -> the composer is letting a leading slash through.
- Step 2 is refused -> the composer is treating a Mention-form command as Huddle's own.
- Step 2 gets no reply and no `O-LOG` line -> the Message was not delivered to Ada; check the Room's Members first.

### COMMANDS-03 — A command the profile does not allow is an ordinary Message

**Paid** 💰 · about 10 min

*Proves D-4 and D-9: an unknown or disallowed name falls through, and the framed prompt keeps the Adapter from reading it as a command, so `/config` or `/mcp` cannot be run from a Room.*

**Before you start**

- Paid lane. `Scratch` is Online and its card shows the `/compact` line.
- `T-A` shows the app log (`O-LOG`).

**Steps**

1. Type `@Scratch /config` and send it.
2. Wait for the reply and read it.
3. Type `@Scratch /compact-everything` and send it. Read the reply.
4. Read `O-LOG` for both Messages.

**Pass if — all of these**

- Step 2 is ordinary prose that treats the text as a request or a question. It does **not** open, change or report any setting.
- Step 3 is also prose, and no compaction message is posted.
- `O-LOG` holds one Information line per Message, each saying the command is not offered to `Scratch` and that it was read as an ordinary message.

**Fail if — any of these**

- Either Message changes the Agent's behaviour as a setting would (for example a different Model is reported) -> a disallowed command reached the Adapter bare; stop, copy both Messages and the reply, and report it.
- Nothing is logged for either Message -> the not-offered path is not reporting.

### COMMANDS-04 — The card lists what the Agent offers, and only while it is running

**Paid** 💰 · about 15 min

*Proves the card line follows the Adapter's list: it is present once the Adapter has advertised, is plain text, returns by itself after a restart of the session, and an Agent whose profile allows nothing shows none.*

**Before you start**

- Paid lane. The supervisor starts every Agent at launch and the runner opens the session at once, so the list arrives within a second or two and `Scratch` is already Online with the line. This test therefore watches the line leave and return rather than appear for the first time.
- Another browser tab on `/agents` is not needed; keep one.

**Steps**

1. Open `Scratch`'s card and keep it open. Read the status and the lines under it.
2. Hover the `/compact` line and read its tooltip (a browser `title` tooltip, not a MudBlazor one).
3. Press **Restart** on the card. Watch the status and the lines, without closing or reopening the card, until the status is Online again.
4. Count the `/…` lines.
5. Relaunch with two Adapter profiles as in [adapters](adapters.md) Setup 1 (`mock` first, `claude` second), then on `Scratch`'s card choose the `mock` Adapter, which restarts it. Open the card of the `claude`-profile Agent if you have one.
6. Read the card of `Scratch` on the `mock` Adapter.

**Pass if — all of these**

- Step 1 shows exactly one line, `/compact — Free up context by summarizing the conversation so far`, and no empty heading.
- Step 2's tooltip reads `Write @Scratch /compact in a Room to run it` (the Agent's Name).
- Step 3 shows the line gone while the status is Starting, then back, without reopening the card.
- Step 4 counts exactly one line.
- Step 6 shows no command line and no empty heading: the mock advertises none, and a configured profile with no `Commands` allows none.

**Fail if — any of these**

- More than one line shows -> the allowlist is not filtering the Adapter's full list, and the Human's own skills may be leaking into the card; record the lines.
- The line stays while the status is Starting or Offline -> `PersonaCommands` is not forgetting a stopped runner.
- The line does not return after Online -> the list is not re-read after a restart; the Human's retry of a command would be refused as unoffered.

### COMMANDS-05 — Stop during a compaction leaves the Agent usable

**Paid** 💰 · about 15 min

*Proves verification V-4: whether Stop interrupts a compaction in flight, and that the session is still good afterwards. Also the only place the real behaviour of `session/cancel` on `/compact` is recorded.*

**Before you start**

- Paid lane. `Scratch` has as long a conversation as you are willing to pay for, so the compaction takes several seconds: ten exchanges, each asking for a paragraph.

**Steps**

1. Type `@Scratch /compact` and send it.
2. As soon as the Draft appears, press **Stop** in the Room.
3. Wait ten seconds and record what the Room shows: a posted Message, or nothing.
4. Send `Reply with the word yes.` and read the reply.
5. Type `@Scratch /compact` again and let it finish.

**Pass if — all of these**

- Step 3 shows no outcome Message after a Stop that landed while the Turn was running, or a normal completed message if the compaction finished first. Write down which.
- Step 4 is answered normally; the session is not wedged.
- Step 5 posts the usual `Compacted my conversation…` Message.

**Fail if — any of these**

- Step 4 never answers, or the Agent reads Degraded -> a cancelled compaction left the session unusable.
- Step 3 shows a `/compact did not finish.` Message for a Stop -> a Stop is being read as a failure.

Record the observed behaviour of step 3 in the spec's Appendix C (row V-4, which names COMMANDS-04 in error; this is COMMANDS-05) and in [Known limits](../known-limits.md): it is the one fact this page exists to settle.

### COMMANDS-06 — An Agent's `@Kai /compact` is an ordinary Message (C7)

**Paid** 💰 · about 15 min

*Proves C7: only a Message from the Human can be a command. Another Agent that posts `@Kai /compact` with `post_message` wakes Kai with text, not with a compaction.*

**Before you start**

- Paid lane. `Scratch` and a second Agent `Kai` (Name `Kai`, **Model** Haiku, **Effort** `low`, made with [`P-NEW-PERSONA`](common.md#p-new-persona)) are both Online, and both cards show the `/compact` line.
- A Room with you, `Scratch` and `Kai` is open (New chat with both). With three Members only a Mention wakes anyone.
- `T-A` shows the app log (`O-LOG`).

**Steps**

1. Send `@Scratch Post a message in this room that says exactly @Kai /compact and nothing more.`
2. Wait for `Scratch` to post. Read the Message exactly as posted.
3. Wait for `Kai` to answer. Read the reply.
4. Read `O-LOG` for `Kai`.

**Pass if — all of these**

- Step 2 shows a Message from `Scratch` whose text is `@Kai /compact` with the Mention styled.
- Step 3 is ordinary prose from `Kai`, not a `Compacted my conversation` or `Ran /compact` Message. If `Scratch` posted something other than the bare text, write down what and repeat step 1 once.
- `O-LOG` shows no line saying `Kai` read `/compact` as an ordinary message: an Agent's Message is not examined as a command at all.

**Fail if — any of these**

- `Kai` posts `Compacted my conversation…` or `Ran /compact…` -> a Message from an Agent was run as a command.
- `Kai` never answers although it was Mentioned -> the Reply Gate dropped the Mention; this is not a C7 result, report it under the Gate.

### COMMANDS-07 — A compaction is not charged to the token Budget (C15)

**Paid** 💰 · about 20 min

*Proves C15: after a compaction the next ordinary Turn rebuilds the fixed overhead, and that rise (about 41,000 tokens in the trace) is not counted against `Acp:TokenBudget` (`RoomSession.cs:1054-1058`, spec §6.7).*

**Before you start**

- Paid lane, but launched with `$env:Team__Acp__TokenBudget = '90000'` instead of `'200000'`, and with the Spend and context figures of COMMANDS-01 in mind. The budget must sit above a warm conversation and below a warm conversation plus the rebuild, so that a charged rebuild would exhaust it. If your Adapter's fresh session is larger or smaller than the 50,624 / 2,964 the spec observed, adjust the number and write the value you used.
- `Scratch` is fresh (restart it so no earlier Turn counts), Online, in a direct Room. `T-A` shows the app log (`O-LOG`).

**Steps**

1. Hold a warm conversation with `Scratch` of three or four short exchanges. Check the card stays Online.
2. Send `@Scratch /compact` and wait for the `Compacted my conversation…` Message.
3. Send two ordinary Messages, one after the other, waiting for each reply.
4. Read the card and `O-LOG`.

**Pass if — all of these**

- Steps 2 and 3 complete and both ordinary Messages are answered.
- The card stays Online, and `O-LOG` has no Warning saying `Scratch` has spent its token budget.

**Fail if — any of these**

- The card turns Degraded with `The per-Persona token Budget of 90000 is spent; no more Turns until a Human speaks.` after step 3, or `O-LOG` shows the matching Warning -> the rebuilt context was added to the Budget. First check that the budget was not simply too small: relaunch with `'120000'` and repeat; only a failure that persists at the larger number is a defect.
