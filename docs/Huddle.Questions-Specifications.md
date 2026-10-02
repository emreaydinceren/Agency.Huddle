# Huddle.Questions — Design Specification

**Date:** 2026-09-22 (revised 2026-10-01) · **Status:** Built 2026-10-01 · **Decision record:**
[ADR-0022](adr/0022-an-agent-asks-the-human-with-a-question.md) · **Vocabulary:**
[language.md](engineering/language.md) (**Question**)

This is the design for `ask_human`, an App Tool that lets an Agent put one to three
multiple-choice Questions to the Human. They appear on a card in the Room as options to tap,
and the Human's answer is posted as a Message from the Human, which wakes the Agent. It is
Huddle's own version of the option-picker Claude's apps offer, built on the same machinery as
the Skills spec's Proposal.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Read §5 for the shape, §6 for the subsystems, and Appendix A for the ordered,
test-first task list. Every design decision is recorded in §11 with the alternative it beat.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides either.

> [!NOTE]
> **Built 2026-10-01.** Every task in Appendix A landed, in order. Three things differ from what
> was drafted, and the text below says so where each matters. The answer has a **blank line between each
> quoted Question and its answer** (§6.4): directly under `> question`, CommonMark reads the answer as a
> lazy continuation of the quote and renders it inside the Agent's words, and a test now fails without
> the blank line. **Q-G1 lives in `tests/Huddle.Tests/Conformance/PersonaHostTests.cs`**, not in
> `Huddle.Acp.Tests`: it drives the real factory against the ACP effort's `FakeAcpAgent`, so it touches
> nothing in their tree and needed no announcement. And `dotacp` 2026.7.19's stable `ClientCapabilities` has
> no `Elicitation` member, so the `elicitation` half of the guard could not be proved by flipping a
> member; the elicitation bridge below got around that without a library upgrade. The six paid manual tests are written
> ([questions.md](engineering/manual-tests/questions.md)) and were run once, 2026-10-01, in the app.
>
> **What that run found.** The card, the answer, the wake, D-8 and §6.8 all held against a real model
> (QUESTIONS-02, 05 and 06 pass). **D-11 as first written did not.** On the Claude Adapter every `mcp__team__*` tool is a
> *deferred* tool: the model sees only its name until it calls ToolSearch, so the description that carries
> all of "when to ask and when not to" is never read. Haiku, given the exact QM-1 prompt, wrote its
> questions as a list twice; told to call `ask_human` by name, it did so correctly. A fix has to put a
> sentence about when to ask where the model always sees it (the system prompt's tool roster is the
> candidate), or stop the Adapter deferring. **The first is built** (§6.6, D-11): `systemPrompt.askHuman`, one
> paragraph after the tools paragraph. QUESTIONS-01, re-run against it the same day, **passed** (one sample: a
> fresh Haiku Teammate called ToolSearch and then `ask_human` unprompted).
> The run also added a muted, barred style for a quoted line in a Message (`app.css`), because an answer
> and the question it quotes were otherwise indistinguishable.

> [!NOTE]
> **The elicitation bridge, built 2026-10-01 (§6.8a).** `ask_human` is still the primary way to ask, and
> its answer is still an ordinary Message (D-2). Beside it Huddle now advertises ACP
> `clientCapabilities.elicitation.form` (`Acp:AdvertiseElicitation`, on by default), so the Claude Adapter
> turns on `AskUserQuestion`, the retry-after-refusal dialog and MCP forms, and every `elicitation/create`
> is answered with a card in the Room. The three gates D-13 set are met: the idle watchdog pauses while a
> request is open, no request is left unanswered, and the answer is written to the Transcript as a Message.
> Three things differ from what the earlier text assumed. `dotacp` 2026.7.19 needed **no upgrade**: its
> typed `unstable` elicitation request drops `sessionId`, `requestedSchema` and the answer's `content`, so
> Huddle stays on the stable types, adds the capability through a subclass and renames the inbound request
> so dotacp routes it. The answer **cannot be an ordinary Human Message** in a one-to-one Room, because the
> Reply Gate answers every Human Message there and would queue a second Turn behind the open one, so it is
> posted through `ChatService.PostHumanAnswerAsync`, which withholds it from the asker. And **looking at
> the card in a browser found two defects the suite cannot see**, both fixed: the card had no height limit
> (its CSS lived in a scoped stylesheet whose `::deep` rules can never match a Mud component), and a Room
> whose Transcript was taller than the window could not be scrolled at all (§6.8a, "What looking at it found").

> [!NOTE]
> **Sequencing.** The Skills work (S2) landed on 2026-09-22, so `ProposalStore` and
> `ProposalCard` now exist; copy their patterns rather than inventing parallel ones. The Proposal
> is referred to throughout as the precedent; see
> [Huddle.Skills-Specifications.md](Huddle.Skills-Specifications.md) §6.9–§6.11. Four files are
> still shared with other work: the tool list in `Acp/DotAcpAgentHostFactory.cs`,
> `Prompts/PromptCatalog.cs`, `Services/ChatService.cs` and `Components/Pages/Chat.razor`. Work
> Modes phase 3 (plan approval, [WorkModes spec](Huddle.WorkModes-Specifications.md)) will add a
> third card beside `ProposalCard`, so it touches `Chat.razor` as well; whichever lands second
> rebases.

---

## 1. Goal

When an Agent needs the Human's preferences before it can help, let it ask with options the
Human taps, rather than with a list of questions the Human has to answer in prose.

Concretely:

1. **`ask_human` asks only the Human.** It takes no recipient. An Agent that wants something
   from another Teammate Mentions them in an ordinary Message, as today.
2. **The answer is an ordinary Message from the Human.** It quotes each Question and its
   answer, and Mentions the Agent that asked, so the Reply Gate wakes it in any Room. Nothing
   new crosses the pipe.
3. **The tool returns at once, and the asker ends its Turn.** The answer arrives later, as
   the Human's next Message in that Room, not as the tool's result.
4. **Tapping beats typing.** A single Question with a single answer is sent by one tap.

**Why this matters.** Interviewing is most of what the Chief of Staff does before it proposes
a team: one-off or recurring, how involved, practice or real work, how much to spend. Today
each answer costs the Human a typed sentence and costs the Agent a guess at what the sentence
meant. Fixed options make both cheap, and make the answer unambiguous.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| Q1 | The Human writes *"Help me plan a workout routine"* to Coach | Coach replies with one framing sentence and calls `ask_human` with a goal Question (Strength / Cardio / Weight loss) and a time Question. A card appears above the composer |
| Q2 | The Human taps Strength and 3 days, then **Send** | A Message from the Human is posted, quoting both Questions with the answers, ending `@Coach`. Coach wakes and writes the routine |
| Q3 | The card holds one `single_select` Question | One tap on an option posts the answer. There is no Send button |
| Q4 | The Human types *"Actually I have a bad knee"* instead of tapping | That Message is posted as usual and the card disappears. Coach reads the Message as the answer |
| Q5 | The Human clicks **Dismiss** | The card disappears and nothing is posted. Coach is not woken |
| Q6 | Coach calls `ask_human` again while its card waits | The new Questions **replace** the waiting ones |
| Q7 | Nova calls `ask_human` in a Room where Coach's card waits | Refused: *"Questions from Coach are already waiting in this Room. Wait for the Human to answer them."* |
| Q8 | An option reads `@Coder go ahead` | Refused. The answer is posted as the Human, so a Mention in it would speak for the Human |
| Q9 | The Room has spent its Budget | Refused, worded as terminal, like `post_message`'s Budget refusal |
| Q10 | A `rank_priorities` Question: Cost / Speed / Quality | The card lists the three with up and down buttons; **Send** posts `1. Speed · 2. Cost · 3. Quality` |
| Q11 | The Human taps an option while Coach's Turn is still streaming | The options are disabled until Coach's Turn ends, so Coach's framing Message is posted before the answer |
| Q12 | Two browser tabs answer the same card | The first wins. The second finds nothing, posts nothing, and its card disappears |
| Q13 | The app restarts with a card waiting | The card is lost. Coach is not told; the Human types the answer instead |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **Asking another Agent** | Agents already read Messages. Options exist to save a person typing; an Agent gains nothing from them |
| **A free-text "Other" option on the card** | The composer is the Other. Typing a Message is always available and drops the card (Q4) |
| **Persisting a card across restarts** | In memory, like a Proposal or a Budget. A restart losing it is acceptable because the Human can type the answer |
| **Changing an answer after sending it** | The answer is a Message, and a Transcript is append-only (ADR-0002). The Human sends a correction as another Message |
| **Expiring a card after a period** | V1 keeps it until it is answered, dismissed, replaced or dropped |
| **Images, descriptions or icons on an option** | Short labels only. The Agent's framing Message carries any explanation |
| **Drag-and-drop ranking** | Up and down buttons work with a keyboard and a screen reader, and need no JavaScript. V2 may add drag |
| **A new Envelope or a `ProtocolVersion` bump** | The card never crosses the pipe (`traps.md`, the closed polymorphism rule) |
| **ACP elicitation (`elicitation/create`) as the way `ask_human` works** | **Built beside `ask_human`, not instead of it.** A form the Adapter puts to the Human (Claude's built-in `AskUserQuestion`, the retry-after-refusal dialog, an MCP server's form) is shown as an `ElicitationCard`, and the answer is still written to the Transcript as a Message. `ask_human` stays the primary way to ask, because it returns at once and its answer is an ordinary Message (D-2). See [The elicitation bridge](#68a-the-elicitation-bridge) |

---

## 4. Design principles

1. **Everything an Agent knows arrives through a tool or a Message.** The answer is a
   Message; the Agent reads it like any other. No new Envelope, no privileged path, no
   ambient "current Room".
2. **Text never guards; code does.** The tool description asks for restraint, but the refusals
   that matter are in C#: no `@`, no paused Room, no second asker, and limits on counts and
   lengths.
3. **Words posted as the Human are the Human's words.** The Agent writes the options, but only
   the Human's tap posts them. That is why no option may Mention anyone, and why nothing is
   posted on Dismiss.
4. **Copy the Proposal, do not generalise it.** A Question card and a Proposal card look alike,
   but have different lifetimes: a typed Message drops a Question but deliberately leaves a
   Proposal waiting for revision. Two small stores are simpler than one store with a mode.
5. **Simple over complete.** No abstraction a current feature does not need.

---

## 5. Architecture overview

```text
 Agent's Turn
   │  framing text ─────────────────────────────────────────▶ posted at Turn end (as the Agent)
   │  ask_human(roomId, questions)
   ▼
 AskHumanTool ── checks ──▶ QuestionStore.TryPut ──▶ RoomEvents.QuestionsChanged(roomId)
   │  returns "Asked… end your Turn now"                          │
                                                                  ▼
                                               Chat.razor ── QuestionCard (options, Send, Dismiss)
                                                                  │  disabled while the asker
                                                                  │  has a Draft in this Room
                                                    Send / tap    ▼
                                               QuestionService.AnswerAsync
                                                 ├─ QuestionStore.TryTake   (first tap wins)
                                                 └─ ChatService.PostAsync   (as the Human)
                                                        │  "> …\nStrength\n\n@Coach"
                                                        ▼
                                               MessagePosted ─▶ Reply Gate ─▶ wakes the asker

 Any other Human Message in the Room ─▶ ChatService.PostAsync ─▶ QuestionStore.Drop(roomId)
 Archive or delete the Room          ─▶ QuestionStore.Drop(roomId)
```

| Component | Kind | New or changed |
| --- | --- | --- |
| `Questions/Question.cs` | Records: `Question`, `QuestionKind`, `PendingQuestions` | New |
| `Questions/QuestionStore.cs` | Singleton, in memory | New |
| `Questions/QuestionService.cs` | Scoped or singleton service | New |
| `Acp/Tools/AskHumanTool.cs` | `IAppTool`, one per Persona | New |
| `Components/Shared/QuestionCard.razor` | Component | New |
| `Services/RoomEvents.cs` | Gains `QuestionsChanged` | Changed |
| `Services/ChatService.cs` | Drops a waiting card on a Human post, on archive and on delete | Changed |
| `Acp/DotAcpAgentHostFactory.cs` | Adds `ask_human` to every Persona's tools | Changed |
| `Prompts/PromptCatalog.cs`, `prompts.default.json` | Gains `tool.askHuman.description` | Changed |
| `Components/Pages/Chat.razor` | Renders `QuestionCard` and subscribes to `QuestionsChanged` | Changed |

---

## 6. Components

### 6.1 The records

```csharp
public enum QuestionKind { SingleSelect, MultiSelect, RankPriorities }

/// <summary>One multiple-choice Question an Agent puts to the Human.</summary>
public sealed record Question(string Text, IReadOnlyList<string> Options, QuestionKind Kind);

/// <summary>The one to three Questions waiting on one card in one Room.</summary>
public sealed record PendingQuestions(
    string Id,                       // Guid "N"; identifies this card, so a stale tap finds nothing
    string RoomId,
    string AskerAgentId,
    string AskerName,                // at ask time; the posted Mention re-resolves it (§6.4)
    IReadOnlyList<Question> Questions,
    DateTimeOffset AskedAt);         // from the injected TimeProvider

/// <summary>The Human's answer to one Question: option indexes, in the order that matters.</summary>
public sealed record QuestionAnswer(IReadOnlyList<int> Chosen);
```

`QuestionKind` is an enum rather than a string (the "make illegal states unrepresentable"
principle). The wire values `single_select`, `multi_select` and `rank_priorities` are parsed into
it once, in the tool.

### 6.2 `ask_human` — the App Tool

**Arguments.**

```json
{
  "type": "object",
  "properties": {
    "roomId": { "type": "string" },
    "questions": {
      "type": "array", "minItems": 1, "maxItems": 3,
      "items": {
        "type": "object",
        "properties": {
          "question": { "type": "string" },
          "options":  { "type": "array", "minItems": 2, "maxItems": 4, "items": { "type": "string" } },
          "type":     { "type": "string", "enum": ["single_select", "multi_select", "rank_priorities"] }
        },
        "required": ["question", "options"]
      }
    }
  },
  "required": ["roomId", "questions"]
}
```

`type` defaults to `single_select`. The schema's limits are advisory for the model; the checks
below are the rule.

**Checks, in order.** Every check returns a string, and none throws for an expected failure:

```text
 roomId, questions present?             no  → "Both 'roomId' and 'questions' are required arguments."
 Room exists?                           no  → "Unknown room 'x'."
 caller is a Member?                    no  → "You are not a Member of that Room."
 Room Archived?                         yes → "That Room is Archived."
 Room's Budget spent?                   yes → "That Room is paused: its Budget is spent. Do not retry;
                                               nothing can be asked there until the Human speaks."
 1 to 3 questions?                      no  → "Ask 1 to 3 questions; you asked 5."
 each question, each option             bad → one line per problem (below), all reported at once
 QuestionStore.TryPut
     Refused(existing)                  → "Questions from Nova are already waiting in this Room.
                                            Wait for the Human to answer them."
     Stored | Replaced                  → QuestionsChanged(roomId) → the success text
```

Problems, one line each, prefixed with their position (`Question 2:`, `Question 2, option 3:`):

| Rule | Problem text |
| --- | --- |
| Question text is 1 to 200 characters after trimming | `is empty.` / `is 240 characters; the limit is 200.` |
| Options: 2 to 4 | `give 2 to 4 options; it has 5.` |
| Each option is 1 to 60 characters after trimming | `is empty.` / `is 75 characters; keep options short, the limit is 60.` |
| Options are distinct, ignoring case | `repeats option 1.` |
| No line break in a question or an option | `must be one line.` |
| **No `@` in a question or an option** | `must not contain '@'. The answer is posted as the Human, so a Mention in it would speak for them.` |
| `type` is one of the three | `type must be single_select, multi_select or rank_priorities.` |

**Success text.**

```text
Asked the Human 2 questions in Room 'Workout' (id 01H...). End your Turn now, and do not guess
the answers. The answer will arrive later as a Message from the Human in that Room, quoting each
question. If they write their own reply instead, that reply is the answer.
```

A replacement starts `Replaced your waiting questions.` and continues the same way.

**Implementation notes.**
- Constructed per Persona with the caller's Agent id, exactly like `PostMessageTool`.
- The Budget check reads `ChatService.GetBudget(roomId).Exhausted`. It is advisory, not
  check-then-act critical: a Room that pauses after the check still cannot be answered without
  a Human tap, which is the Human speaking.
- The refusal texts are tool results, not Prompts, and live as constants in the tool, matching
  `PostMessageTool`. Only the description is a Prompt (§6.6).

### 6.3 `QuestionStore`

**Purpose.** Hold at most one waiting card per Room.

```csharp
public sealed class QuestionStore(RoomEvents events)   // public only so ChatService's public constructor can take one
{
    internal PendingQuestions? Get(string roomId);
    internal QuestionPut TryPut(PendingQuestions pending);      // Stored | Replaced | Refused(existing)
    internal PendingQuestions? TryTake(string roomId, string id);   // first tap wins
    internal bool Drop(string roomId);                          // Human Message, Dismiss, archive, delete
}
```

- A `Dictionary<string, PendingQuestions>` under a `private readonly Lock gate`, keyed by Room
  id with `StringComparer.Ordinal`. All operations are O(1).
- **Replacement** (§8.1): the same `AskerAgentId` replaces; any other asker is refused.
- `TryTake` matches the card `Id` as well as the Room, so a tap on a card that was replaced in
  the meantime finds nothing instead of answering the new Questions with the old indexes.
- `QuestionsChanged(roomId)` is raised **outside** the lock, after every change, including a
  `Drop` that removed something. A `Drop` that found nothing raises nothing.
- A leaf singleton: it depends only on `RoomEvents`, so `ChatService` can call it without a
  cycle, as it will call `ProposalStore`.

### 6.4 `QuestionService` — Answer and Dismiss

```csharp
internal sealed class QuestionService(
    QuestionStore questions, ChatService chat, ITeamDirectory directory, ILogger<QuestionService> logger)
{
    internal Task<string?> AnswerAsync(string roomId, string id, IReadOnlyList<QuestionAnswer> answers, CancellationToken ct);
    internal void Dismiss(string roomId, string id);
}
```

**Answer.**

```text
 p = questions.TryTake(roomId, id)            null → return null; post nothing (another tab won)
 answers match p?                             no  → throw InvalidOperationException (a UI bug:
                                                     the card builds them, the Human cannot)
 asker = directory's Agent by p.AskerAgentId  gone → no Mention
 text  = Compose(p, answers, asker?.Name)
 await chat.PostAsync(roomId, human.Id, text, ct: ct)  → MessagePosted → Reply Gate wakes the asker
 return text
```

`TryTake` runs **before** `PostAsync`, so the Human-post hook in §6.5 finds nothing to drop and
raises nothing twice. `PostAsync` takes an optional `messageId` before the token, so the token is
passed by name.

"Answers match" means one `QuestionAnswer` per Question, and:

| Kind | `Chosen` must be |
| --- | --- |
| `SingleSelect` | exactly one valid index |
| `MultiSelect` | one or more distinct valid indexes, in option order |
| `RankPriorities` | every index exactly once, most important first |

**The posted text.** The answer is Human-authored, so it is **interface copy, not a Prompt**,
and lives as a constant format in `QuestionService`, as the Proposal outcomes do. Each Question
is quoted, then answered, then the asker is Mentioned on the last line:

```markdown
> What is your main goal?

Strength

> Which days can you train?

Monday, Wednesday, Friday

> Rank what matters most

1. Speed · 2. Cost · 3. Quality

@Coach
```

- **A blank line separates each quote from its answer.** The first draft of this spec had the answer on
  the line directly under the quote. In CommonMark that is a lazy continuation of the blockquote, so the
  Human's answer rendered inside the Agent's quoted question. `QuestionServiceTests.Answer_RenderedAsMarkdown_KeepsTheAnswerOutsideTheQuote`
  fails without the blank line.
- Multiple choices are joined with `, `; a ranking is `1. … · 2. …` on one line, so the
  Markdown renderer does not turn it into a list that loses the numbers' meaning.
- The Mention uses the asker's **current** Name, resolved from `AskerAgentId` at post time,
  so an asker renamed while its card waited is still woken. An asker that no longer exists
  gets no Mention, and the line is omitted.
- The Mention is there even in a Room of two, where it is not needed to wake anyone. That keeps
  the text the same in every Room, and says in the Transcript who asked.
- Posting as the Human resets the Room's Budget (ADR-0006). That is correct: the Human just
  spoke. The `@` refusal in §6.2 is what stops this post speaking for the Human to anyone else.

**Dismiss.** `questions.TryTake(roomId, id)`, and nothing else. Nothing is posted and the asker
is not woken. The Human can still type.

**Failure.** If `PostAsync` throws a `ChatException`, for example because the Room was deleted
mid-tap, log it and return `null`. The card is already gone, which is right: the Room it
belonged to may be too.

### 6.5 Lifetime: what drops a waiting card

| Event | Where | Effect |
| --- | --- | --- |
| Answered | `QuestionService.AnswerAsync` | Taken, then the answer is posted |
| Dismissed | `QuestionService.Dismiss` | Taken; nothing posted |
| Replaced | `QuestionStore.TryPut`, same asker | The new card replaces the old |
| **Any other Human Message in the Room** | `ChatService.PostAsync`, after the Message is appended, when the sender is the Human | `QuestionStore.Drop(roomId)` |
| Room Archived or deleted | `ChatService.SetRoomArchivedAsync(…, true)`, `DeleteRoomAsync` | `QuestionStore.Drop(roomId)` |
| App restart | — | Lost, by design |

**A typed Human Message drops the card** because it *is* the answer the asker will act on: in a
Room of two it wakes the asker at once. A card left on screen afterwards invites a second,
contradicting answer. The cost is accepted: a Human who types *"what does rank mean?"* loses the
card, and the Agent asks again (§11, D-4).

An Agent's Message does **not** drop the card. The asker's own framing Message arrives at the
end of its Turn, after the card appears, and must not remove it.

### 6.6 The tool description — `tool.askHuman.description`

A Prompt, `Timing: NextSession` like every other tool description, editable on Settings ›
Prompts. It carries the whole of when to use the tool, because a model decides whether to call
a tool from its description alone:

```text
Asks the Human one to three multiple-choice questions, shown in a Room as options they tap. Use it when you need their preferences, constraints or goals before you can help, such as which days, what budget, or which of these matters most, and you were about to write your questions out as a list. Do not use it when the answer is already in the conversation or can be inferred, for a fact you can look up, when they want your own recommendation or opinion, when they are venting, or when they have already given you detailed constraints. It asks only the Human: to ask another Teammate something, Mention them in an ordinary Message. Prefer one question, and write each so it makes sense on its own. Each has 2 to 4 short options that do not overlap, and a type: single_select (the default), multi_select, or rank_priorities. Always say in your reply why you are asking. After calling it, end your Turn: the answer arrives later, as a Message from the Human in that Room.
```

It names no tool, so it needs no placeholder and cannot break the `mcp__team__` rule in
`rules.md`. `PromptValidator` needs no new check.

**The same guidance, shorter, in the system prompt.** The paragraph above is the tool's *description*, and
the first live run (2026-10-01) showed a model may never read it: the Claude Adapter defers every MCP
tool, so the model sees `mcp__team__ask_human` as a bare name until it calls ToolSearch, and Haiku wrote
its questions as a list twice. So `SystemPromptComposer` also appends **`systemPrompt.askHuman`** straight
after the tools paragraph, for every Persona, whenever the session offers the tool:

```text
When you need the Human's preferences, constraints or goals before you can help, such as which days, what
budget, or which of these matters most, and you were about to write your questions out as a list, call
{{askHumanTool}} instead: it shows them as options the Human taps. Say in your reply why you are asking,
then end your Turn; the answer arrives later as a Message from the Human. Do not use it for a fact you
can look up or infer, when they want your own opinion, or when they have already given you the detail.
```

It *does* name the tool, so `{{askHumanTool}}` is filled in by code from the same instance the factory
registers, with the Adapter's prefix (`mcp__team__ask_human`, or bare on `agency-acp`); the template
never contains the prefix. The factory passes the name to `DotAcpPersonaHost`, which hands it to
`SystemPromptComposer.Compose`'s `askHumanToolName`; a caller that passes none gets the old output byte
for byte. It is a Prompt, `NextSession`, editable on Settings › Prompts, and the description stays: an
Adapter that does not defer tools reads both.

### 6.7 `QuestionCard` in the Room

**Placement.** Between the Transcript and the composer, with the Budget prompt and the
`ProposalCard`, and for the same reason: it is about the exchange just above it. A separate
component in `Components/Shared/`, so `Chat.razor` gains only a tag and a subscription.

**Header.** *"Coach is asking"*, using the asker's current Name, with **Dismiss** at the right.
Each Question's text is shown as written, with its options below it.

| Kind | Control | Rule |
| --- | --- | --- |
| `SingleSelect` | A row of chips; choosing one clears the others | — |
| `MultiSelect` | A row of chips that toggle | A caption: *"Choose any."* |
| `RankPriorities` | An ordered list; each row has up and down buttons | Starts in the Agent's order. A caption: *"Most important first."* |

- **One tap sends** when the card holds exactly one `SingleSelect` Question. There is no Send
  button in that case.
- Otherwise **Send** is enabled once every `SingleSelect` has a choice and every `MultiSelect`
  has at least one. A ranking is always complete, because it starts in a valid order.
- **Options are disabled while the asker has a Draft in this Room**, with the caption *"Coach
  is still writing…"*. `Drafts.ForRoom(roomId)` carries each Draft's `AgentId`, and
  `DraftChanged` already reaches the Room view. This is what makes the asker's framing Message
  land in the Transcript *before* the answer (Q11). An asker that called `ask_human` from a Turn
  in a different Room has no Draft here, so its card is enabled at once.
- Busy while `AnswerAsync` runs. The outcome is the posted Message, not card state; the card
  disappears on `QuestionsChanged`.
- Chips are at least 44 pixels tall and wrap, so the card works at phone width.
- Not rendered for an Archived Room.
- `role="group"` with an `aria-label` of the Question text for each Question. Chips expose
  `aria-pressed`, and the rank buttons read *"Move Speed up"*.

**Internal flow.** It mirrors the Proposal card and the Budget prompt:

```text
 OnInitialized     → pending = questions.Get(roomId); subscribe QuestionsChanged, DraftChanged
 QuestionsChanged  → InvokeAsync: pending = questions.Get(roomId); reset choices; StateHasChanged
 DraftChanged      → InvokeAsync: recompute askerIsWriting; StateHasChanged
 Send / tap        → busy = true → QuestionService.AnswerAsync(roomId, pending.Id, answers, ct)
 Dismiss           → QuestionService.Dismiss(roomId, pending.Id)
 Dispose           → unsubscribe both (rules.md: every RoomEvents subscriber unsubscribes)
```

The `[Parameter]` types are `public`, per `rules.md`; `QuestionKind` and the records are
public for that reason.

### 6.8 Claude's built-in `AskUserQuestion` and ACP elicitation

Claude Code ships its own option-picker, `AskUserQuestion`. Whether a Claude Persona sees it depends
on whether Huddle advertises ACP elicitation. Until 2026-10-01 it did not, so the Adapter kept the tool
off and `ask_human` had no competitor. It now advertises `elicitation.form` by default
(`Acp:AdvertiseElicitation`), the Adapter offers the tool, and §6.8a is how it, the retry-after-refusal
dialog and an MCP server's forms reach the Human. The section below is why that needed three gates, and
what the Adapter does. With `Acp:AdvertiseElicitation` off the old behaviour returns exactly.

| | `AskUserQuestion` | `ask_human` |
| --- | --- | --- |
| Owner | Claude Code, in its built-in tool preset. Claude Adapters only | Huddle's `AppToolServer`. Every Adapter |
| Turn | Blocks until the Human answers; Huddle pauses the idle watchdog for the wait and bounds it (§6.8a) | Returns at once; the asker ends its Turn |
| Answer | The tool result, and also a Message from the Human in the Transcript (G3) | A Message from the Human, in the Transcript |
| Shape | 1 to 4 Questions, 2 to 4 options with descriptions, `multiSelect`, and a free-text "Other" the Adapter adds | 1 to 3 Questions, three kinds including `rank_priorities`, no free text (§3) |

- **Why it was off.** `claude-agent-acp` 0.75.1 adds `AskUserQuestion` to the SDK's
  `disallowedTools` unless the client advertised `clientCapabilities.elicitation.form`
  (`dist/acp-agent.js`: the `disallowedTools` constant near line 5878, passed to the SDK near
  line 6007). Huddle advertised no `elicitation` capability, so a Claude Persona never saw the
  tool, and `ask_human` was the only way it asked. This was read from the vendored source on
  2026-09-30, and a live model confirmed it on 2026-10-01 (QUESTIONS-06, QM-6, run before the bridge).
- **What advertising it does.** With `form` advertised the Adapter enables `AskUserQuestion`
  and sends `elicitation/create`, then waits, holding the Turn open until the Human answers. The
  Adapter never times out by itself, and an open request emits no events, so without a pause it
  runs into `Acp:TurnIdleTimeoutSeconds`. Huddle pauses the watchdog for the wait and bounds the
  wait itself, and writes the answer to the Transcript as a Message (§6.8a).
- **It is not one switch.** The same capability also turns on the Adapter's refusal-fallback
  dialog (*"model X declined; retry with Y?"*) and lets any MCP server put a form to the user.
  All three arrive as an `elicitation/create`, and one handler answers all of them, so none can
  hang the Turn. A request that handler cannot show faithfully is answered `decline` at once, with
  no card. Switching `Acp:AdvertiseElicitation` off removes all three together (D-13, Q-G1).
- **URL mode** (`elicitation.url`, the Adapter's OAuth flow for MCP servers passed in
  `session/new`) is not advertised either. Huddle's one MCP server authenticates with a bearer
  token.

### 6.8a The elicitation bridge

ACP elicitation is the protocol's standard way for an Agent to put a form to the Human, and the
earlier rejection of it (D-13, [ADR-0022](adr/0022-an-agent-asks-the-human-with-a-question.md)) rested
on one premise: an open request holds the Turn and emits no events, so it ran into
`Acp:TurnIdleTimeoutSeconds`. Pausing the watchdog removes that premise, so the bridge was built on
2026-10-01, **beside** `ask_human` and not instead of it: `ask_human` stays the primary way an Agent
asks (D-2), because it returns at once and the asker ends its Turn. The bridge exists for what the
Adapter can send that `ask_human` cannot express: typed fields (dates, numbers, booleans, free text) from
an MCP server, Claude's own `AskUserQuestion`, and the retry-after-refusal dialog. It met the three gates
D-13 set before it advertised anything:

1. **The idle watchdog pauses while a request is open (G1).**
2. **A handler answers every `elicitation/create` (G2)**, so no request can hang the Turn.
3. **The answer is written to the Transcript as a Message (G3).** A bridged answer is never only a
   tool result.

**The wire.** `dotacp` 2026.7.19 routes only an inbound method that starts with `_` to the client's
`ExtMethodAsync`, and its typed `unstable` elicitation request has no `sessionId`, `toolCallId` or
`requestedSchema` (and its response no `content`), so Huddle stays on the stable types. With
`DotAcpHostOptions.AdvertiseElicitationForm` on, the host (1) subclasses `ClientCapabilities` to add
`"elicitation":{"form":{}}`, never `url`, because the Adapter would then start an MCP OAuth flow this client
cannot complete, and (2) wraps the Adapter's output in `NdjsonMethodRewriteStream`, which renames an inbound
**request** whose `method` is exactly `elicitation/create` to `_elicitation/create`, line by line and only
after parsing it (never a byte replace: a `session/update` chunk can contain the string). The wire trace
therefore shows `_elicitation/create`. `DotAcpClientAdapter.ExtMethodAsync` then reads the raw arguments,
gives `ElicitationRequest(SessionId, ToolCallId, Message, RequestedSchemaJson)` to the session's
`IElicitationScope`, and replies `{action, content}` with plain CLR types, never a `JsonNode`. A request
nothing can answer (an unknown session, no scope bound, the Turn gone) is answered `cancel`. The Adapter
sends `$/cancel_request` when it cancels, which StreamJsonRpc does not hear (it listens for
`$/cancelRequest`), and dotacp cancels no handler when the peer closes or the connection is disposed, so
Huddle cancels each handler itself: on Stop, when the Turn ends, when the session is disposed, on a
disconnect, and when the bound below runs out.

**G1: the watchdog pause.** `RoomSession` is each session's scope. It takes a lease on the Turn in flight;
while any lease is open `ActiveTurn.IdleFor` is zero, so `WatchForAdapterSilenceAsync` sleeps another full
bound instead of firing, and releasing a lease restarts the clock from the answer. The lease counts open
requests per **Turn**, never per session, so one that is never released ends with its Turn and cannot pause
the next. It is idempotent, and Stop, the watchdog, a shutdown and a normal end all cancel it. The pause
removes the only timeout there was, so `Acp:UserInputTimeoutSeconds` (default 600, clamped to 30 to 86,400)
bounds a question: on expiry the request is answered `cancel`, which is not a Turn failure and never sets the
Turn's timed-out latch. The Turn's slot stays held while a card waits, and `Acp:MaxConcurrentTurns` defaults
to 1, so an unanswered form holds that Persona's other Rooms back for up to the bound. That is the cost D-2
refused for `ask_human` and accepts here, because a form is what the Adapter asked for.

**G3: the answer is a Message, and does not wake the asker.** `ElicitationService` posts the answer through
`ChatService.PostHumanAnswerAsync`: a Message from the Human, in the Transcript, quoting each field's label
and the answer (a blank line between them, as §6.4), that mentions nobody and is **withheld from the asker**
(`MessagePostedEvent.WithheldFromAgentId`; `AgentGateway` skips that Agent). The asker's Turn is still open
and takes the answer as the tool's own result; an ordinary Human Message would queue a second Turn, because
the Reply Gate answers every Human Message in a one-to-one Room whether or not anyone is mentioned. The post
takes the same per-Room lock as `PostAsync`, resets the Budget and drops waiting Questions, and a blank answer
posts nothing. The request is answered after the Message is posted, and **always**: if posting throws, the
request is still answered and the failure is logged. Accepted leaks, in `known-limits.md`: another Agent that
follows the Room still wakes, the asker's own token Budget is not reset, and on resume the asker's catch-up
may include the answer as context.

**Cards and mapping.** `ElicitationStore` holds **many** waiting forms per Room (each its own handler,
ordered by a counter, not the clock); `ElicitationCard` shows each of them between the Transcript and the
composer with one control per field kind: text, number, integer (typed, with the schema's bounds), date
(ISO text), boolean, single select (radios), multi select (checkboxes), and an "Other" box under an
`AskUserQuestion` question that, once typed in, clears and disables that question's options, exactly as the
Adapter lets a custom answer beat a choice. Send is enabled when every schema-`required` field is set and,
when none is required, at least one field is filled; **Skip** answers `decline` (the model is told the user
skipped) and posts nothing. Anything that ends the form without the Human answering it answers `cancel`: Stop,
the Turn ending, archive, delete, a typed Human Message, the bound running out, a disconnect. A schema the
reader cannot show faithfully (a nested object, an array of objects, `$ref`/`allOf`, a type list, no `type`,
more than 20 properties or 50 options) is answered `decline` at once with a logged warning and **no card**;
a number or boolean is never degraded to text, because the server would reject a string. Every string the
Adapter or an MCP server wrote (the message, labels, descriptions, options) is rendered as plain text, never
Markdown. `AskUserQuestion` is answered with the Adapter's own keys: `question_n` as the option's value (an
array for multi select) and `question_n_custom` for Other, only the keys that were filled; the retry dialog
answers `{"choice": "retry_fallback"}` or `{"choice": "cancelled"}`.

**Steering.** With both tools available the system prompt (`systemPrompt.askHuman`) tells a Claude Persona to
prefer `ask_human`: the built-in tool works but holds the Turn open until the Human answers, while ending the
Turn lets the answer arrive as the Human's next Message and keeps the conversation moving.

**What looking at it found.** The suite cannot see layout, and two defects only showed in a browser. The
card's rules lived in `ElicitationCard.razor.css`, and Blazor's CSS isolation stamps its attribute only on
HTML elements written in the component's own markup, so `::deep .elicitation-card` (a `MudPaper` root with
Mud components inside) compiled to a selector with no scoped ancestor and never matched: the card had no
height limit, and a form of eight fields was taller than the window and pushed Send and the composer off the
page. Its rules are now in `app.css` (capped at half the window, the fields scroll, the actions stay in view),
the scoped file is gone, and `MudBlazorImplementation.md` already said to do this. Second, **pre-existing**:
the Room's own column had no `min-height: 0`, so a Transcript taller than the window grew the column past it,
`.main-column` clipped the top, and `.message-list` never got a bounded height to scroll in, so the header and
every earlier Message were unreachable by the Human. It now shrinks and the message list scrolls
(`RoomLayoutSourceTests` pins both, as source text, since nothing in the suite renders a browser). The
multi-select options were also centred and stair-stepped instead of left-aligned.

**Not built, and not scheduled.** URL mode (`elicitation.url`, the Adapter's OAuth flow); a Huddle tool that
lets an Agent *start* a typed form of its own (today typed fields arrive only from an MCP server, a non-Claude
Adapter, or `AskUserQuestion`'s free-text Other); and a human approval before a tool runs (roadmap item 20),
which shares the watchdog pause above and nothing else.

---

## 7. Storage

| State | Where | Survives restart |
| --- | --- | --- |
| A waiting card | `QuestionStore`, memory | No |
| The answer | The Room's Transcript, as a Message | Yes |
| The framing | The Room's Transcript, as the asker's Message | Yes |
| The tool description | `PromptCatalog`, overridable in `prompts.json` | Yes |

No table, no file and no column. The Questions themselves reach the Transcript only inside the
answer, so a card that was dismissed or lost leaves no trace but the framing Message. That is
accepted.

---

## 8. Core rules

### 8.1 Replacement

| A card waits in the Room | Caller | Result |
| --- | --- | --- |
| None | Anyone | Stored |
| The caller's own | The same Agent | Replaced |
| Another Agent's | Anyone else | Refused, naming the asker |

The same as a Proposal (Skills spec D-6). A queue would put several cards in front of the Human
at once, which is what the three-Question limit exists to prevent.

### 8.2 A Proposal and a Question in the same Room

They are independent: both cards can wait at once, and both render. Answering the Question
posts a Human Message, which drops no Proposal, because a Proposal survives typed Messages by
design. No rule refuses one while the other waits. A Skill that uses both should ask first and
propose after the answer.

---

## 9. Edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| E-1 | Two tabs tap at once | `TryTake` lets exactly one through. The other gets `null`; its card disappears on `QuestionsChanged` |
| E-2 | Tap on a card that was replaced meanwhile | The `Id` does not match, so `TryTake` finds nothing. The new card renders |
| E-3 | The asker's Persona restarts while its card waits | No effect. The card is keyed by Room and the asker by Agent id; the answer still wakes it |
| E-4 | The asker is renamed while its card waits | The header and the Mention use the current Name |
| E-5 | The asker is deleted while its card waits | The card stays and can be answered; the answer carries no Mention. Dismiss is the likely choice |
| E-6 | The Room pauses after the card appears | The card still works. Answering is the Human speaking, which resets the Budget |
| E-7 | The asker calls `ask_human` and keeps writing, guessing the answer | Nothing in code prevents it; the success text forbids it. Manual test QM-2 |
| E-8 | The asker asks in a Room other than the one its Turn is in | Allowed. There is no framing Message there, so the card must stand on its own. The tool description asks for self-contained questions |
| E-9 | `PostAsync` fails after the take | Logged; the card is gone; the Human types |
| E-10 | An Adapter whose MCP client mishandles a nested array schema | Shared with `propose_teammates`. Manual test QM-4 on `agency-acp` |
| E-11 | An option reads `/compact`, or any text beginning with `/` | Nothing runs. The answer is posted as a quoted Question and its answer, ending with the asker's Mention, so it never *begins* with a Mention, and an Adapter command needs a **leading** Mention of that Teammate ([ADR-0035](adr/0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md); Commands spec C8). An ordinary prompt is also guarded against a leading `/` (Commands D-9) |

---

## 10. Testing

Every automated test uses fakes and costs nothing. What only a real model can show is in the
manual tests.

**Golden files change.** Adding a tool changes the roster in the system prompt, so every
`systemPrompt*.txt` in `tests/Huddle.Tests/Acp/Golden/` (six today: plain, `unprefixed`, `memory`,
`roomSessions`, `roomSessions.memory` and `skills`) and any `get_help` golden are regenerated in
the task that registers the tool. Each diff is reviewed to be exactly one new tool; none lists
`propose_teammates` today, because a Skill grants it, but all list `post_message`, and `ask_human`
goes to every Persona (D-10).

**Manual tests** live in [manual-tests/questions.md](engineering/manual-tests/questions.md) as QUESTIONS-01 to
QUESTIONS-06, in this table's order, and are registered in [manual-tests.md](engineering/manual-tests.md):

| Id | Steps | Pass |
| --- | --- | --- |
| QM-1 | Ask a Persona *"Help me plan a workout routine"* | It frames the ask in one or two sentences and calls `ask_human` with one to three Questions |
| QM-2 | Same | Its Turn ends without guessing an answer; the framing Message appears before you can tap |
| QM-3 | Ask *"What is the capital of France?"* and *"Should I learn Python or JavaScript?"* | It answers directly both times and does not call `ask_human` |
| QM-4 | QM-1 on a Persona on `agency-acp` | Same as QM-1, or the failure is recorded in the Adapters live findings |
| QM-5 | In a Room of three, answer a card | Only the asker wakes; the other Agent reads the answer as Catch-up next time it is Mentioned |
| QM-6 | Ask a Claude Persona *"Use your AskUserQuestion tool to ask me my favourite colour"* | It says it has no such tool, or asks with `ask_human`. No other card appears and the Turn does not hang (§6.8) |

---

## 11. Decisions

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-1 | **Only the Human is asked; no recipient argument** | Letting an Agent ask another Agent | Options save a person typing; an Agent reads prose as easily. Agent-to-Agent questions stay plain Mentions. Decided by the repo owner |
| D-2 | **The answer is a Message from the Human** | A tool result the asker waits for; a new Envelope | A blocking tool would hold a Turn open for as long as the Human takes. That runs into the idle timeout, and it holds the Persona's Turn slot: `MaxConcurrentTurns` is per Persona and defaults to 1 ([ADR-0024](adr/0024-an-agent-holds-one-session-per-room.md)), so the Teammate would stall in every other Room too. Sessions are per Room now, so it is the slot, not a shared session, that spans Rooms. An Envelope means a `ProtocolVersion` bump. A Message reuses the Reply Gate, the Budget and the Transcript unchanged. Work Modes D-16 takes the same stance for plan approval |
| D-3 | **No `@` in a question or an option** | Escaping `@` when composing; allowing it | The answer is posted as the Human. An escaped `@` is still the Agent putting words in the Human's mouth; refusing is simpler, and costs only an e-mail address in an option |
| D-4 | **Any typed Human Message drops the card** | Keeping it until answered, as a Proposal is kept | The typed Message is the answer the asker acts on. A card left behind invites a second, contradicting one. A Proposal is kept because revising it through prose is its normal path; a Question has no such path |
| D-5 | **Refused in a paused Room** | Allowed, since only a Human tap can answer | A tap resets the Budget. A Human deciding whether to let a runaway Room continue should see the Continue prompt, not a card that makes the decision for them as a side effect |
| D-6 | **One card per Room; the same asker replaces** | A queue; last writer wins | Same as the Proposal's D-6 |
| D-7 | **Separate store from `ProposalStore`** | One "pending card" store for both | Different lifetimes (D-4). A shared store would need a mode flag at every call site |
| D-8 | **Options disabled while the asker is still writing** | Enabled at once | Keeps the Transcript in the order it happened: the framing, then the answer |
| D-9 | **Up and down buttons to rank** | Drag and drop | Keyboard and screen-reader access, and no JavaScript |
| D-10 | **Every Agent gets the tool** | Granting it through a Skill | Asking costs nothing and creates nothing. `propose_teammates` is granted by a Skill because each Teammate is a billed process; `ask_human` is not |
| D-11 | **The full decision guidance lives in the tool description, and a short form of it in the system prompt** | A `get_help` section; a Skill; the description alone | A model decides to call a tool from its description; guidance in `get_help` or a Skill is read too late or not at all. *Amended 2026-10-01:* the description alone proved not enough, because the Claude Adapter defers MCP tools and the model sees only the name. The system prompt is the one text it always reads, so it carries a paragraph on when to ask (§6.6). The description stays as the full version |
| D-12 | **`rank_priorities` ships in V1** | V2 | It is the one kind a typed answer is worst at, and up and down buttons keep it small |
| D-13 | **Advertise ACP elicitation only once (a) the idle watchdog can pause for an open request, (b) a handler answers every `elicitation/create`, including the refusal-fallback and MCP form cases, and (c) the answer is written to the Transcript as a Message.** *Met 2026-10-01 (§6.8a, D-14 to D-19); `Acp:AdvertiseElicitation` is on by default and off restores the old behaviour exactly* | Bridging `AskUserQuestion` and `elicitation/create` to the `QuestionCard` before those held; rejecting elicitation for good | A bridge without the pause blocks the Turn and runs into the idle timeout; advertising `form` also enables the refusal-fallback dialog and MCP-initiated forms, each needing a handler, and gives a Claude Persona two ways to ask. The rejection was conditional, never permanent, and it ended when the three gates were met |
| D-14 | **Stay on the stable `dotacp` types: add the capability through a `ClientCapabilities` subclass and rename the inbound request** | Upgrading `dotacp` (2026.7.19 is the newest release); using its `unstable` typed elicitation | The `unstable` request has no `sessionId`, `toolCallId` or `requestedSchema` and its response no `content`, so it cannot carry the form or the answer. Stable dotacp routes only a `_`-prefixed method to `ExtMethodAsync`, so `NdjsonMethodRewriteStream` renames the one request it needs, parsing each line and never replacing bytes |
| D-15 | **The pause is a per-Turn lease on `RoomSession`, bounded by `Acp:UserInputTimeoutSeconds`** | A counter on the session; a registry of sessions by id; an unbounded wait | A counter on the session leaks into the next Turn and silences its watchdog; a registry goes stale when a session is replaced. The pause removes the only timeout there was, and `MaxConcurrentTurns` is 1, so without a bound one absent Human freezes a Persona in every Room |
| D-16 | **The answer is posted with `ChatService.PostHumanAnswerAsync`: a Message from the Human that mentions nobody and is withheld from the asker** | `PostAsync` with or without `@asker`; a tool result alone | The Reply Gate answers every Human Message in a one-to-one Room, mentioned or not, so any ordinary post queues a second Turn behind the open one. A tool result alone leaves the Transcript without what the Human said (G3) |
| D-17 | **Many waiting forms per Room, each its own handler; a schema that cannot be shown faithfully is declined with no card** | One card per Room, as for Questions; degrading a number or boolean to a text box | Each form is a blocking request, so a second one cannot replace the first. A server rejects a string where it asked for a number |
| D-18 | **The system prompt tells a Claude Persona to prefer `ask_human`** | Turning `AskUserQuestion` off again through the Adapter's `disallowedTools` while elicitation stays on for the other two cases | `AskUserQuestion` still works and is bridged, so the model may use either; the prompt says which to prefer and why. Switching it off is possible later and was not needed |
| D-19 | **`Acp:AdvertiseElicitation` defaults to `true`, with an off switch** | Off by default until a real model had been watched using it | The suite and a scripted adapter exercise every path, and the live check is in the manual tests (QUESTIONS-07 to QUESTIONS-10). The switch exists so a problem found later is one setting, not a revert |

---

## Appendix A — Test-first task plan

Each `.t` task ends red for the right reason; each `.i` task ends with `dotnet test Huddle.slnx
--` green. Test names follow `Method_Scenario_Expectation`.

| # | Kind | Task | Done when |
| --- | --- | --- | --- |
| Q-T1 | Unit | `QuestionStoreTests`: Stored; same asker Replaced; other asker Refused; `TryTake` once; `TryTake` with a stale id finds nothing; `Drop`; `QuestionsChanged` raised outside the lock, and not by a `Drop` that found nothing | Fails |
| Q-I1 | Impl | `Question.cs`, `QuestionStore`, `RoomEvents.QuestionsChanged` | T1 green |
| Q-T2 | Unit | `AskHumanToolTests`: each refusal in §6.2, in order; every problem in one result; `@` refused; `type` defaults to single; success and replacement texts | Fails |
| Q-I2 | Impl | `AskHumanTool` | T2 green |
| Q-T3 | Functional | `QuestionServiceTests.Answer_ThreeKinds_PostsQuotedAnswersAsHumanMentioningAsker` (real `ChatService`, `TempDataDir`) + `…_SecondAnswer_PostsNothing` + `…_AskerRenamed_MentionsNewName` + `…_AskerDeleted_OmitsMention` + `Dismiss_PostsNothing` | Fails |
| Q-I3 | Impl | `QuestionService` | T3 green |
| Q-T4 | Functional | `QuestionServiceTests.Answer_InGroupRoom_ReplyGateWakesOnlyAsker` (a fake gateway records `Mentioned`) | Fails |
| Q-I4 | Impl | Whatever T4 shows is missing | T4 green |
| Q-T5 | Functional | `ChatServiceTests.PostAsync_HumanMessage_DropsWaitingQuestions` + `…_AgentMessage_KeepsThem` + `SetRoomArchived_WithQuestions_Drops` + `DeleteRoom_WithQuestions_Drops` | Fails |
| Q-I5 | Impl | `ChatService` → `QuestionStore.Drop` | T5 green |
| Q-T6 | Unit | `PromptCatalogTests`: `tool.askHuman.description` exists, `NextSession`, contains no `mcp__team__`; `prompts.default.json` drift test | Fails |
| Q-I6 | Impl | The Prompt, and regenerate `prompts.default.json` | T6 green |
| Q-T7 | Functional | The factory offers `ask_human` to every Persona; goldens regenerated and reviewed | Fails |
| Q-I7 | Impl | `DotAcpAgentHostFactory` registration. *Added 2026-10-01 (D-11):* `systemPrompt.askHuman`, `SystemPromptComposer`'s `askHumanToolName`, and the name threaded through `DotAcpPersonaHost`; `Golden/systemPrompt.askHuman.txt` pins the paragraph, and `PersonaHostTests.Open_SystemPromptCarriesTheAskHumanParagraph_NamingTheToolOnce` pins it on the real factory's `session/new` | T7 green |
| Q-T8 | bUnit | `QuestionCardTests`: one single Question sends on tap; Send disabled until complete; multi toggles; rank up/down reorders and sends `1. … · 2. …`; disabled while the asker has a Draft; Dismiss posts nothing; a second tab hides on `QuestionsChanged`; hidden when Archived | Fails |
| Q-I8 | Impl | `QuestionCard.razor`, `Chat.razor` wiring | T8 green |
| Q-G1 | Guard | A test that pins the `initialize` request's client capabilities: `Fs` and `Terminal` false, and `elicitation` present only as `{"form":{}}` when `Acp:AdvertiseElicitation` is on, absent when it is off. **Done as** `PersonaHostTests.Start_WithAdvertiseElicitationOn_AdvertisesFormObjectOnly`, `…Start_CapabilityKeys_AreFsTerminalElicitation_Only`, `…Start_WithAdvertiseElicitationOff_AdvertisesNoElicitation` and `…Start_WithTheShippedDefault_AdvertisesFormObjectOnly` in `tests/Huddle.Tests/Conformance`, which drive the real factory against `FakeAcpAgent`. Each was proved by mutation (a `url` entry added, another key added, always or never advertising) | Passes now, and goes red if `url` or any capability beyond `fs`, `terminal` and `elicitation.form` is ever advertised, or `fs`/`terminal` is turned on. The probe host and the console never advertise (`DotAcpHostOptions.AdvertiseElicitationForm` defaults to `false`) |
| Q-D | Docs | `language.md` Question drops "Proposed, not built"; `known-limits.md` gains "a waiting Question is lost on restart"; `manual-tests.md` gains QM-1 to QM-6; ADR-0022 to Accepted | Reviewed |

## Appendix A2 — The elicitation bridge, as built

Built 2026-10-01 in this order, each part test-first and green on its own, with the flip that turns it on last.

| Part | What it is | Pinned by |
| --- | --- | --- |
| Wire | `NdjsonMethodRewriteStream`, `ElicitationClientCapabilities`, `DotAcpHostOptions.AdvertiseElicitationForm` (off by default) | `NdjsonMethodRewriteStreamTests`, `DotAcpAgentHostElicitationWireTests` |
| Adapter | `DotAcpClientAdapter` answers `elicitation/create` through the session's `IElicitationScope`; handlers are cancelled on peer close, session dispose and prompt cancellation | `DotAcpClientAdapterElicitationTests`, `DotAcpAgentHostElicitationHandlingTests`, `FakeAcpAgentElicitationTests` (the fake sends the real wire method and refuses unless `elicitation.form` was advertised) |
| G1 | `RoomSession` lease, the watchdog pause, `Acp:UserInputTimeoutSeconds` | `RoomSessionUserInputTests`, `ElicitationBridgePlumbingTests` |
| G3 | `ChatService.PostHumanAnswerAsync`, `MessagePostedEvent.WithheldFromAgentId`, the `AgentGateway` skip | `ChatServiceHumanAnswerTests`, `AgentGatewayWithheldAnswerTests` |
| Core | `ElicitationSchemaReader`, `ElicitationStore`, `ElicitationComposer`, `ElicitationService`, `RoomElicitationBridge`, and the drops on a typed message, archive and delete | `tests/Huddle.Tests/Elicitation/*`, `ChatServiceElicitationDropTests` |
| Card | `ElicitationCard` | `ElicitationCardTests`, `ChatPageTests` |
| Flip | `Acp:AdvertiseElicitation` (default on), the steering sentence, Q-G1, the whole path against a scripted agent | `ElicitationConformanceTests`, `PersonaHostTests`, `PromptCatalogTests` |
| Layout | the card's rules in `app.css`, the Room column's `min-height: 0` | `RoomLayoutSourceTests` |
| Scripted adapter | `mock-acp` puts a form to the Human when a prompt contains `[elicit:ask]`, `[elicit:one]`, `[elicit:form]`, `[elicit:refusal]` or `[elicit:unsupported]`, and replies with what it got | `MockElicitationTests` |

## Appendix B — Follow-ups outside this spec

- **The `team-building` Skill's step 1 interview** is the obvious first user: *"One-off or
  recurring"*, *"How involved"* and *"Practice or real work"* are each a `single_select`. The
  Greeting's menu of 31 teams is **not**: it is far past four options, and it stays prose. Edit
  the Skill once this tool exists, not before, so the Skill never names a tool its Teammate
  cannot call.
- **ACP elicitation** is built (§6.8a). Still outside this spec, and not scheduled: URL mode (the
  Adapter's OAuth flow for MCP servers); a Huddle tool that lets an Agent *start* a typed form of its own,
  since typed fields today arrive only from an MCP server, a non-Claude Adapter or `AskUserQuestion`'s
  free-text Other; and a human approval card on `session/request_permission` (roadmap item 20), which now
  needs only its own card and the Transcript Message, because the watchdog pause is built.
