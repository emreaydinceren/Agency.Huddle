# Agents as colleagues, not a pipeline

*What building a Slack-shaped chat for Claude agents taught us about multi-agent
systems.*

Every explainer on multi-agent systems opens the same way, and it is right. One
model, one context window, one many-sided task — a literature review, a
competitive analysis, a report and a deck — and somewhere around the third
sub-goal the context degrades, the citations drift, and the output confidently
describes a world that is not there. The monolithic bottleneck.

The consensus answer is to stop asking one model to be everyone. Split the work
across specialists. Give them channels to talk on, rules for who acts when, tools
for ground truth, and memory that outlasts a prompt. Five pillars; every
framework claims them.

We agree with all of that. Agency.Huddle is built on those five pillars. Where we part
company is the diagram that usually comes next.

## What makes a team worth having

A team is not a gain just because it has more members. Agents on the same model,
working from the same context, share blind spots and agree with each other as
easily as one agent agrees with itself. The research on why multi-agent systems
fail points the same way: poor role design, agents out of step with each other,
and weak verification (Cemri et al., [arXiv 2503.13657](https://arxiv.org/abs/2503.13657)).
More agents alone adds cost, not quality.

The gain comes from three things together:

1. **Different roles.** Each member has its own job, instructions and tools.
2. **Separate context.** Each member holds only its own part, so its attention is
   not diluted and it does not inherit the author's assumptions.
3. **A real review or test step.** Something checks the result against ground
   truth: a test, a source, or a person.

Put those in a loop and you have **Generate → Challenge → Verify**. One agent
produces the work, a second with its own context hunts for flaws, and a third step
checks the result against something real. Without the last step it is just two
agents agreeing. Everything below is how Agency.Huddle tries to supply all three,
and where it does not yet.

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
forgotten when a new stage is inserted. And the failures the same explainers warn
about — the Scale Fallacy, where agent chatter bloats cost without improving the
answer; the cascading error, where one upstream hallucination is trusted by
everything downstream — are exactly what happens in the part of the diagram
nobody is watching.

## Put the human in the room

Agency.Huddle is a chat app. It looks like Slack because Slack already solved this
problem for people: work happens in rooms, and you are in the rooms that concern
you.

The other members of your rooms are Claude processes. Each is a separate `claude`
session, spawned as a child process, connected to the app over a named pipe. They
read the room, answer when spoken to, start rooms of their own, and pull each
other in when a question is not theirs.

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
Nobody has to remember to insert the checkpoint. The Scale Fallacy is still real
— two agents can still tag each other — but it happens in a room you can open, and
a cap stops it (see [What we have not solved](#what-we-have-not-solved)).

That human is also the **Verify** step. A person who is in the room at the moment
a claim is made can ask for the source, which a gate at the bottom of a pipeline
cannot do for a mistake made three stages earlier.

## Five pillars, five answers

**Specialised agents.** A **Teammate** is a Markdown file,
`Teammates/<Name>/<Name>.md`. Its frontmatter says who it is and what it knows; its
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

The fields other Teammates may see compose into a job description, which is what
lets one agent bring in the right colleague instead of guessing at a name. The body
never reaches anyone else, so a character can hold goals the others do not see.
A Teammate's **Model**, **Effort** and **Work Mode** (how much it may do before it
must ask) are set per Teammate, and so is its **Adapter**, the AI process behind it,
so one team can span vendors and different blind spots are a setting rather than a
project. A **Skill** is know-how an agent reads on demand, and the built-in Chief of
Staff proposes a whole team from it: nothing is created until you select Approve.
Teammates group into **Teams** and work on **Projects**, which are folders.

**Communication channels.** A named pipe, one JSON object per line. An agent
exists in Agency.Huddle for exactly one reason: some process connected to the pipe and
said `hello`. That is the entire contract. A forty-line PowerShell script that
echoes text back is a full member of any room, and so is a real Claude session —
the chat surface cannot tell them apart, and that is the point. Any language, any
process, one line of JSON.

**Coordination rules.** The reply rule is still one pure function:

```csharp
return memberCount <= 2 || mentioned || following ? ReplyDecision.Reply : ReplyDecision.CatchUp;
```

Two members is a private conversation, so the agent answers everything. Three or
more is a group, so it answers only when `@`-mentioned, or when it has chosen to
follow the room. A room's behaviour comes from how many members it has. There is
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
each Teammate has its own memory folder and its own **Room Session** per room, so the
context of one conversation does not leak into another. Work that must outlast a
chat lives in **Tasks**, a Markdown file with an owner, a status and a change log,
and in the **Library**, where you and your Teammates keep notes side by side. A
Teammate is told which watched files changed since its last turn. There is no vector
store; the memory is files you can open.

## Stages are verbs, not people

The standard pipeline names its stages as if they were staff: Planner,
Researcher, Analyst, Critic, Writer. It is a natural reading, and we think it is
wrong.

Those are things a colleague *does*, not who a colleague *is*. A CFO plans,
researches, analyses, critiques and writes, depending on what you asked. Hire a
"Critic" and you have fragmented one person into five, none of whom knows the
numbers well enough to critique them.

So in Agency.Huddle the verb goes in the request:

```text
@CMO research what competitors ran last Valentine's Day
@CFO critique the margin assumptions above
```

Two Teammates, two verbs, no mechanism. Mention-gating routes it. The CFO
critiques the numbers *because* it is the CFO.

## Topology without an engine

The other diagram in every explainer is peer-to-peer mesh versus hierarchical
tree, with a selection matrix for choosing between them. Most frameworks make you
pick up front.

Agency.Huddle is a mesh by default: a room of peers, every member sees every message. It
becomes a tree the moment one agent creates a room, seeds it with the context the
specialists need, and follows it. The Chief of Staff you asked for a Valentine's
strategy starts a room with the CMO and the CFO, writes the brief as the first
message, and brings the outcome back to you. You are in that room too; you are
just not interrupted by it.

Reports on multi-agent systems name three layouts: **centralized** (one coordinator
routes everything, easy to follow but a bottleneck), **peer-to-peer** (agents talk
directly, flexible but hard to track and prone to drift) and **hierarchical** (leads
supervise sub-teams, scales but costs more hand-offs). Most tools ask you to choose
one before you start.

Nothing switches modes. There is no room kind. Topology is whatever falls out of
which Teammates exist, which tools each holds, and who is in the room. The design
record — including why we rejected a blocking `delegate()` tool, and why relying
on specialists to tag the coordinator back turned out to be a silent failure
path — is in
[ADR-0005](adr/0005-agent-topologies-are-emergent.md).

## What we have not solved

A team fixes none of its own failure modes by default. Four are worth saying plainly:

- **Collaboration is not automatic value.** Teammates talking to each other is not
  a review. Mention a challenger to attack a specific piece of work.
- **Cascading errors.** If one Teammate makes a wrong assumption early and the next
  trusts it, every later step builds on it and the work looks more finished, not
  less. The fix is to verify at each hand-off, not only at the end.
- **Cost.** Every hand-off is more model calls. A team costs more than one agent
  doing the same job.
- **Mental load moves, it does not vanish.** Setting up roles and reviewing a team's
  output is work. The payoff comes on repeated or long-running jobs. Start with one
  Teammate, and add a team when coordination, not capability, is what slows you down.

And here is where Agency.Huddle stands on the guardrails an autonomous network needs:

| Guardrail | Status |
| --- | --- |
| Human-in-the-loop | **Structural.** Every room, by schema. |
| A verify step | **Partly.** The human is always in the room and Tasks name an owner, but there is no built-in test step. A team that has only a Generate and a Challenge Teammate has no Verify. |
| Runaway-loop guard | **Yes.** A per-room Budget of agent replies (40 by default) that any human message resets, a per-Teammate token Budget, and an idle timeout on a silent Turn. **Stop** cancels a Turn. |
| Seeing what an agent did | **Mostly.** A reply shows its recent tool calls, an edit opens to a preview, a Teammate card shows its reported spend, Task changes are logged, and file changes are reported to the next Turn. None of the tool-call detail is kept after the reply. |
| Least-privilege tool access | **Partly.** A Work Mode limits what a Teammate may do before it asks, and writes outside its Work Dir are refused. Per-Teammate tool grants are not built ([roadmap item 9](engineering/roadmap.md)), and asking the human before a tool runs is proposed, not built (item 20). |
| Sandboxing | No. A Teammate's work directory is real disk. |
| Authentication | None, by design. Run it on your own machine. |

The wall we expected to hit first, one session per agent spanning every room, is
gone: a Teammate now holds one Room Session per room
([ADR-0024](adr/0024-an-agent-holds-one-session-per-room.md)). What remains is the
Adapter. One that cannot resume a session has to be run in the older shared shape,
and that is a setting, not a guarantee.

## Try it

Requires the .NET 10 SDK. For a first run that spends nothing, turn the agent
process off, because the development configuration starts a real one per Teammate:

```powershell
$env:Team__Acp__Enabled = 'false'
dotnet run --project src/Huddle.App --urls http://localhost:5100
```

Open `http://localhost:5100`. Two rooms, `echo` and `alpha`, are already there.
Click **echo**, type `hi`, and it answers. Bringing real Claude agents online takes
Node.js and a logged-in `claude` CLI, and **spends money**; `Team:Acp:Enabled` is
the switch. [`docs/Huddle.UserGuide.md`](Huddle.UserGuide.md) walks through
Teammates, Teams, Tasks and the Library, and
[`docs/Huddle.EngineeringGuide.md`](Huddle.EngineeringGuide.md) holds the whole design.

.NET 10 and Blazor Server, 5,284 tests, zero build warnings.
