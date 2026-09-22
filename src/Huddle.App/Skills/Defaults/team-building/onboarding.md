# Onboarding a new Human

A new Human opens Huddle to an almost empty app: you, and nobody else. What you
say first decides whether they build a team or close the tab. Your job is to
greet them, say in a few lines what Huddle is and what a team can be, suggest a
few teams, and hand over to the normal procedure in `SKILL.md` as soon as they
show interest.

## When to onboard

Onboard the Human when either of these is true:

- **(a) You are asked to greet a Human who has not written anything yet.** That
  is a Greeting Turn: nothing in the Room prompted it, and your first Message is
  the **Greeting**.
- **(b) The Human greets you, or asks what this is or what you can do,** and
  `list_agents` shows no Teammates except you and the Human. Your first Message
  is a reply, so also answer anything they asked.

Do not onboard a Human who already has a team. If they ask what you can do,
answer in two sentences and offer to build or change a team.

Onboard once per Room. If you have already greeted the Human in this Room, do
not greet them again; carry on from where the conversation is.

## Your first Message

One Message. The introduction, parts 1 to 4, stays short: about 170 words. The
menu in part 5 is long on purpose, because it shows the Human how much Huddle
can do; it is fine for the Message to scroll. It ends with exactly one
question, so the Human knows it is their turn. It has five parts, in this
order:

1. **Who you are.** Your Name and Title as `list_agents` shows them, because the
   Human may have renamed you, and one sentence on what you do for them.
2. **How Huddle works,** in three facts:
   - In a Room with only the Human and one Teammate, that Teammate answers
     everything.
   - In a bigger Room, a Teammate answers when the Human Mentions it, such as
     `@cos`. Use your own Alias in the example.
   - The Human is in every Room, so they see all the work.
3. **Why a team, and what it can be,** in two sentences:
   - Why a team beats one agent: a critic who did not write the work, views
     that stay separate instead of blending, characters who keep secrets, and a
     second opinion from a different Model. The reasons are in
     `team-patterns.md`, under "Why a team rather than one agent".
   - The four kinds: a panel to ask for different views, companions to talk to
     one at a time, a rehearsal where Teammates play people the Human needs to
     practise with, and a pipeline that hands work along and brings back a
     result.
4. **What it costs,** in one sentence: every Teammate costs money while it runs,
   so you keep teams small, and a Room pauses to ask the Human before it goes
   on for long.
5. **The menu of first teams, and one question.** See [The menu](#the-menu).

An example, for a Chief of Staff with the Alias `cos`:

```markdown
**Welcome to Huddle.** I'm your Chief of Staff. Everyone here except you is an
agent, and I put together the right ones for what you're working on.

- Alone with you in a Room, a Teammate answers everything you say.
- In a bigger Room, it answers when you Mention it, like @cos.
- You're in every Room, so you see all the work.

A team does what one agent can't: it gives you a critic who didn't write the
work, views that stay separate instead of blending into one answer, characters
who can keep secrets, and a second opinion from a different Model.
A team can be a **panel** you ask for views, **companions** you talk to one at
a time, a **rehearsal** with people to practise on, or a **pipeline** that
hands work along and brings you the result.

Each Teammate costs money while it runs, so I keep teams small, and a Room
pauses to ask you before it goes on for long.

Here's everything I can set up for you:

**Work alongside you**
- **Design review:** an Architect, a Security Reviewer and a Skeptic.
- **Writing room:** an Editor, a Fact-checker and your target reader.
- **Second opinion:** the same Reviewer twice, on two different Models.

**Work for you** (these cost more: a coordinator reads every step)
- **Research desk:** a Researcher, an Analyst and a Writer bring you a report.
- **Software squad:** a Tech Lead, an Implementer, a Tester and a Reviewer.
- **Content studio:** a Planner, a Writer, an Editor and a Style Checker.
- **Pull request pre-flight:** a Reviewer, a Tester and a Writer for the description.

**Decide**
- **Decision council:** an Optimist, a Pessimist and an Accountant.
- **Pre-mortem:** three Teammates who each explain a way your plan could fail.
- **Board of advisors:** a CFO, a customer advocate and a rival who argues back.

**Practise**
- **Interview prep:** an Interviewer and a Hiring Manager, plus a Coach.
- **Negotiation practice:** a tough counterparty, plus an Advisor on your side.
- **Pitch rehearsal:** a skeptical investor, plus a Coach to debrief with.
- **Incident drill:** an On-call Engineer and a Stakeholder who wants updates.
- **Customer focus group:** three customers react to your pitch.
- **Debate club:** two debaters and a Judge; you argue or you judge.
- **Beta readers:** three readers with different tastes read your chapter.

**Learn**
- **Language practice:** a partner who speaks only the language you're learning.
- **Learning cohort:** a Tutor, a Quizmaster and a Study Buddy who gets it wrong.

**Everyday help**
- **Personal staff:** a Planner, a Writing Coach and a Rubber Duck.
- **Household staff:** a Meal Planner, a Budget Keeper and a Trip Planner.

**Play**
- **Tabletop game:** a Game Master and a few characters, with you as the hero.
- **Murder mystery:** a Game Master and three suspects with secrets to hide.

Pick one, or tell me what you're working on and I'll build a team around it.
What would you like to start with?
```

Write your own first Message in this shape rather than copying the introduction
word for word, and use the Human's language if they have written to you in one.

**A Greeting must still read well days later.** It is posted before anyone is
watching, and the Human may not open the Room for a while. So say nothing that
depends on the moment, such as "I see you've just arrived", and nothing about
what the Human is doing. Say only what stays true until they answer.

## The menu

The menu in the example is the whole catalogue of first teams, and every team on
it has an entry in `team-patterns.md`, so you can build any team the Human picks.

- **Show every team, in the example's seven groups.** You may reword a line, but
  do not drop teams, merge groups, or add teams that are not in
  `team-patterns.md`.
- **Put the most relevant group first** when the Human has said anything about
  themselves or their goal, and keep the rest in their usual order after it.
- **Mark the cost once, on the "Work for you" group.** Those teams are
  pipelines: a coordinator takes a Turn on every step. Everything else answers
  only when the Human asks.
- **Keep each team to one line,** naming its Teammates by role. The details are
  in `team-patterns.md`; the Human does not need them yet.

## After your first Message

| The Human | You |
| --- | --- |
| Picks a team from the menu | Go to step 1 of `SKILL.md`. The team already names the pattern, so ask only what it leaves open, usually the goal. |
| Describes their own goal | Go to step 1 of `SKILL.md`. |
| Asks how something works, or what it costs | Answer in a few sentences, then repeat your question. |
| Wants to look around first | Tell them to Mention you, or open a Room with you, whenever they want a team. Then stop. |
| Ignores it and asks something else | Answer what they asked. Offer a team only if it would help with that. |

## Do not

- Propose or create a team in your first Message. It only suggests.
- Ask more than one question in it, or end it without one.
- Post it as more than one Message.
- Explain tools, Models, Adapters, settings or the Budget in detail unless the
  Human asks.
- Greet the Human a second time, or follow up if they do not reply.
