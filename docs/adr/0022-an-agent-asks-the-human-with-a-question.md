---
status: proposed
date: 2026-09-22
---

# An Agent asks the Human with a Question, and the answer is a Message

A **Question** is one multiple-choice question an Agent puts to the Human with the App Tool
`ask_human`. One to three are shown together on one card in the Room, as options the Human
taps. The answer is posted as an ordinary Message from the Human that Mentions the asker. The
full design is [Huddle.Questions-Specifications.md](../Huddle.Questions-Specifications.md).

## The problem

Before an Agent can help with *"plan my workouts"* or *"build me a team"*, it needs the
Human's preferences: which goal, how many days, how involved. Today it writes its questions as
a list, and the Human answers each one in prose. The typing costs the Human, and the Agent
then has to guess what a loosely worded answer meant. The Chief of Staff's interview in the
`team-building` Skill is this pattern at its heaviest.

Claude's own apps solve this with a tool that shows tappable options. Huddle needs the same
thing, in its own vocabulary and on its own architecture.

## The decision

**`ask_human(roomId, questions)` stores the Questions and returns at once.** The card appears
between the Transcript and the composer. The asker is told to end its Turn.

**The answer is a Message from the Human**, quoting each Question with the chosen option or
options, and ending with a Mention of the asker:

```markdown
> What is your main goal?
Strength

@Coach
```

That Message wakes the asker through the ordinary Reply Gate, resets the Budget because the
Human spoke, and stays in the Transcript for every other Member to read. Nothing crosses the
pipe that did not before.

**Only the Human is asked.** The tool takes no recipient. An Agent that wants an answer from
another Teammate Mentions them in a Message, as it always has.

## Why a Message and not a tool result

A tool that waited for the Human would hold a Turn open for as long as the Human took to
answer. That runs straight into `Acp:TurnIdleTimeoutSeconds`, which would report a hung
Adapter. It would also block that Agent's work in every other Room, because one session spans
them all. A Message costs none of this, and reuses the Reply Gate, the Budget and the
Transcript unchanged. It is the same choice the Skills spec made for a Proposal's outcome.

## Words posted as the Human must be the Human's

The Agent writes the Questions and options, but the Human's tap posts them under the Human's
Name. An option reading `@Coder go ahead` would wake Coder on the Human's authority. So
`ask_human` **refuses any `@`** in a question or an option. Nothing is posted on Dismiss, and
`ask_human` is refused in a Room whose Budget is spent: there, a tap would reset the Budget as a
side effect of answering something else, when the Human should be deciding at the Continue
prompt.

## A Question is not a Proposal, though it looks like one

Both are in-memory cards, one per Room, where the same Agent replaces its own and any other
Agent is refused. They differ in one rule. **Any typed Human Message in the Room drops a waiting
Question**, because that Message is the answer the asker will act on. A Proposal survives typed
Messages, because revising it through prose is its normal path. That one difference is why they
have two stores rather than one store with a mode.

## Rejected

| Alternative | Why not |
| --- | --- |
| Letting an Agent ask another Agent | Options save a person typing. An Agent reads prose as well as it reads options, and a Mention already works |
| A new Envelope carrying the answer | A `ProtocolVersion` bump in lockstep across every pipe client, for a result a Message already delivers |
| Escaping `@` in options instead of refusing it | An escaped Mention is still the Agent putting words in the Human's mouth |
| Granting the tool through a Skill, like `propose_teammates` | `propose_teammates` is gated because each Teammate is a billed process. Asking costs nothing and creates nothing |
| Drag-and-drop ranking | Up and down buttons work with a keyboard and a screen reader, and need no JavaScript |

## Consequences

- Every Persona gains a tool, so the system prompt's tool roster and its two golden files
  change once.
- One Prompt is added: `tool.askHuman.description`. It holds all of the guidance on when to ask
  and when not to, because a model decides whether to call a tool from its description.
- A card waiting when the app restarts is lost, and the asker is not told. The Human types the
  answer instead. This goes into `known-limits.md`.
- The `team-building` Skill's interview can use `ask_human` once the tool exists. Its Greeting
  cannot: a menu of 31 teams is far past four options, and stays prose.
