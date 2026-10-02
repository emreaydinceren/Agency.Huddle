# Questions

Prove, in a real browser against a real model, that an Agent asks the Human with `ask_human` when it should and does not when it should not; that it ends its Turn after asking and does not guess the answer; that the card appears only after the asker's framing Message; that an answer wakes only the asker in a Room of three; and that a Claude Teammate has no second, built-in way to ask. The automated suite proves the card, the store, the tool's refusals and the Reply Gate; this page proves what only a model can show: whether it calls the tool at the right moment, ends its Turn when told to, and does not reach for `AskUserQuestion`. **Written and run once on 2026-10-01, in the app** (Haiku, Budget 4, a scratch data directory): 02, 05 and 06 pass, 03 passes weakly, 04 is inconclusive for want of an `agency-acp` Profile, and 01 **failed** until `systemPrompt.askHuman` was added, then passed on a re-run the same day (one sample). Why it failed: the Claude Adapter defers every MCP tool, so the model never read `ask_human`'s description; see the [tracker](tracker.md#questions) and the spec's header note. All six spend money, because each one needs a Turn.

**10 tests** · 1 free, 9 paid 💰 · about 3 hours.

QUESTIONS-01 to QUESTIONS-06 are the `ask_human` tests. **QUESTIONS-07 to QUESTIONS-10 cover the elicitation bridge** ([the spec](../../Huddle.Questions-Specifications.md) §6.8a): a form an Agent's tool puts to the Human over ACP elicitation, shown as an *Elicitation card* (QUESTIONS-07 needs no model; the other three need a Turn). They were written and run on 2026-10-01, in the app, against the scripted `mock-acp` adapter and against a real Claude Adapter on Haiku.

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
| QUESTIONS-06 | QM-6 | Paid 💰 | With `Acp:AdvertiseElicitation` **off**, does a Claude Teammate have no `AskUserQuestion`, and does the Turn not hang (§6.8) |
| QUESTIONS-07 | QM-7 | Free | Does a scripted Adapter's form reach the Human as a card, with typed fields and the right typed answer (§6.8a) |
| QUESTIONS-08 | QM-8 | Paid 💰 | With the default **on**, does Claude's `AskUserQuestion` reach the Human as a card, does the Turn survive the wait, and does the answer land in the Transcript without waking the asker |
| QUESTIONS-09 | QM-9 | Paid 💰 | Do Skip and Stop end a waiting form cleanly |
| QUESTIONS-10 | QM-10 | Paid 💰 | Is an unanswered form dropped after `Acp:UserInputTimeoutSeconds` |

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
   The trace shows an elicitation request as **`_elicitation/create`**, because the host renames it so
   `dotacp` routes it (spec §6.8a).
6. QUESTIONS-07 to QUESTIONS-10 use `Acp:AdvertiseElicitation`, which defaults to **`true`**: do not set it
   to `false` for them. QUESTIONS-07 is **free** and runs on the scripted adapter: launch with
   `Team__Acp__Enabled=true` and an Adapter Profile whose only entry is `mock-acp` (the executable
   `src/Huddle.MockAdapter/bin/Debug/net10.0/mock-acp.exe`, with `Args` set to any one value such as
   `--mock`, and `UsesToolNamePrefix` false; see [adapters.md](adapters.md)). It spends nothing, because
   no real model is started. A message to a Teammate on it that contains `[elicit:form]`, `[elicit:ask]`,
   `[elicit:one]`, `[elicit:refusal]` or `[elicit:unsupported]` makes it put that form to you and reply with
   exactly what it received. QUESTIONS-08 to QUESTIONS-10 need the stock Claude Adapter and a scratch
   `Team__DataDir`, with Coach on **Haiku**. Point `Team__Acp__AdapterPath` at the installed
   `claude-agent-acp` entry point if you run from a worktree.
7. QUESTIONS-08 shortens the idle bound so the pause is visible: set `Team__Acp__TurnIdleTimeoutSeconds` to
   `20`. QUESTIONS-10 sets `Team__Acp__UserInputTimeoutSeconds` to `30` (its minimum). "The form card" means
   the panel headed *"<Name> is asking"* with typed fields and **Send** and **Skip**, between the
   Transcript and the composer.

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
- The Adapter cannot call the tool, and the failure and its wording are written in Adapters live findings.

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

*Proves the off switch of §6.8 against a live model, not just the vendored source: with `Acp:AdvertiseElicitation` false no `elicitation` capability is advertised, so the Adapter keeps Claude's built-in option picker off, `ask_human` is the only way to ask and the Turn never waits on an `elicitation/create` nothing answers. With the shipped default (on) the tool exists and is bridged: that is QUESTIONS-08. Spec QM-6.*

**Before you start**

- Coach on the stock Claude Adapter, with the wire trace on (setup step 5), launched with **`Team__Acp__AdvertiseElicitation=false`** (the default is `true`, and with it this test's expectations do not hold).

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

### QUESTIONS-07 — A scripted Adapter's forms reach the Human as cards, with the right types

**Free** · about 25 min

*Proves the Elicitation card, the wire rename and the Transcript Message without a model: every field kind, the answer's JSON types, the two cancel paths, and the forms the app must decline. Spec QM-7.*

**Before you start**

- The free scripted lane (setup step 6). A Teammate on `mock-acp` (the Chief of Staff is one on a fresh data directory) in a Room with you. The window at least 720 pixels tall.

**Steps**

1. Send `[elicit:form] plan this`. The card "Plan the delivery" appears. Try **Send** with only Title filled: it is disabled. Fill Title `Move the office`, Due date `2026-10-15`, Boxes `25`, and note that **Send stays disabled** (the schema's maximum is 20, and the field says so); change Boxes to `7`, Budget `12.5`, switch **Insured** on, choose **High**, tick **Fragile** and **Cold chain**, type `Pianos <b>first</b> & "quotes"` in Notes, and press **Send**.
2. Read the Transcript and the Teammate's reply.
3. Send `[elicit:ask] two`. In the first question tap **Green**, then type `Teal` in **Other**. Tick two options of the second question and press **Send**.
4. Send `[elicit:unsupported]`, then `[elicit:one]` (press **Skip**), then `[elicit:refusal]` (choose the second option and press **Send**).
5. Send `[elicit:ask] again`, and while its card is open type `never mind` in the composer and send it.

**Pass if — all of these**

- Step 1: the card is capped well inside the window, its fields scroll, and **Send** and **Skip** stay in view with the composer below. The Transcript holds a Message from you that quotes each field's label (muted, barred) and its answer. The reply reads `Got action=accept content=` followed by JSON with `"count":7`, `"budget":12.5`, `"agree":true`, `"priority":"high"`, `"tags":["fragile","cold"]`, `"due":"2026-10-15"` and the notes with their `<b>` shown as text, never as bold.
- Step 3: tapping into **Other** clears the first question's selection and disables its options. The reply's content holds `question_0_custom` `Teal` and **no** `question_0`, and `question_1` as an array of the two options.
- Step 4: `[elicit:unsupported]` shows **no card** and the reply says `action=decline content=none`. `[elicit:one]` shows the question **once** (not as both a heading and a label) and **Skip** posts nothing, the reply saying `action=decline`. The refusal form shows no raw heading `choice`, its Transcript Message quotes the question text, and the reply holds `{"choice":"cancelled"}`.
- Step 5: the card disappears at once, the reply says `action=cancel content=none`, and your typed Message gets its own, normal reply.
- A multi-select's options stand in one left-aligned column.

**Fail if — any of these**

- Send, Skip or the composer cannot be reached on a 720-pixel window -> the card's height cap is not applying; check that its rules are in `app.css`, not a scoped stylesheet.
- A number or boolean arrives as text (`"7"`, `"true"`) -> the answer's typing is broken (D-17).
- The Teammate replied to the Transcript Message as if you had spoken to it -> the answer is being delivered to the asker (D-16).
- A card stays after step 5 -> a typed Message no longer ends a waiting form.

### QUESTIONS-08 — Claude's `AskUserQuestion` reaches the Human, and the Turn survives the wait

**Paid** 💰 · about 20 min

*Proves, against a live model, the three gates of §6.8a at once: Claude's built-in tool arrives as a card (G2), the Turn is not killed by the idle bound while the card waits (G1), and the answer lands in the Transcript as a Message without waking the asker a second time (G3). Spec QM-8.*

**Before you start**

- Paid lane (`P-LAUNCH-PAID`) with the default `Acp:AdvertiseElicitation`, `Team__Acp__TurnIdleTimeoutSeconds=20`, and the wire trace on (setup step 5). Coach on Haiku, in a Room with you.

**Steps**

1. Send Coach *"Use your AskUserQuestion tool to ask me my favourite colour. Offer red, green and blue."*
2. When the form card appears, leave it alone for **45 seconds**, watching the Room.
3. Choose **Green** and press **Send**.
4. Wait for the Turn to end. Read the Transcript, then search `O-WIRE` for the lines `"method": "session/prompt"` addressed to Coach's session.

**Pass if — all of these**

- Step 1: a card appears with the header *Colour*, the question, **Red**, **Green** and **Blue** with their descriptions, and an **Other** box. The question is shown **once**.
- Step 2: after 45 seconds, more than twice the 20-second bound, the card is still there, **Stop** is still offered on Coach's Draft, and there is no hung-Adapter line in `O-LOG`.
- Step 3: the card disappears. The Transcript holds a Message from you quoting the question and ending with `Green`. Coach continues **in the same Turn**, replying that green is your favourite colour.
- Step 4: the trace holds `"method": "_elicitation/create"` (the Adapter's request, renamed) with `question_0` as a `oneOf` of three and `question_0_custom`, and Coach's session received **exactly one** `session/prompt`: yours.

**Fail if — any of these**

- The Turn was cancelled during step 2 -> the idle watchdog is not paused (G1).
- No card appeared and Coach said the tool is unavailable -> elicitation is not being advertised; check `Acp:AdvertiseElicitation` and the `initialize` in the trace.
- A second `session/prompt` for Coach follows your answer, or Coach answers twice -> the Transcript Message woke the asker (D-16).

### QUESTIONS-09 — Skip and Stop end a waiting form cleanly

**Paid** 💰 · about 20 min

*Proves that neither way of leaving a waiting form leaves the Persona stuck or its Adapter hung. Spec QM-9.*

**Before you start**

- As QUESTIONS-08, without the shortened idle bound being needed.

**Steps**

1. Send Coach *"Use your AskUserQuestion tool to ask me whether I prefer morning or evening workouts."* When the card appears press **Skip**.
2. Send Coach *"Use your AskUserQuestion tool to ask me which day I train: Monday, Wednesday or Friday."* When the card appears press **Stop** on Coach's Draft.
3. Send Coach *"Say hello back in exactly five words."*

**Pass if — all of these**

- Step 1: the card disappears, nothing is posted to the Transcript as you, and Coach carries on in the same Turn, saying it asked and will wait (it was told you skipped).
- Step 2: the card disappears, **Stop** disappears, the Turn ends, and no failure alert appears.
- Step 3: Coach replies normally, so its Turn slot and session are intact.

**Fail if — any of these**

- The card stays after Stop -> a cancelled Turn does not drop its form.
- Coach does not reply in step 3 -> the slot or session was left held.

### QUESTIONS-10 — An unanswered form is dropped after `Acp:UserInputTimeoutSeconds`

**Paid** 💰 · about 15 min

*Proves the bound that replaces the idle timeout while a form waits: an absent Human cannot freeze a Persona. Spec QM-10.*

**Before you start**

- As QUESTIONS-08, with **`Team__Acp__UserInputTimeoutSeconds=30`** (its minimum) and `Team__Acp__TurnIdleTimeoutSeconds=20`.

**Steps**

1. Send Coach *"Use your AskUserQuestion tool to ask me which exercise I like best: squats, lunges or planks."*
2. When the card appears, leave it alone and wait **45 seconds**.
3. Read the Room.

**Pass if — all of these**

- After about 30 seconds the card disappears on its own.
- Coach replies that the tool use did not go through and offers to ask again, and the Turn ends.
- No hung-Adapter line appears in `O-LOG`, and no failure alert in the Room.

**Fail if — any of these**

- The card is still there after a minute -> the bound is not applied.
- A hung-Adapter or idle-timeout line appears -> the bound ended the wait by failing the Turn instead of cancelling the request.

---

Back to [the manual test script](../manual-tests.md).
