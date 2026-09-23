---
status: accepted
date: 2026-09-22
---

# An Agent holds one session per Room, and only Memory and File Changes cross between them

An Agent's working context becomes per Room: one **Room Session** for each (Persona, Room). What
makes the Agent one Teammate stays per Persona and is shared by all its Room Sessions: its Persona
text, Work Dir, Memory, App Tools and Adapter process. Continuity between Rooms comes only through
Memory and File Changes. The full design is
[Huddle.RoomSessions-Specifications.md](../Huddle.RoomSessions-Specifications.md). It lifts the
known limit "One session per Persona spans every Room it is in", and builds on
[ADR-0023](0023-an-agent-learns-of-file-changes-on-its-next-turn.md).

## The problem

Today one session per Persona spans every Room the Persona is in. `PersonaSupervisor` keys one
`PersonaRunner` per Persona Name (`PersonaSupervisor.cs:33`), and each runner holds one
`IAgentSession` (`PersonaRunner.cs:79`) and one work queue (`:45`). The only thing telling Rooms
apart inside that session is the `[Room: name (id: …)]` label on each prompt.

At small scale the label works; a live test on 2026-09-22 confirmed it. Over a long run it is a
convention the model may ignore, and the repo owner named four ways it fails:

- **A terse follow-up resolves by recency.** "Go with option 2" picks the nearest option 2 in the
  context, whichever Room offered it.
- **Compaction merges Rooms.** One context is summarised into one text that no longer says which
  Room decided what.
- **Catch-up arrives out of order.** Room A's missed Messages land after Room B's later Turns.
- **With tools, a wrong reference becomes a wrong action.** "Book it" writes the wrong file.

The same live test found a preference leaking between Rooms, through the session and through
Claude Code's own auto-memory; Agents making false claims about their own memory; two same-named
Rooms merged in the model's account; and the Human's own Claude Code output style in Agent replies.
Research points the same way: the LTM Benchmark (NeurIPS 2024) on interleaved tasks, and task
interference (EMNLP 2024) measuring drops of roughly 10 to 19% from unrelated earlier turns. Every
framework checked, ACP included, scopes working context per conversation and shares through an
explicit store.

## The decision

**One session per (Persona, Room).** A Room Session holds one Room's Turns and nothing else. There
is nothing in its context from another Room to confuse a follow-up with, to merge in a summary, or
to act on.

**One Adapter process per Persona, hosting its Room Sessions.** `claude-agent-acp` keeps many
sessions per process, and spawns one Claude Code CLI child per session. The factory is split so the
Adapter process and the `AppToolServer` start once, and sessions open later. A dead child takes one
Room Session, which reopens; a dead Adapter process takes one Persona's sessions, as it takes its one
session today.

**Lazy, bounded and resumable.** The Room with the Human opens at start, so authentication failures
still surface there. Other Rooms open on their first Turn, close after `SessionIdleMinutes`, and are
capped at `MaxLiveSessions` per Persona. A closed session is resumed by stored id where the Adapter
advertises `resume`, which `claude-agent-acp` does.

**A new session is never blank.** Its first Turn carries the Room's Messages it has not seen, from
the Transcript, ending before the Message that started the Turn so that independent first answers
hold. `PersonaRunner` stays an ordinary pipe client: it asks with a new `ReadTranscript` Envelope
and the server answers with `TranscriptTail`. Both are additive, and the answer is sent only to a
client that asked, so `ProtocolVersion` stays 3.

**Serial across Rooms by default.** `MaxConcurrentTurns` is 1, which is today's behaviour. Parallel
Turns are one setting away, after the stress test has measured isolation on its own.

**Stop, the idle watchdog and `lastUsed` are per Room Session; health and the token Budget stay per
Persona.** Stop ends one Room's Turn and queue. The token Budget stays a sum, because it exists to
catch a loop that mints fresh Rooms. The failure streak stays one per Persona, because its usual
causes are shared.

**The system prompt tells the truth**, with one text for Room Sessions and one for a shared session,
chosen in code. Room identity stays out of the system prompt.

**The Human's own Claude Code configuration is kept out** of Persona sessions, so Memory has one
visible home. The Adapter's code shows two per-session routes, `settingSources` and
`autoMemoryEnabled` through `_meta.claudeCode.options`. Both are verified before anything relies on
them.

## Why continuity comes only through Memory and File Changes

Some things should cross Rooms: "I prefer C#" said once should hold everywhere. A shared context
carried that by accident, invisibly, alongside everything that should not have crossed. Memory
carries it on purpose: one file per fact in the Agent's Work Dir, indexed into every new session's
system prompt. File Changes tells each Room Session what changed since that Room last looked, and
marks an edit *by you, in Room 'X'* when another Room Session of the same Agent made it. Both are
visible and the Human can edit them. So Room Sessions are built **after** File Changes and Memory:
built first, a preference stated in one Room would vanish from the others with nothing to carry it.

A coordinator such as the Chief of Staff loses the cross-Room overview it had by accident. It gets
it back from Memory, from reports posted to it, and from the seed Message of each Room it creates.

## Rejected

| Alternative | Why not |
| --- | --- |
| Keep one session per Persona, with better labels and Prompts | A label is a convention the model may ignore, and the four failures are about long runs. The better Prompt ships anyway, as Phase 0 |
| One session per Turn, rebuilt from the Transcript | Re-reads the whole history every Turn, and loses tool results and the Agent's own working |
| One Adapter process per Room Session | Doubles the processes; each session already has its own CLI child |
| One Adapter process for every Persona | Every Teammate behind one crash and one environment |
| `session/load` instead of resume | It replays history through the live event stream |
| The runner reading the Transcript file, or a read App Tool | The first breaks the rule that everything a runner knows arrives in an Envelope. The second relies on the model calling it on the right Turn |
| A token Budget per Room | It would reset on every fresh Room, the loop it exists to catch |
| Parallel Turns by default | Raises the spend rate and makes concurrent Work Dir writes normal before anyone has watched them |
| Exempting coordinators | A coordinator is where two projects' decisions are likeliest to be merged |

## Consequences

- **Phase 0 ships first, without the refactor:** the truthful shared-session Prompt, Stop per Room
  inside the one session (the fix already open), a label suffix for same-named Rooms, and isolation
  from the Human's Claude Code configuration.
- A Persona costs one Adapter process plus up to `MaxLiveSessions` CLI children, where it cost one
  plus one. Memory per child and resume latency are unmeasured; a manual test records them.
- **The Restart button forgets a Persona's sessions but not its Rooms:** each Room's next Turn opens
  fresh with its recent Messages. An app restart resumes every Room.
- `Huddle.Acp` gains `ResumeSessionAsync`, a `SupportsResumeSession` flag and a `_meta`
  pass-through, and `FakeAcpAgent` gains several sessions and resume. Those belong to the ACP
  effort. `FakeAcpAgent.cs` is linked into `Huddle.MockAdapter`, so its change reaches two
  assemblies.
- `IAgentHostFactory`'s signature changes, which `PersonaSupervisor` had deliberately kept frozen.
  `FakeAgentHostFactory` keeps every existing test working by returning its one `Session` from the
  first open.
- A new per-Persona store, `{DataDir}/room-sessions/<Name>.json`, joins `PersonaRenameCascade`.
- An Adapter Profile gains `SessionPerRoom`. `agency-acp` sets it `false` until it is shown to hold
  several sessions on one process, and runs exactly as today.
- Stop no longer stops an Agent everywhere. `language.md`'s Stop entry, and the two known limits
  about one session spanning every Room, change when this ships.
- Claude Code's own session transcripts stay in the Human's profile, where resume reads them. If
  they are lost, the Room opens fresh from the Transcript, and nothing is lost that the Transcript
  does not hold.
