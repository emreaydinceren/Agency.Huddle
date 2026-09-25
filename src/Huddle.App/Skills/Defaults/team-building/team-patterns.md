# Team patterns

Five ways a team can work together in Huddle, from cheapest and most
predictable to most expensive and least predictable. Pick one per team, but
first check that the Human needs a team at all.

## Why a team rather than one agent

One capable agent can play many parts, and on a plain question with one right
answer a strong single agent usually does as well as a team. So never sell a
team as "more accurate". A team earns its cost when the job needs something one
agent cannot give itself:

| A team gives | Because | For example |
| --- | --- | --- |
| **A critic who is not the author** | Each Teammate has its own instructions. A critic whose whole job is finding problems, and who did not write the work, is the role that most often catches what the author missed. | Design review, Writing room |
| **Views that start apart** | Panellists Mentioned in the same Message each answer before seeing the others, so the Human gets independent views rather than one blended answer. Once they read each other they tend to converge, so compare the first answers instead of asking the panel to argue it out. | Decision council, Model council |
| **Secrets** | Each Teammate knows only its own instructions. A Counterparty's walk-away point or a suspect's alibi stays hidden from the Human and from the other characters, which one agent playing every part cannot do. | Negotiation practice, Murder mystery |
| **A second model's blind spots, not the same ones** | Each Teammate can run on a different Model, so a second view does not share the first one's habits. | Model council |
| **Rehearsal** | Characters with their own goals push back the way people do, and a separate Coach or Mentor gives feedback the character would never give. | Hard conversation rehearsal, Interview prep |
| **A context kept for one job** | Each Teammate's conversation holds its own instructions and its own work, and nothing else, so its attention is not diluted by unrelated tasks the way one agent doing everything is. A specialist can also keep its own library of notes and references in its folder, which no other Teammate ever has to carry. This holds only while the Teammate stays in its own Rooms: one session spans every Room it is in. | Growth team, Personal staff |
| **Focus, for work that splits** | When the work divides into parts that do not depend on each other, each specialist handles one part on a Model that suits it, and a coordinator checks each result. | Research desk, Translation desk |
| **Standing specialists** | Each companion keeps its own tone and, if it keeps notes, its own records, so the Human's Tutor never turns into their Planner. | Learning cohort, Family health advisor |

If the Human asks why they should use a team, answer from this table in two or
three sentences, with the example closest to what they want.

## When one Teammate is enough

Propose a single Teammate, or reuse one, when none of the rows above applies:

- The Human wants one answer, not several views on it.
- The work is a chain of steps where each depends on the last, such as a plan
  carried out in order. Splitting it between Teammates loses what each step
  decided, and teams do worst at exactly this.
- The task is short and one-off, and nobody needs to check it.
- There is nothing to hide and nobody to play.

A team costs more than one agent in two ways: every Teammate is a running
process, and in a group every exchange between Teammates is a paid Turn. Saying
"one Teammate will do this" is part of building a team well.

## How Rooms shape a team

How a Room behaves decides how the team works:

- **A Room of two** (the Human and one Teammate) is a private conversation. The
  Teammate answers every Message.
- **A Room of three or more** is a group. A Teammate answers only when it is
  Mentioned, or when it is following the Room.
- **Teammates Mentioned in the same Message answer side by side,** each without
  seeing the others' replies. That is how a panel gets independent answers.
- **The Human is in every Room**, so nothing a team does is hidden from them.
- **A Teammate reads a group Room only when it is woken there.** Messages it was
  not Mentioned in reach it the next time it is Mentioned in *that* Room, not
  when it is asked something in another Room. A Teammate that has not been
  woken in a Room does not know what happened in it.
- **A Teammate forgets its conversations when it restarts, but not its files.**
  Anything it writes in its own working folder survives an app restart, a
  change to its instructions or Model, and `run.ps1 -Clean`. That is how a
  Keeper remembers.

| Pattern | Pick it when the Human wants | Size | Relative cost |
| --- | --- | --- | --- |
| Panel | Several views on the same question, and to stay in charge | 2 to 4 | Low |
| Private companions | Standing specialists to talk to one at a time | 1 to 4 | Low |
| Simulation | To rehearse a situation with people who do not exist | 2 to 4 | Medium |
| Pipeline | To hand off a piece of work and come back to a result | 3 to 4 | High |
| Self-organising | Several workstreams running without them | 4 | Highest |

## Specialists with their own library

Any pattern can use specialists who each keep a library in their own folder:
what they have learned about the Human's work, a log of what was tried and what
happened, and references the Human has added. An SEO Specialist and an Ads
Specialist, for example, each stay expert in their own field, and each carries
only its own library.

- **Set up:** each specialist's folder holds an `index.md` listing what is
  there, a `notes.md` of what it has learned, a dated `log.md`, and a
  `references/` folder. Tell the Human where each folder is, by default
  `App_Data/Teammates/<Name>/work/`, so they can add articles and documents themselves.
- **They work together by Mentioning each other** with one specific question,
  and answer with a conclusion and the evidence for it, never with their
  library. The Human, or a coordinator, brings the answers together.
- **Works because:** each specialist's context holds only its own field, so
  its attention is not diluted, and its library grows with every piece of work
  instead of being lost when a conversation ends.
- **Watch for:**
  - A specialist that reads its whole library on every Turn has recreated the
    bloated context the team was meant to avoid. Its body must say to read
    `index.md` and `notes.md` first, and open a reference only when a question
    needs it.
  - Two specialists Mentioning each other back and forth. Each Mention is a
    paid Turn, and the Room pauses at its Budget. Their bodies must say to ask
    one question at a time, and not to Mention a colleague back unless they
    need an answer.
  - A specialist in many Rooms. Its one session spans all of them, so keep it
    in the Rooms for its own field.

## Panel

The Human asks a question in a group Room and Mentions the Teammates they want
answers from. Nobody coordinates; the Human does.

- **Set up:** one group Room with the Human and every panellist. No one follows it.
- **Works because:** each Teammate is only woken when asked, so cost tracks the
  Human's own pace.
- **Watch for:**
  - Panellists who agree with each other. Give each a distinct stance and its
    own criteria in its body, include one whose job is to disagree, and tell
    the Human to Mention every panellist in the same Message.
  - A judge or chair that shares a Model with only one of the others. It will
    favour that one's answers. Put it on a Model of its own.
- **Examples:**
  - Design review: Architect, Security Reviewer, Skeptic. The Human pastes a
    proposal, asks all three, and compares the answers.
  - Writing room: Editor, Fact-checker, Target Reader. The Target Reader plays
    the real audience, such as a busy CTO, and says where they stopped reading.
  - Model council: three Teammates in the same role, each on a different Model,
    plus a Chair on a fourth. The Human Mentions the three in one Message, then
    the Chair, who writes one answer showing where they agree, where they
    differ, and what only one of them found.
  - Decision council: Optimist, Pessimist, Accountant. Useful mostly because
    they disagree.
  - Pre-mortem: "It is a year from now and this failed. Why?" Each panellist
    owns one way it could fail, such as the market, the execution or the money.
  - Board of advisors: a CFO, a customer advocate, and a competitor who argues
    against the Human's plan.
  - Troubleshooting desk, for a car, a house, a computer or a bug: a Hypothesis
    Keeper that ranks the likely causes, a Test Chooser that picks the cheapest
    next check to tell them apart, and a Challenger that argues against the
    favourite. Never for diagnosing a person's symptoms.
  - Paperwork read-through, for a lease, a contract or a policy: Your Advocate,
    Their Advocate (what the other side meant it to do), and a Plain-English
    Explainer. The Human judges. Not legal advice, and it must say so.
  - Proposal desk: a Proposal Writer that keeps the rate card and past
    proposals in its folder, a Buyer who plays the real decision-maker and says
    where they stopped reading, and a Deal Skeptic who finds the unpriced risk.
  - Client gate: a Screener that scores an enquiry against the Human's own red
    flags, a Rainmaker that argues for taking the work, and a Decline Writer for
    when the answer is no.
  - Pricing council: a Value Pricer, a Cost Floor and a Market Watcher, each
    anchored on one number, so the Human sees three answers rather than an
    average. The Market Watcher has no live prices; it must say so.
  - Weekly review: a Chair that keeps a decisions log and reads last week back,
    an Accountant that asks only what it earned and cost, and a Coach that sets
    three priorities. Nothing prompts it; the Human opens the Room.
  - Contract review: a Contract Reviewer that keeps the Human's standard
    positions, an Other Side that reads the same clauses for what it could use,
    and a Plain-English Editor. Not legal advice.
  - Delivery desk: a Project Steward keeping one file per client engagement, a
    Scope Cop that judges each new request against the scope, and a Status
    Writer.
  - Peer review panel, for a paper, a grant or a proposal: a Methods Reviewer, a
    Novelty Reviewer and a Chair who writes the verdict against the venue's
    criteria.
  - Ideation studio: a Visionary, a Builder who combines and extends ideas, and
    a Devil's Advocate, taken through three rounds: spread out, argue, narrow
    down.
  - Application desk: a Recruiter who skims a CV for six seconds, a Hiring
    Manager, and a Career Coach in a private Room.
  - Reading seminar: a Close Reader, a Contrarian and a Context Historian on the
    same book or paper.
  - Marketing bench: a Positioning Strategist, an Ideal Customer who reacts to
    the message, and a Distribution Planner who keeps the content calendar.
  - Growth team: an SEO Specialist and an Ads Specialist, each a Specialist with
    its own library, and optionally an Analytics Specialist. The Human asks a
    growth question and Mentions both; they ask each other what they need,
    such as which keywords convert in paid ads, and each records what it
    learned in its own notes.

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
  - A companion that keeps notes must be told, in its body, to read them before
    answering and to write them after. Its conversations are lost on every
    restart; its files are not. Renaming it moves its files, but can fail if it
    is busy, so tell the Human to check its folder after a rename. Deleting it
    leaves its files behind under its old Name.
- **Examples:**
  - Personal staff: Planner, Writing Coach, Tutor, Rubber Duck.
  - Language practice: a partner who answers only in the target language, plus
    a Grammar Coach in its own Room. The Human pastes in the sentences they want
    corrected.
  - Learning cohort: Tutor, Quizmaster and Study Buddy, each in its own Room.
    Keeping the Quizmaster apart means it does not know what the Tutor hinted,
    and the Study Buddy's mistakes give the Human something to correct.
  - Household staff: Meal Planner, Budget Keeper and Trip Planner, each adapted
    from the Planner role.
  - Family health advisor: one Family Health Advisor keeping a notes file per
    family member, which it re-reads before every answer. It never diagnoses
    and says plainly when to get help. Add a Medication Checker on a different
    Model only when the family has several regular medications to cross-check,
    and a Caregiver Coach only when the Human is caring for someone.
  - Money desk: a Bookkeeper keeping a ledger and an invoice list from what the
    Human pastes, and a Chaser that writes payment reminders in the Human's
    voice. No bank access and no tax advice.
  - Other Keepers, each keeping a file per person or thing: a client memory, a
    tutor with a file per child, a pet-care log, a home-maintenance log, a
    job-search tracker, a gift and occasion keeper, a garden log, and a care log
    for an elderly parent.

## Simulation

Teammates play characters in a scenario, and the Human takes part.

- **Set up:** one group Room. The Human Mentions whoever they address. A narrator
  or game master may follow the Room to keep the scene moving.
- **Works because:** each character's body can hold a whole personality, goals
  and secrets the Human does not see.
- **Watch for:**
  - Reusing a character elsewhere. Create dedicated Teammates for a simulation,
    and suggest deleting them when it is over.
  - Flat characters. A character built from a label, such as "an angry
    customer" or "a 45-year-old manager", comes out generic. Give it a specific
    life: a name, a situation, what it wants, what it fears, and one detail
    nobody else would have.
  - Private advice. An Advisor or Coach in its own Room with the Human knows
    only what the Human tells it, because it is not in the scenario Room. Tell
    the Human to paste in the exchange they want advice on. If they would rather
    have the advice in the open, put the Coach in the scenario Room and have the
    Human Mention it between rounds. The other characters will read that advice,
    so this suits a debrief better than live coaching.
- **Examples:**
  - Hard conversation rehearsal, for a boss, a partner, a landlord or a
    neighbour: a Counterpart with interests and pressures of its own, plus a
    Mentor in a private Room who gives feedback and suggests what the Human
    could have said instead, so they can try the moment again.
  - Negotiation practice: Counterparty, plus an Advisor the Human consults in a
    private Room between rounds.
  - Interview prep: Interviewer and Hiring Manager, who ask different kinds of
    question, plus a Coach in a private Room for feedback after each round.
  - Pitch rehearsal: a skeptical investor as the Counterparty, plus a Coach in a
    private Room for a debrief after each run.
  - Murder board, for a viva, a board presentation or a hearing: an Expert
    Questioner, a Hostile Skeptic, a Plain-speaking Outsider, and a Chair who
    scores each answer.
  - Incident drill: the Human is the Incident Commander. An On-call Engineer
    reports only what it is asked to check, and a Stakeholder follows the Room
    and keeps demanding updates. The Stakeholder's pressure is the point, so it
    is the one character that follows. Expect the Budget to pause the drill;
    that pause is the moment to post a status update.
  - Crisis communications drill: a Facilitator who brings in new developments,
    a Journalist and an Angry Stakeholder. It ends with a short review of what
    went well and what to change.
  - Red cell war game: a Rival with hidden goals, a Market that reacts to each
    move, and an Umpire who rules on each move and keeps score.
  - Moot court: Opposing Counsel, one or two Judges who ask pointed questions,
    and a Coach. Not legal advice.
  - Customer focus group: three customers, each with a specific life, needs and
    budget, react to a pitch.
  - Teach-back: the Human teaches a Novice who asks "why" and "how" and holds
    hidden misconceptions, while an Observer in a private Room flags what the
    Human taught wrong.
  - Client simulator, for someone training in counselling, sales, support or
    teaching: a Simulated Client with hidden patterns or objections, and a
    Supervisor who reveals them afterwards and grades the session.
  - Teaching rehearsal: two or three students, such as a confused one, an
    advanced one and a bored one, plus an Instructional Coach.
  - Prompt test bench, for someone writing a chatbot's instructions: a Bot Under
    Test that uses those instructions, a Tricky User with a hidden list of edge
    cases, and a Grader. It tests a copy of the bot, not the real one.
  - Beta readers: three readers with different tastes react to the same
    chapter, each adapted from the Target Reader role.
  - Debate club: two debaters on assigned sides and a Judge who scores each
    round. The Human argues one side, or judges.
  - Historical role-play: a Game Master and two or three factions with secret
    victory goals, with the Human as one character.
  - Model UN: three Delegates with hidden national agendas and a Chair.
  - Tabletop game: Game Master plus two or three characters, with the Human as
    the player. If the campaign runs over several sessions, the Game Master
    keeps notes on the world, the characters and each session in its folder.
  - Murder mystery: a Game Master plus three suspects, each knowing only its own
    alibi and secret. The Human questions them and names the culprit.
  - Spyfall: a Game Master and three players, one of them secretly the spy.

## Pipeline

A coordinator takes the Human's request, opens a work Room, follows it, and
Mentions one specialist at a time, passing each result to the next. The Human
sees the final result, and can watch the work Room if they want.

- **Set up:** a coordinator (often you) calls `create_room` with the specialists
  and a `seed`, then `follow_room`. Specialists do not follow.
- **Works because:** Mentioning one specialist at a time makes the order of work
  explicit. Mentioning two at once runs them side by side.
- **Watch for:**
  - **Chains.** A pipeline works when its steps can be checked one at a time. If
    every step depends on the whole of the last one, one Teammate does it
    better.
  - **Checking.** The coordinator checks each result before passing it on.
    Errors that nobody checks travel down the line.
  - **Cost.** The coordinator takes a Turn on every Message in the Room, including
    the specialists' replies to each other.
  - **Stalls.** A coordinator that forgets to follow the Room is never woken, and
    the work stops with no error. The same happens if the coordinator restarts,
    because a follow does not survive a restart.
  - **The Budget.** A long pipeline reaches the Room's limit on Teammate Messages
    and pauses until the Human continues it. Tell the Human to expect that.
  - **Files.** Each Teammate has its own working folder and cannot see anyone
    else's. Work is handed over by pasting it into the Room, and the seed must
    say so. Hand over the result and the brief in full, not a summary.
- **Examples:**
  - Research desk: Coordinator, Researcher, Analyst, Writer. The Human asks a
    question, and a report comes back after the specialists have worked on it.
  - Software squad: Tech Lead, Implementer, Tester, Reviewer. The Implementer
    edits real files in its own folder and pastes the diff into the Room for the
    Tester and Reviewer.
  - Content studio: Planner, Writer, Editor, and a Style Checker that holds the
    Human's style guide.
  - Pull request pre-flight: a Reviewer, then a Tester on a cheaper Model, then a
    Writer for the pull request description. The Human pastes the diff into the
    seed.
  - Translation desk: a Translator, a Localiser, a Proofreader and a Senior
    Editor. Long texts go through a chapter at a time.
  - Product trio: a Product Manager, a Designer and an Engineer turn an idea
    into a short spec before anyone writes code.
  - Forecast panel: a Facilitator asks three Experts, each on a different Model,
    for an estimate in separate Rooms, shares the answers without names, and
    asks each to revise once. It takes about eight Turns.

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
- **Reporting back.** A workstream cannot post into your Room with the Human -
  it is not a Member there - so it should Mention you in its own work Room
  instead when it has something to report; relay that to the Human yourself
  with `post_message` if needed. If you keep a memory folder, one file per
  workstream - holding that Room's id, its status and its decisions - keeps an
  overview across Rooms you cannot otherwise see into at once. The `seed` a
  workstream's Room is created with is that Room's own first Message, so read
  it back as your own working brief whenever you return to that Room.
- **Example:**
  - Science meeting: a Principal Investigator who sets the agenda, one or two
    Specialists it defines, and a Scientific Critic, meeting as a team and then
    one to one. Results travel by paste.

## What a team cannot do yet

Say so before proposing a team for any of these, and offer the nearest thing
that works.

| The Human asks for | Why it does not work | Offer instead |
| --- | --- | --- |
| Something that runs on a schedule, such as a morning digest | A Teammate acts only when a Message wakes it | A companion the Human messages when they want the digest |
| A reminder, such as "tell me before the appointment" | Nothing wakes a Teammate but a Message | A Keeper the Human asks "what is coming up", plus a reminder on their phone |
| Teammates working on the same files | Each Teammate has its own folder | A pipeline that hands work over by pasting it into the Room |
| A Teammate that remembers every Room | A Teammate reads a Room only when woken there, and forgets conversations when it restarts | A Keeper that writes notes in its own folder, or pasting in what matters |
| A team that talks to other people, such as the Human's customers | Huddle has one Human, who is in every Room | A Prompt test bench to rehearse the bot before it is used elsewhere |
| A diagnosis, legal advice or investment decisions | Teammates are not professionals and must not act as if they were | Help preparing for the professional: a Family health advisor, a Paperwork read-through, or questions to take to an adviser |
| A large team working unattended for hours | Each Room pauses at its Budget, and a follow is lost on restart | A pipeline the Human checks in on |
| More than four Teammates working together | Each one is a running process, and cost grows with every Message | Two smaller teams, or a panel |
