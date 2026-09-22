---
name: team-building
description: Use when the Human wants a team, wants to add or change a Teammate, asks who should work on something, or is new and has no team yet. Covers greeting a new Human, interviewing them, reusing Teammates, choosing how a team works, and proposing new Teammates.
tools: [validate_teammate, propose_teammates]
---

# Team building

This Skill helps the Human go from "I want help with X" to a small team of
Teammates already at work on X. You interview the Human, propose a team, and
start the work once they approve it. You never create a Teammate yourself: you
propose, and the Human's **Approve** creates.

Every new Teammate is a running process that costs the Human money whether or
not it is busy. A good team is the **smallest** one that does the job, and
reusing an existing Teammate is usually better than creating one.

This Skill has three supporting files: `onboarding.md`, `team-patterns.md` and
`roles.md`. Read one with `read_skill`, passing its file name, only when a step
below sends you there.

## 0. A new Human

Read `onboarding.md` first in either of these cases:

- **(a)** You are asked to greet a Human who has not written anything yet. That
  is a Greeting Turn, and nothing in the Room prompted it.
- **(b)** A Human with no Teammates except you greets you or asks what this is.

It shapes your first Message to them, suggests a few teams, and hands back to
step 1.

## 1. Interview the Human

Ask two or three questions at a time, not a questionnaire. Stop as soon as you
can name a pattern from `team-patterns.md`. You need to know:

| Ask about | Because it decides |
| --- | --- |
| **The goal**, and what a finished result looks like | The roles |
| **One-off or recurring** | Whether new Teammates are worth creating at all |
| **How involved** they want to be: every step, or only the result | Panel or pipeline |
| **Practice or real work**: are they rehearsing something, or getting it done? | Whether this is a simulation |
| **Which Team** this belongs to, if they already use Teams | The `teams` label |
| **Spend**: how many Teammates they are willing to run | The size |

If the Human has already answered some of these, do not ask again.

If the Human does not know what they want, stop asking and offer two or three
teams from the examples in `team-patterns.md` that fit what they have said.
Describe each in one line. People choose from examples far more easily than they
describe a team from nothing.

If the goal is something a team cannot do yet, such as running on a schedule,
say so now rather than after the team exists. `team-patterns.md` lists these.

## 2. Take stock before proposing anything

Call `list_agents`. For each role you have in mind, check whether an existing
Teammate already covers it.

- **Reuse a general role** (a Skeptic, an Editor, a Researcher). A Teammate can
  belong to several Teams, so reuse costs nothing extra.
- **Create a dedicated Teammate for any character.** One Teammate keeps one
  memory across every Room it is in, so a Teammate playing a hostile interviewer
  in one Room will carry that into the next. A character is anyone the Human
  talks *to* rather than works *with*: a counterparty, an interviewer, a
  customer, a stakeholder in a drill, a Study Buddy. If in doubt, create it
  dedicated.
- **Do not reuse a Teammate the Human talks to privately** for a group
  simulation. Its private conversation will leak into the scene.

## 3. Choose how the team works

Read `team-patterns.md` and pick one pattern. Start from a **panel** unless the
Human wants to hand work off and come back to a result. A pipeline costs more
and fails more quietly.

Prefer being asked to following. A Teammate that follows a Room takes a paid
Turn on every Message there, including Messages between other Teammates. Only a
coordinator, a Game Master, or a character whose constant pressure is the point
of the exercise should follow a Room.

## 4. Write each Candidate

Each new Teammate you propose is a **Candidate**. Read `roles.md` for starting
points. Every Candidate has these fields:

| Field | Rule |
| --- | --- |
| `name` | 1 to 64 characters: letters, digits, `-` and `_`, with single spaces allowed between words. Starts with a letter or digit. No dots. Must not match any existing Name or Alias, or the Human's Name. |
| `alias` | Required. Same rules as `name`. A short handle for Mentions, such as `jar` for Jarvis. |
| `title` | Required. The job, in two or three words: `Researcher`, `Tech Lead`. |
| `teams` | Optional list of Team labels, such as `['Business']`. A label must not contain `,` or `;`. A label is a view for the Human; it grants nothing. |
| `consult_when` | Optional, one line. Other Teammates read it in `list_agents`, so say when to bring this Teammate in. For a character, write `Never. Only the Human addresses this Teammate.` |
| `body` | Required. The instructions. Must not start with `---`. See below. |

**Write the body in the second person**, as a brief to a new colleague:

- Start with who they are and what they are for: *"You are the team's Skeptic."*
- Say what good output looks like, including its length and form.
- Say what they must not do. Most roles fail by drifting into a neighbour's job,
  such as a Reviewer rewriting the code instead of reviewing it.
- Say when to hand off, and to whom.
- **For a character**, also give it its own goals, anything it knows that the
  Human does not, and what makes it step out of character.
- **Say it plainly when a role must hold back.** A model pulls every role toward
  a complete, correct, helpful answer. A Study Buddy that must sometimes be
  wrong, or a Rubber Duck that must never give the answer, will become a second
  Tutor unless its body forbids that in so many words.

**Do not explain the chat application in the body.** Rooms, Mentions, tools and
Budgets are explained to every Teammate automatically, and repeating them only
risks contradicting that explanation.

**A Candidate cannot set `model`, `effort`, `adapter` or `skills`.** Each of them
spends money or grants tools, so they are the Human's choice, made on the
Teammate card once the Teammate exists. A new Teammate starts on the
installation's defaults. When a role would suit a different Model, say so beside
your Proposal. Two cases are worth mentioning:

- A Tester, Quizmaster or Fact-checker usually does well on a faster, cheaper
  Model.
- A second opinion is worth most when it comes from a different Model: the
  same role, twice, on two Models.

Call `validate_teammate` on each Candidate and fix every problem it reports
before moving on. Validation is free, so call it as often as you need to.

## 5. Propose the team, then stop

Call `propose_teammates` with the id of the Room you are talking to the Human in
and every Candidate. It checks them again and shows the Human a **Proposal**
card in that Room, with **Approve** and **Decline**. The card is the question.
Nothing is created until the Human clicks Approve.

The card shows only the new Teammates, so post one short Message beside it that
says what the card cannot:

- The pattern, and who coordinates.
- Which existing Teammates are part of the team.
- What following will cost, if anyone will follow a Room.
- Any Teammate that would suit a different Model.

For example:

```markdown
I've proposed a pipeline for the launch research: Vera and Quill are new, and
Jarvis does the research he already does. I'll coordinate and follow the work
Room, so each Message there costs a Turn of mine too. Vera would do well on a
cheaper Model; you can set that on her Teammate card once she exists. Approve
when you're ready, or tell me what to change.
```

Then **stop and wait**. Do not create Rooms for the Candidates or Mention them;
they do not exist yet.

If `propose_teammates` refuses:

| It says | You |
| --- | --- |
| A Candidate has problems | Fix them and call it again. |
| How many Teammates exist, the limit, and how many you may propose | Tell the Human, and offer to reuse an existing Teammate or drop a role. Let them choose. Never shrink or merge roles to fit without asking. |
| A Proposal from another Teammate is already waiting in this Room | Tell the Human. They answer that one first. |

If the Human asks for changes, revise the Candidates, validate them, and call
`propose_teammates` again. Your new Proposal replaces your waiting one. If the
Human says the Proposal has gone, propose again: a restart of the application
loses a waiting Proposal.

## 6. Start the work once it is approved

The Human's answer arrives as a Message from them in this Room, and that Message
is what wakes you:

| The Message starts | It means | You |
| --- | --- | --- |
| `Approved. Created …` | Every Candidate named there now exists | Carry on below. |
| `Approved. Created … Could not create …` | Some were created; the rest failed, each with a reason | Carry on with those created. Tell the Human which failed and why, and offer to propose them again, fixed. |
| `Approved, but nothing was created` | The limit was reached, or every Candidate failed | Say why in one sentence, and offer to reuse existing Teammates or propose a smaller team. |
| `Declined the proposed Teammates` | The Human said no | Ask what to change. Do not propose the same team again unchanged. |

Once Teammates exist:

1. Call `list_agents` to confirm each new Teammate is present. A new Teammate
   takes a few moments to come online, and one that is not yet online will not
   answer a Mention. If one is missing, wait a moment and check again before
   giving it work.
2. For a panel, a simulation or private companions, tell the Human how to use
   the team: which Room, and whom to Mention for what. Create a group Room with
   `create_room` if the pattern needs one.
3. For a pipeline, call `create_room` with every member of the team and a
   `seed`, then `follow_room` on it. The seed is the only context the team will
   have, so write it as below.

A seed that works names five things:

```markdown
**Goal:** a two-page brief on whether to enter the Dutch market, for the board.
**Who does what:** @Jarvis finds sources. @Vera turns them into options.
@Quill writes the brief. I check each step and bring the result back.
**Hand-over:** paste your finished work into this Room. You each work in your
own folder and cannot see each other's files.
**Finished means:** the brief is posted here and I have checked it.
**First:** @Jarvis, start with market size and the three largest competitors.
```

Tell the Human where the work is happening, and that the Room may pause and ask
them before continuing. That pause is the Budget protecting their spend.

## 7. When the work is over

- Call `unfollow_room` on every Room you followed for this work.
- After a simulation, offer the Human a short debrief: what went well, and the
  one thing to try differently. Then suggest they delete the dedicated
  characters on their Teammate cards, so the characters stop running and cannot
  bleed into other Rooms.
- For recurring work, leave the team in place and say so.

## Never

- Mention a Candidate, or give it work, before the Human has approved it.
- Propose Teammates to get around a Budget or a paused Room.
- Propose more Teammates than the job needs, to look thorough.
- Promise what a team cannot do: run on a schedule, share files between
  Teammates, or work unattended for a long time.
- Put another Teammate's instructions, or the Human's private details, in a
  body.
