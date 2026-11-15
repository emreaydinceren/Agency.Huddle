# Agents as colleagues, not a pipeline

*An open-source multi-agent workspace where the human is in every room, and what
building it taught us about why agent teams fail.*

Using an AI agent still feels like manual work. You write the prompt, read the
response, catch the errors, and carry the output to the next step. The agent is
fast; you are still the planner, the router and the reviewer.

The obvious fix is more agents: split the work across specialists, give them
channels to talk on, rules for who acts when, tools for ground truth, and memory
that outlasts a prompt. Most multi-agent frameworks are built on some version of
those five pillars, and so is Agency.Huddle.

On its own, though, the fix makes things worse. Five agents on one model give you
one opinion five times, at five times the cost. Where we part company with most
frameworks is in what we think turns a crowd of agents into a team, and in where
the human sits.

Agency.Huddle is a shared workspace for people and agents. Some tools run a company
with a fixed org chart and approval chains. Some run agent teams in terminal panes,
where the agents talk out of your sight. Some are toolkits you build your own system
from. This is a different bet: a chat app where you are in the room, installed with
one command on Windows or macOS.

## What makes a team worth having

A team is not a gain just because it has more members. Agents on the same model,
working from the same context, share blind spots and agree with each other as
easily as one agent agrees with itself. The research on why multi-agent systems
fail points the same way: poor role design, agents out of step with each other,
and weak verification (Cemri et al., [arXiv 2503.13657](https://arxiv.org/abs/2503.13657)).
More agents alone adds cost, not quality.

The evidence for diversity is concrete: 2 diverse agents can match or exceed 16
homogeneous ones ([arXiv 2602.03794](https://arxiv.org/abs/2602.03794)). And more
agents is not free. In Google Research's tests, centralized coordination improved a
parallelizable financial-reasoning task by 80.9% over a single agent, while on
strictly sequential planning every multi-agent variant they tried did 39–70% worse
([*Towards a science of scaling agent systems*](https://research.google/blog/towards-a-science-of-scaling-agent-systems-when-and-why-agent-systems-work/)).

The gain comes from three things together:

1. **Different roles.** Each member has its own job, instructions and tools.
2. **Separate context, and ideally a different model.** Each member holds only its
   own part, so its attention is not diluted and it does not inherit the author's
   assumptions. A reviewer on a different model goes further: it does not share the
   author's blind spots either.
3. **A real review or test step.** Something checks the result against ground
   truth: a test, a source, or a person.

Put those in a loop and you have **Generate → Challenge → Verify**. One agent
produces the work, a second with its own context hunts for flaws, and a third step
checks the result against something real. Without the last step it is just two
agents agreeing. Here is how Agency.Huddle supplies each step, and where it does
not yet:

| Step | In Agency.Huddle | Status |
| --- | --- | --- |
| Generate | An Agent with its own role, context and model | Built |
| Challenge | `@`-mention a second Agent, ideally on another model | Built; you have to ask for it |
| Verify | You, a member of every room; Tasks with a named owner and a Review status | Partly: no built-in test step |

The rest of this page explains each row.

### Making members disagree on purpose

A different title on the same model is a costume, not a perspective. Four things
make members reach different conclusions, listed roughly by how hard each is to
fake with a prompt:

1. **Different models.** Members on one model share its priors however their roles
   read. An Agent's Model and Adapter are set per Agent, so one team can span
   vendors and the strongest lever is a setting.
2. **Different information.** A member that reads other files, or a different slice
   of the conversation, argues from different evidence. Each Agent has its own
   Work Dir and memory, so what it knows is its own.
3. **Opposed incentives.** "Find what breaks" produces a different answer than
   "Review this". Give roles that pull against each other, such as builder and
   breaker or cost and quality, rather than roles that merely differ. Write the
   stance into the Agent's own instructions so it holds whatever you ask.
4. **Independent first answers.** Mention several members in one Message and each
   answers before seeing the others, so the discussion starts from real
   differences. After that they read each other, as colleagues do. That is the
   point, and it is why the first three levers matter: they decide whether a
   disagreement survives the conversation.

A **Skill** supplies a method, not a viewpoint, such as a threat-modeling
procedure for the one Agent who reviews security. Because Skills are assigned per
Agent, a method does not spread to the whole team and flatten it. The first lever
depends on you setting each Agent's Model; see
[What we have not solved](#what-we-have-not-solved).

## The diagram with a hole in it

```text
[User Request]
      │
      ▼
[Planner] ──┬──► [Research A] ──┐
            └──► [Research B] ──┴──► [Analysis] ──► [Critic] ──► [Writer]
                                                                     │
                                                                     ▼
                                                              [Final Output]
```

Look at where the human is. At the top, as a request. At the bottom, as a
recipient. Everything in between — the planning, the research, the analysis, the
critique — happens somewhere the human is not.

The standard fix is a *human-in-the-loop checkpoint*: a gate inserted between
stages where the pipeline pauses and asks permission. A checkpoint is something
you add. It can be skipped in configuration, placed at the wrong stage, or
forgotten when a new stage is inserted. And the two failures multi-agent systems
are known for — agent chatter that inflates cost without improving the answer, and
the cascading error, where one upstream hallucination is trusted by everything
downstream — happen exactly in the part of the diagram nobody is watching.

## Put the human in the room

Agency.Huddle is a chat app. It looks like Slack because Slack already solved this
problem for people: work happens in rooms, and you are in the rooms that concern
you.

The other members of your rooms are AI agents. Each Agent runs as its own agent
process, driven over the [Agent Client Protocol](https://agentclientprotocol.com)
(ACP). They read the room, answer when spoken to, start rooms of their own, and
pull each other in when a question is not theirs.

```text
#  valentines-strategy                                4 members

   You              What should we run for Valentine's Day?
   Chief of Staff   @CMO research what competitors ran last year.
                    @CFO critique whatever comes back — margin first.
   CMO              Three of four ran bundles, not discounts. Draft attached.
   CFO              Bundles hold margin; the discount variant does not. Go
                    with the CMO's second option.
```

And the human is a member of every room. Not by convention, not by checkpoint —
by schema:

```csharp
var memberIds = new List<string>(agents.Count + 1) { KnownIds.Human };
```

That is the line in `ChatService` that creates a room. When an agent creates a
room to work something out with two other agents, `CreateRoomTool` adds the
caller, the named agents — and you. There is no way to create a room the human
is not in, because the code has no branch for it.

Oversight stops being a feature and becomes a consequence of the data model.
Nobody has to remember to insert the checkpoint. Runaway chatter is still possible
— two agents can still tag each other — but it happens in a room you can open, and
a Budget pauses it and asks whether you want to continue (see
[What we have not solved](#what-we-have-not-solved)).

Being in the room is also what makes you able to be the **Verify** step. A person
who is there at the moment a claim is made can ask for the source, which a gate at
the bottom of a pipeline cannot do for a mistake made three stages earlier.
Agency.Huddle puts you in the room; it does not make you check.

## Five pillars, five answers

**Specialized agents.** An **Agent** is a Markdown file,
`Agents/<Name>/<Name>.md`. Its frontmatter says who it is and what it knows; its
body becomes its private instructions. Drop a new file in and `@CFO` comes online
without a restart.

```yaml
name: 'CFO'
title: 'Finance'
alias: 'cfo'
teams: ['Business']
skills: ['team-building']
consult_when: 'A recommendation rests on figures nobody has sourced'
```

The fields other Agents may see compose into a job description, which is what
lets one agent bring in the right colleague instead of guessing at a name. The body
never reaches anyone else, so an Agent can hold goals the others do not see.

Each Agent also names its **Adapter**, the ACP agent process behind it, and its
model. One Agent can run on Anthropic's `claude-agent-acp` in the cloud while
the one reviewing its work runs a local model through `agency-acp`, which is the
first lever in [Making members disagree on purpose](#making-members-disagree-on-purpose).
The [User Guide](Huddle.UserGuide.md) covers the rest of an Agent's settings, the
Skills it can read, and the Teams and Projects it works in.

You do not have to write the files yourself. The built-in Chief of Staff proposes a
team, and nothing is created until you select **Approve**. By default it proposes
no more than eight Agents, because every Agent is a process.

**Communication channels.** Inside the app, a named pipe carries one JSON object
per line. An agent exists in Agency.Huddle for exactly one reason: some process
connected to the pipe and said `hello`. That is the entire contract. A
forty-line PowerShell script that echoes text back is a full member of any room,
and so is an Agent whose runner speaks ACP to a real agent on the other side —
the chat surface cannot tell them apart, and that is the point. Any language, any
process, one line of JSON.

**Coordination rules.** The reply rule is one pure function:

```csharp
return memberCount <= 2 || mentioned || following ? ReplyDecision.Reply : ReplyDecision.CatchUp;
```

Two members is a private conversation, so the agent answers everything. Three or
more is a group, so it answers only when `@`-mentioned, or when it has chosen to
follow the room. A room's behavior comes from how many members it has. There is
no "direct room" type in the schema, because a stored type would have to be kept in
sync on every invite and would drift. Above that rule sits one override: a room
that has spent its **Budget** of agent replies answers nothing until you speak or
grant more. The server labels each delivery; it never decides who replies.

**Tools.** Agents get a set of tools offered over MCP, whose bodies run inside the
app: to find colleagues, create rooms, invite and post, follow a room, read a Skill,
watch a folder, and file and update **Tasks**. An agent never reaches into storage —
it asks, and the app decides. `get_help` describes the rest, so detail an agent may
never need is paid for when it asks, not on every turn of every session.

**Memory.** Shared: one append-only JSON Lines **Transcript** per room that
everyone reads, plus a **Team Memory** folder every member of a Team sees. Individual:
each Agent has its own memory folder and its own **Room Session** per room, so the
context of one conversation does not leak into another. Work that must outlast a
chat lives in **Tasks**, a Markdown file with an owner, a status and a timeline,
and in the **Library**, where you and your Agents keep notes side by side. Notes
link to each other with `[[wiki links]]`, show their backlinks, and keep their links
when you rename or move them. An Agent is told which watched files changed since
its last turn. An Agent does not keep its conversation across a restart, so
anything worth keeping goes into a memory file. There is no vector store; the memory
is files you can open.

## Work that wakes people up

**Tasks** are Markdown files you can see as a List or as a Board, with one swimlane
per Project and cards you drag between columns. When a Task assigned to an Agent
changes, whether you drag a card, edit the file by hand or another Agent updates
it, Huddle posts a message in a Room that wakes the assignee. Work moves without
anyone polling a chat. Every change is appended to the Task's timeline, and the
statuses include **Review**, which is where a Challenge step belongs. Agents
changing each other's Tasks could loop, so each Task allows ten Agent-made
wake-ups until you select **Allow 10 more**.

## Stages are verbs, not people

The standard pipeline names its stages as if they were staff: Planner,
Researcher, Analyst, Critic, Writer. It is a natural reading, and we think it is
wrong.

Those are things a colleague *does*, not who a colleague *is*. A CFO plans,
researches, analyzes, critiques and writes, depending on what you asked. Hire a
"Critic" and you have fragmented one person into five, none of whom knows the
numbers well enough to critique them.

So in Agency.Huddle the verb goes in the request:

```text
@CMO research what competitors ran last Valentine's Day
@CFO critique the margin assumptions above
```

Two Agents, two verbs, no mechanism. Mention-gating routes it. The CFO
critiques the numbers *because* it is the CFO.

The verb is in the request; the stance is lasting. An Agent's Markdown body is its
private instructions, so a CFO told to doubt every margin assumption keeps doing so
whichever verb you use.

## Topology without an engine

Reports on multi-agent systems name three layouts: **centralized** (one coordinator
routes everything, easy to follow but a bottleneck), **peer-to-peer** (agents talk
directly, flexible but hard to track and prone to drift) and **hierarchical** (leads
supervise sub-teams, scales but costs more hand-offs). Most frameworks make you
choose one before you start.

Agency.Huddle is a mesh by default: a room of peers, every member sees every
message. It becomes a tree the moment one agent creates a room, seeds it with the
context the specialists need, and follows it. The Chief of Staff you asked for a
Valentine's strategy starts a room with the CMO and the CFO, writes the brief as the
first message, and brings the outcome back to you. You are in that room too; you
are just not interrupted by it.

Research gives a reason for care here. Google Research found that independent
agents working in parallel without talking amplified errors 17.2x, against 4.4x for a
centralized system with an orchestrator. A Huddle room is not the independent case:
members read each other, you are in the room, and a Budget stops runaway chatter. It
is still why we treat checking at each hand-off as the open problem.

Nothing switches modes. There is no room kind. Topology is whatever falls out of
which Agents exist, which tools each holds, and who is in the room. The design
record — including why we rejected a blocking `delegate()` tool, and why relying
on specialists to tag the coordinator back turned out to be a silent failure
path — is in
[ADR-0005](adr/0005-agent-topologies-are-emergent.md).

## This repository is built the way it describes

The argument above is that nobody can read everything a team of agents produces,
so the work moves from reading output to engineering what checks it. Testing made
the same move once, from a role into a system of automated checks and telemetry.
This repository applies it to review, and each piece below can be opened and read:

- **Rules are code, not review comments.** Warnings are errors, nullable
  references are on, and the .NET and Sonar analyzers run inside the build, so a
  style or language rule fails a build instead of waiting for someone to notice.
  [CSharpPrinciples.md](../agents/CSharpPrinciples.md) lists them for the agents
  that write the code.
- **A test has to be able to fail.** `Prove-Mutation.ps1` changes one line of
  source, runs the tests and restores the file byte for byte, so a green test that
  exercises nothing shows itself. [Testing.md](../agents/Testing.md) says when to
  use it.
- **Gates are scripts.** `Check-All.ps1` runs the line-ending, diff, visibility and
  documentation-index checks. CI repeats restore, build and test on every pull
  request, with a secret scan and a vulnerable-dependency check beside them.
- **Decisions are written down.** The [decision records](Index.md#4-decisions-adrs)
  say why the system is shaped as it is, so an agent or a person reads the reason
  instead of guessing at it.
- **The documentation is routed.** [Index.md](Index.md) sends a reader to the one
  page that owns an answer, with a rough cost in tokens, and `Check-DocIndex.ps1`
  fails when a page is missing from it. An agent spends its context on the answer
  rather than on working out the layout.

None of this removes a person. A person sets what done means, and the checks say
whether it was met. It also checks the repository, not your Agents: a team in
the app still has no built-in test step, as the table below says.

## What we have not solved

A team fixes none of its own failure modes by default. Five are worth saying plainly:

- **Convergence.** A team the Chief of Staff proposes starts every Agent on the
  installation's default Model and Adapter, because a Candidate cannot carry either.
  Until you change them on each Agent card, the team is one model in several
  roles, and a discussion between them can settle too easily
  ([why this matters](#making-members-disagree-on-purpose)).
- **Collaboration is not automatic value.** Agents talking to each other is not
  a review. Mention a challenger to attack a specific piece of work.
- **Cascading errors.** If one Agent makes a wrong assumption early and the next
  trusts it, every later step builds on it and the work looks more finished, not
  less. The fix is to verify at each hand-off, not only at the end.
- **Cost.** Every hand-off is more model calls. A team costs more than one agent
  doing the same job. An Agent's card shows what it spent since it started, and a
  long-running Agent can be summarized with `@name /compact` to shrink the
  conversation it carries. The compaction itself is a model call that the card does
  not count.
- **Mental load moves, it does not vanish.** Setting up roles and reviewing a team's
  output is work. The payoff comes on repeated or long-running jobs. Start with one
  Agent, and add a team when coordination, not capability, is what slows you down.

And here is where Agency.Huddle stands on the guardrails an autonomous network needs:

| Guardrail | Status |
| --- | --- |
| Human-in-the-loop | **Structural.** Every room, by schema. |
| A verify step | **Partly.** The human is always in the room and Tasks name an owner, but there is no built-in test step. A team that has only a Generate and a Challenge Agent has no Verify. |
| Runaway-loop guard | **Yes.** A per-room Budget of agent replies (40 by default) that any human message resets; when it is spent the agents pause and you choose **Continue** or **Leave paused**. A per-Agent token Budget, ten Agent-made wake-ups per Task, and an idle timeout on a silent Turn. **Stop** cancels a Turn. |
| Seeing what an agent did | **Mostly.** A reply shows its recent tool calls, an edit opens to a preview, an Agent card shows its reported spend, Task changes are logged, and file changes are reported to the next Turn. None of the tool-call detail is kept after the reply. |
| Least-privilege tool access | **Partly.** A Work Mode limits what the agent does before it asks, but Huddle approves the agent's permission requests itself, so nothing asks you before a tool runs. The one write refused in every mode is into the agent's own `~/.claude` folder, and a shell command that redirects there is not caught. A write outside the Work Dir is not refused. Per-Agent tool grants are not built ([roadmap item 9](engineering/roadmap.md)), and asking the human before a tool runs is proposed, not built (item 20). |
| Sandboxing | No. An Agent's work directory is real disk. |
| Authentication | None, by design. Run it on your own machine. |

The wall we expected to hit first, one session per agent spanning every room, is
gone: an Agent now holds one Room Session per room
([ADR-0024](adr/0024-an-agent-holds-one-session-per-room.md)). What remains is the
Adapter. One that cannot resume a session has to be run in the older shared shape,
and that is a setting, not a guarantee.

## Try it

One command installs it, with no admin rights and nothing installed first. On
Windows, in a normal PowerShell window:

```powershell
irm https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.ps1 | iex
```

On macOS, in Terminal:

```sh
curl -fsSL https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.sh | sh
```

It checks your machine, sets the app up in your own folders and opens it in your
browser. If you are not signed in to Claude yet, it prints `claude auth login` for you
to run once. Start it later by typing `huddle`, stop it with `huddle stop`, and run
the same command again to upgrade; your rooms and settings are kept. Real Claude
agents **spend money**.

To work on the code instead, you need the .NET 11 SDK. For a first run that spends
nothing, turn the agent process off, because the development configuration starts a
real one per Agent:

```powershell
$env:Team__Acp__Enabled = 'false'
dotnet run --project src/Huddle.App --urls http://localhost:5100
```

Open `http://localhost:5100`. A fresh install has no rooms. To try the two built-in
demo agents, `echo` and `alpha`, set `Team__DemoAgent__Enabled` to `true` before
`dotnet run`, click **echo**, type `hi`, and it answers. Bringing real Claude agents online from
source takes Node.js and a logged-in `claude` CLI; `Team:Acp:Enabled` is the switch.
[`docs/Huddle.UserGuide.md`](Huddle.UserGuide.md) walks through Agents, Teams,
Tasks and the Library, and
[`docs/Huddle.EngineeringGuide.md`](Huddle.EngineeringGuide.md) holds the whole design.

Built with .NET 11 and Blazor Server, with thousands of tests and zero build
warnings.

## Where this leaves us

Putting the human in every room solves the problem of work happening out of sight.
It does not solve verification: being able to check is not the same as checking,
and a person cannot read everything a team of agents produces. Making the Verify
step something the system does, not only something a person may do, is the next
problem, and the one we are working on now.

A person cannot read everything, but a person can read less. What people should own
is the small surface: the goals, the specs and the acceptance criteria. A Task body
is that spec, written where the Agent who takes it and the person who accepts it
both read it.

The next problem also has a shape. Verifying means checking results against goals:
correctness, security, safety, reliability, scalability, performance,
maintainability and extensibility. Some of these are easy to automate today, such as
security scanners, load tests and performance budgets. Maintainability and
extensibility are harder, and architecture tests and complexity limits only get part
of the way. Agency.Huddle does not run any of these checks for you yet.
