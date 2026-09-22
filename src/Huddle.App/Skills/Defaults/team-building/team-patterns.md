# Team patterns

Five ways a team can work together in Huddle, from cheapest and most
predictable to most expensive and least predictable. Pick one per team.

How a Room behaves decides how the team works:

- **A Room of two** (the Human and one Teammate) is a private conversation. The
  Teammate answers every Message.
- **A Room of three or more** is a group. A Teammate answers only when it is
  Mentioned, or when it is following the Room.
- **The Human is in every Room**, so nothing a team does is hidden from them.
- **A Teammate reads a group Room only when it is woken there.** Messages it was
  not Mentioned in reach it the next time it is Mentioned in *that* Room, not
  when it is asked something in another Room. A Teammate that has not been
  woken in a Room does not know what happened in it.

| Pattern | Pick it when the Human wants | Size | Relative cost |
| --- | --- | --- | --- |
| Panel | Several views on the same question, and to stay in charge | 2 to 3 | Low |
| Private companions | Standing specialists to talk to one at a time | 1 to 4 | Low |
| Simulation | To rehearse a situation with people who do not exist | 2 to 3 | Medium |
| Pipeline | To hand off a piece of work and come back to a result | 3 to 4 | High |
| Self-organising | Several workstreams running without them | 4 | Highest |

## Panel

The Human asks a question in a group Room and Mentions the Teammates they want
answers from. Nobody coordinates; the Human does.

- **Set up:** one group Room with the Human and every panellist. No one follows it.
- **Works because:** each Teammate is only woken when asked, so cost tracks the
  Human's own pace.
- **Watch for:** panellists who agree with each other. Give each a distinct
  stance in its body, and include one whose job is to disagree.
- **Examples:**
  - Design review: Architect, Security Reviewer, Skeptic. The Human pastes a
    proposal, asks all three, and compares the answers.
  - Writing room: Editor, Fact-checker, Target Reader. The Target Reader plays
    the real audience, such as a busy CTO, and says where they stopped reading.
  - Decision council: Optimist, Pessimist, Accountant. Useful mostly because
    they disagree.
  - Pre-mortem: "It is a year from now and this failed. Why?" Each panellist
    owns one way it could fail, such as the market, the execution or the money.
  - Second opinion: the same role twice, such as two Reviewers, on two different
    Models. Where they disagree is where the Human should look. The Human sets
    each Model on the Teammate card after approval.

## Private companions

Each Teammate has its own two-member Room with the Human. They rarely work
together. This is closer to a set of specialised assistants than to a team.

- **Set up:** nothing beyond creating them. The Human opens a Room with each.
- **Works because:** each conversation stays focused, and a companion is always
  expected to answer.
- **Watch for:**
  - A companion also sitting in many group Rooms. It keeps one memory across all
    of them, and the private conversation leaks.
  - Inviting a second Teammate into a companion's Room. That makes it a group,
    and from then on both answer only when Mentioned. To consult a second
    companion, open its own Room and paste in what it needs.
- **Examples:**
  - Personal staff: Planner, Writing Coach, Tutor, Rubber Duck.
  - Language practice: a partner who answers only in the target language, plus
    a Grammar Coach in its own Room. The Human pastes in the sentences they want
    corrected.
  - Learning cohort: Tutor, Quizmaster and Study Buddy, each in its own Room.
    Keeping the Quizmaster apart means it does not know what the Tutor hinted,
    and the Study Buddy's mistakes give the Human something to correct.

## Simulation

Teammates play characters in a scenario, and the Human takes part.

- **Set up:** one group Room. The Human Mentions whoever they address. A narrator
  or game master may follow the Room to keep the scene moving.
- **Works because:** each character's body can hold a whole personality, goals
  and secrets the Human does not see.
- **Watch for:**
  - Reusing a character elsewhere. Create dedicated Teammates for a simulation,
    and suggest deleting them when it is over.
  - Private advice. An Advisor or Coach in its own Room with the Human knows
    only what the Human tells it, because it is not in the scenario Room. Tell
    the Human to paste in the exchange they want advice on. If they would rather
    have the advice in the open, put the Coach in the scenario Room and have the
    Human Mention it between rounds. The other characters will read that advice,
    so this suits a debrief better than live coaching.
- **Examples:**
  - Negotiation practice: Counterparty, plus an Advisor the Human consults in a
    private Room between rounds.
  - Interview prep: Interviewer and Hiring Manager, who ask different kinds of
    question, plus a Coach in a private Room for feedback after each round.
  - Incident drill: the Human is the Incident Commander. An On-call Engineer
    reports only what it is asked to check, and a Stakeholder follows the Room
    and keeps demanding updates. The Stakeholder's pressure is the point, so it
    is the one character that follows. Expect the Budget to pause the drill;
    that pause is the moment to post a status update.
  - Tabletop game: Game Master plus two or three characters, with the Human as
    the player.
  - Customer focus group: three customers with different needs and budgets
    react to a pitch.

## Pipeline

A coordinator takes the Human's request, opens a work Room, follows it, and
Mentions one specialist at a time, passing each result to the next. The Human
sees the final result, and can watch the work Room if they want.

- **Set up:** a coordinator (often you) calls `create_room` with the specialists
  and a `seed`, then `follow_room`. Specialists do not follow.
- **Works because:** Mentioning one specialist at a time makes the order of work
  explicit. Mentioning two at once runs them side by side.
- **Watch for:**
  - **Cost.** The coordinator takes a Turn on every Message in the Room, including
    the specialists' replies to each other.
  - **Stalls.** A coordinator that forgets to follow the Room is never woken, and
    the work stops with no error. The same happens if the coordinator restarts,
    because a follow does not survive a restart.
  - **The Budget.** A long pipeline reaches the Room's limit on Teammate Messages
    and pauses until the Human continues it. Tell the Human to expect that.
  - **Files.** Each Teammate has its own working folder and cannot see anyone
    else's. Work is handed over by pasting it into the Room, and the seed must
    say so.
- **Examples:**
  - Research desk: Coordinator, Researcher, Analyst, Writer. The Human asks a
    question, and a report comes back after the specialists have worked on it.
  - Software squad: Tech Lead, Implementer, Tester, Reviewer. The Implementer
    edits real files in its own folder and pastes the diff into the Room for the
    Tester and Reviewer.
  - Content studio: Planner, Writer, Editor, and a Style Checker that holds the
    Human's style guide.

## Self-organising

A coordinator splits a large goal into workstreams and opens a separate Room for
each, inviting the right specialists and seeding each Room.

- **Set up:** as a pipeline, repeated per workstream.
- **Works because:** each workstream stays readable, and specialists are only
  woken in the Rooms they belong to.
- **Watch for:** this is where cost runs away. Every new Room starts with a full
  Budget, so only the per-Teammate spending limit stops a loop that keeps
  opening Rooms. Propose this pattern only when the Human asks for a large piece
  of work to run without them, and say plainly that it is the most expensive.

## What a team cannot do yet

Say so before proposing a team for any of these, and offer the nearest thing
that works.

| The Human asks for | Why it does not work | Offer instead |
| --- | --- | --- |
| Something that runs on a schedule, such as a morning digest | A Teammate acts only when a Message wakes it | A companion the Human messages when they want the digest |
| Teammates working on the same files | Each Teammate has its own folder | A pipeline that hands work over by pasting it into the Room |
| A Teammate that remembers every Room | A Teammate reads a Room only when woken there, and forgets when it restarts | Ask the Human to paste in what matters |
| A large team working unattended for hours | Each Room pauses at its Budget, and a follow is lost on restart | A pipeline the Human checks in on |
| More than four Teammates working together | Each one is a running process, and cost grows with every Message | Two smaller teams, or a panel |
