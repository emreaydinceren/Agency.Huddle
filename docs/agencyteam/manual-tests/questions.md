# Questions

Prove, in a real browser against a real model, that an Agent asks the Human with `ask_human` when it should and does not when it should not; that it ends its Turn after asking and does not guess the answer; that the card appears only after the asker's framing Message; that an answer wakes only the asker in a Room of three; and that a Claude Teammate has no second, built-in way to ask. The automated suite proves the card, the store, the tool's refusals and the Reply Gate; this page proves what only a model can show: whether it calls the tool at the right moment, ends its Turn when told to, and does not reach for `AskUserQuestion`. **Written and run once on 2026-10-01, in the app** (Haiku, Budget 4, a scratch data directory): 02, 05 and 06 pass, 03 passes weakly, 04 is inconclusive for want of an `agency-acp` Profile, and 01 **failed** until `systemPrompt.askHuman` was added, then passed on a re-run the same day (one sample). Why it failed: the Claude Adapter defers every MCP tool, so the model never read `ask_human`'s description; see the [tracker](tracker.md#questions) and the spec's header note. All six spend money, because each one needs a Turn.

**6 tests** · 0 free, 6 paid 💰 · about 1.5 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below. The
design is [the Questions spec](../../Huddle.Questions-Specifications.md) (§10 lists the six tests as
QM-1 to QM-6) and [ADR-0022](../../adr/0022-an-agent-asks-the-human-with-a-question.md).

| Test | Spec id | Cost | What it answers |
| --- | --- | --- | --- |
| QUESTIONS-01 | QM-1 | Paid 💰 | Does a model call `ask_human`, with a framing sentence, when it needs preferences |
| QUESTIONS-02 | QM-2 | Paid 💰 | Does it end its Turn without guessing, and does the framing land before you can tap |
| QUESTIONS-03 | QM-3 | Paid 💰 | Does it leave the tool alone when the answer is a fact or an opinion |
| QUESTIONS-04 | QM-4 | Paid 💰 | Does a Teammate on `agency-acp` behave as QUESTIONS-01 does, or is the failure recorded |
| QUESTIONS-05 | QM-5 | Paid 💰 | In a Room of three, does an answer wake only the asker |
| QUESTIONS-06 | QM-6 | Paid 💰 | Does a Claude Teammate have no `AskUserQuestion`, and does the Turn not hang (§6.8) |

## Setup

Run [`P-BUILD`](common.md#p-build) then `P-LAUNCH-PAID` from [Common procedures](common.md). This area adds:

1. Use **scratch** Teammates (`P-NEW-PERSONA`), Haiku, never ones that matter. Create **Coach**
   (body: *"You are a fitness coach."*), and for QUESTIONS-05 a second, **Nova**, and a third,
   **Sable**, each with one sentence of body. Haiku advertises no effort levels, so Effort stays on
   `Model default`.
2. The tool is offered to **every** Teammate, so no Skill is needed. A Teammate's tool descriptions are
   read when its session starts: a Teammate started before this build must be restarted from its card.
3. "The card" means the panel headed *"<Name> is asking"* between the Transcript and the composer.
   "The framing" is the sentence the asker writes in the same Turn, saying why it is asking.
4. QUESTIONS-04 needs an Adapter Profile beyond the stock one, with `agency-acp` as the candidate;
   see [adapters.md](adapters.md). Without it the test is *Inconclusive*, not a defect.
5. QUESTIONS-06 reads the wire. Set **`Team__Acp__TraceWire`** and
   `Logging__LogLevel__Agency.Huddle.Acp.Wire` to `Trace` exactly as
   [work-mode.md](work-mode.md) setup step 4 does, in a throwaway terminal, and close it afterwards.

## Tests

### QUESTIONS-01 — A model frames its ask and calls `ask_human` with one to three Questions

**Paid** 💰 · about 15 min

*Proves a model reads `tool.askHuman.description` as an invitation to ask when it was about to write its questions out as a list. Spec QM-1.*

**Before you start**

- Paid lane (`P-LAUNCH-PAID`). Coach exists on Haiku and is in a Room with you.

**Steps**

1. In Coach's Room, send *"Help me plan a workout routine"*.
2. Wait for the Turn to end. Do not tap anything yet.
3. Read the Room, then run `O-TRANSCRIPT` on it.

**Pass if — all of these**

- Coach posted one or two sentences saying what it needs to know and why.
- A card headed *"Coach is asking"* is on screen with one to three Questions, each with two to four options.
- Every option is a short label, and none contains an `@`.

**Fail if — any of these**

- Coach wrote its questions out as a list in the Message and no card appeared -> the model did not call the tool; record the wording it used and read the tool description again.
- Coach said it has no such tool -> the tool is not offered; restart the Teammate from its card and check [get_help](app-tools.md).
- The card appeared with no framing sentence at all -> the description's "say why you are asking" is not being followed.

### QUESTIONS-02 — The asker ends its Turn without guessing, and the framing lands before the card can be tapped

**Paid** 💰 · about 15 min

*Proves the success text's "end your Turn now, and do not guess" holds, and that the options stay disabled while the asker is still writing (D-8), so the Transcript reads framing then answer. Spec QM-2.*

**Before you start**

- As QUESTIONS-01, with Coach's card from that test, or send the same Message again.

**Steps**

1. Send *"Help me plan a workout routine"* and watch the Room until the Turn ends.
2. While Coach is still streaming, try to tap an option. Note whether the options are enabled and what the card says.
3. After the Turn ends, run `O-TRANSCRIPT` and count the Messages Coach posted for that Turn.

**Pass if — all of these**

- While Coach streamed, the options were disabled and the card said *"Coach is still writing…"*.
- Coach posted its framing and **nothing after the tool call**: no routine, no invented answers, no "assuming 3 days".
- Once the Turn ended, the options became enabled.

**Fail if — any of these**

- Coach wrote a routine in the same Turn as the call -> it guessed; the success text is not being obeyed. Record the model and the text.
- The options were enabled while Coach was still streaming -> the Draft check on `QuestionCard` is not reaching the Room view.

### QUESTIONS-03 — A factual question and a request for an opinion do not call `ask_human`

**Paid** 💰 · about 15 min

*Proves the description's "do not use it" clauses hold in a real model. Spec QM-3.*

**Before you start**

- As QUESTIONS-01.

**Steps**

1. Send Coach *"What is the capital of France?"* and wait for the Turn to end.
2. Send *"Should I learn Python or JavaScript?"* and wait for the Turn to end.
3. Look for a card after each, and run `O-TRANSCRIPT`.

**Pass if — all of these**

- Coach answered both directly, in the Room.
- No card appeared after either.

**Fail if — any of these**

- A card appeared for the first question -> the description is not restraining the model enough on a plain fact.
- A card appeared for the second -> likewise, for a request for an opinion. Either is a wording change to the Prompt, not a code defect: record what it asked.

### QUESTIONS-04 — The same on a Teammate running on `agency-acp`

**Paid** 💰 · about 20 min

*Proves, or records the failure of, the tool's nested array schema through a second Adapter's MCP client (E-10). Needs a second Adapter Profile (setup step 4). Spec QM-4.*

**Before you start**

- A Teammate on the `agency-acp` Adapter, in a Room with you.

**Steps**

1. Send it *"Help me plan a workout routine"*.
2. Read the Room and the card, and `O-LOG`.

**Pass if — either of these**

- As QUESTIONS-01 Pass.
- The Adapter cannot call the tool, and the failure and its wording are written in [Adapters live findings](../../Huddle.Adapters-LiveFindings.md).

**Fail if — any of these**

- The Teammate called the tool, got a success, and no card appeared -> the arguments were mangled in transit; compare the wire with the schema.

### QUESTIONS-05 — In a Room of three, an answer wakes only the asker

**Paid** 💰 · about 20 min

*Proves the Reply Gate, not a bespoke delivery, wakes the asker, and that an unmentioned Member reads the answer as Catch-up. Spec QM-5.*

**Before you start**

- A Room with you, Coach, Nova and Sable. Budget `4` from the launch.

**Steps**

1. Send *"@Coach help me plan a workout routine"*. Wait for the card.
2. Tap an option on each Question and press **Send** (or tap once if the card holds one Question).
3. Watch which Teammates start a Turn, and run `O-TRANSCRIPT`.
4. Then send *"@Nova what did Coach just ask me?"*.

**Pass if — all of these**

- The answer is a Message from **You** quoting each Question, with the choice under it and `@Coach` last.
- Only Coach starts a Turn after the answer; Nova and Sable do not.
- Nova's reply in step 4 shows it read the answer as Catch-up.

**Fail if — any of these**

- Nova or Sable replied to the answer -> the Mention is not the only thing waking them.
- The answer is not rendered under each quoted Question, but inside the quote -> the blank line between a quote and its answer has been lost.

### QUESTIONS-06 — A Claude Teammate has no built-in `AskUserQuestion`

**Paid** 💰 · about 20 min

*Proves §6.8 against a live model, not just the vendored source: with no `elicitation` capability advertised, the Adapter keeps Claude's built-in option picker off, so `ask_human` is the only way to ask and the Turn never waits on an `elicitation/create` nothing answers. Spec QM-6.*

**Before you start**

- Coach on the stock Claude Adapter, with the wire trace on (setup step 5).

**Steps**

1. Send Coach *"Use your AskUserQuestion tool to ask me my favourite colour"*.
2. Wait for the Turn to end, or for the idle timeout, whichever is first. Note which.
3. Search `O-LOG` for `elicitation`.

**Pass if — all of these**

- Coach said it has no such tool, or asked with `ask_human` and a card appeared.
- No second card appeared, and the Turn ended on its own, with no idle-timeout line in `O-LOG`.
- `initialize` in the trace carries no `elicitation` key, and no `elicitation/create` request appears.

**Fail if — any of these**

- An `elicitation/create` request appears -> something advertised the capability; find it before anything else (D-13).
- The Turn hung until the idle timeout -> a request is open that nothing answers; this is the failure D-13 exists to prevent.
- Coach claimed to call `AskUserQuestion` and nothing visible happened -> record the wire; the Adapter's gate may have changed in a newer version.

---

Back to [the manual test script](../manual-tests.md).
