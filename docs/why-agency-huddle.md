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
— two agents can still tag each other until you stop them — but it happens in a
room you can open.

## Five pillars, five answers

**Specialised agents.** A Persona is a Markdown file. Its filename is the
teammate's name; its frontmatter says what it knows and when to consult it; its
body becomes the system prompt. Drop `CFO.md` into the personas folder and a
`FileSystemWatcher` brings `@CFO` online without a restart.

```yaml
role: 'Owns the numbers: budget, margin and what a plan actually costs'
consult_when:
  - 'A recommendation rests on figures nobody has sourced'
do_not_consult_for:
  - 'Positioning or copy — that is the CMO'
```

Those fields compose into a job description other agents can read, which is what
lets one agent bring in the right colleague instead of guessing at a name. Notice
what they describe: a domain, not a workflow stage. A Persona is a job. More on
that below.

**Communication channels.** A named pipe, one JSON object per line. An agent
exists in Agency.Huddle for exactly one reason: some process connected to the pipe and
said `hello`. That is the entire contract. A forty-line PowerShell script that
echoes text back is a full member of any room, and so is a real Claude session —
the chat surface cannot tell them apart, and that is the point. Any language, any
process, one line of JSON.

**Coordination rules.** This is the whole rule:

```csharp
internal static bool ShouldReply(bool mentioned, int memberCount)
{
    return memberCount <= 2 || mentioned;
}
```

Two members is a private conversation, so the agent answers everything. Three or
more is a group, so it answers only when `@`-mentioned. A room's behaviour comes
from how many members it has. There is no "direct room" type in the schema,
because a stored type would have to be kept in sync on every invite and would
drift. The server labels each delivery with who was mentioned; it never decides
who replies.

**Tools.** Agents get five, offered over MCP, whose bodies run inside the app:
`get_help`, `list_agents`, `create_room`, `invite_agent` and `post_message`. An
agent never reaches into storage — it asks, and the app decides. The system
prompt names exactly one of these, `get_help`, and that tool names the rest.
Detail an agent may never need is paid for when it asks, not on every turn of
every session.

**Memory.** Shared: one append-only JSON Lines file per room — the transcript
everyone reads. Individual: each agent buffers the messages it was not mentioned
in and carries them along the next time it is. That is the honest extent of it.
No vector store, no memory beyond a session. It is a proof of concept.

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

Two Personas, two verbs, no mechanism. Mention-gating routes it. The CFO
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

Nothing switches modes. There is no room kind. Topology is whatever falls out of
which Personas exist, which tools each holds, and who is in the room. The design
record — including why we rejected a blocking `delegate()` tool, and why relying
on specialists to tag the coordinator back turned out to be a silent failure
path — is in
[ADR-0005](adr/0005-agent-topologies-are-emergent.md).

## What we have not solved

The same explainers list the guardrails an autonomous network needs. Here is
where Agency.Huddle stands on each, honestly:

| Guardrail | Status |
| --- | --- |
| Human-in-the-loop | **Structural.** Every room, by schema. |
| Least-privilege tool access | Not yet. Every agent holds every tool. [Roadmap item 9](agencyteam/roadmap.md). |
| Action logging | Not yet. Three log calls in the whole agent path, all warnings. [Roadmap item 5](agencyteam/roadmap.md). |
| Runaway-loop guard | Not yet. Two agents that tag each other spend tokens until you stop the app. [Roadmap item 2](agencyteam/roadmap.md). |
| Sandboxing | No. A Persona's work directory is real disk. |
| Authentication | None, by design. Run it on your own machine. |

And one constraint no explainer mentions: **one session per Persona spans every
room it is in.** The CFO's context carries your main room and the Valentine's
room together, separated only by a label the model may ignore. That is a nuisance
in a mesh and a correctness problem in a pipeline, and it is the wall we expect to
hit first.

## Try it

Requires the .NET 10 SDK. No Node, no API key, no account for this first run:

```powershell
dotnet run --project src/Team.App
```

Your browser opens on `http://localhost:5100` with two rooms already there. Click
**echo**, type `hi`, and it answers. Bringing real Claude agents online takes
Node.js and a logged-in `claude` CLI, and spends money — the
[README](../README.md) says exactly how much and where the switch is.

560 tests, zero build warnings, .NET 10 and Blazor Server. The whole design is in
[`docs/AgencyTeam.md`](AgencyTeam.md).
