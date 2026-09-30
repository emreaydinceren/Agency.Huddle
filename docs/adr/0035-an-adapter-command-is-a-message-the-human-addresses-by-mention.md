---
status: accepted
date: 2026-09-30
---

# An Adapter command is a Message the Human addresses by Mention, and an Adapter Profile allows it

An Adapter advertises commands for a session. `claude-agent-acp` advertised 83 in one run, among them
`/compact`, which frees a long-running Teammate's context without the Restart that forgets its
conversation. The Human needs a way to run one on one Teammate. The full design is
[Huddle.Commands-Specifications.md](../Huddle.Commands-Specifications.md).

Three decisions are hard to reverse and surprising without this context, so they are recorded together.

## The decisions

**1. A command is a Message: `@Nova /compact`.** It is posted, stored and delivered like any Message.
When the Human is the sender, the leading Mention is this Teammate, and the name is offered to it, the
runner sends the text after the Mention to the Adapter as the whole prompt. Nothing crosses the pipe that
did not already, and the Reply Gate, Budget, work queue, Stop and idle timeout apply unchanged.

**2. The allowlist is `Team:Acp:Adapters:*:Commands`, on the Adapter Profile.** A Teammate offers a
command only when its Adapter advertises it now *and* its profile names it. An Adapter with no entry
offers nothing. The synthesised Claude profile allows `["compact"]`.

**3. The Mention is required in every Room.** A bare `/compact` in a Room of two is still *"Unknown
command"*. A leading slash in the composer means Huddle's own command (`/invite`), and the Mention form
never starts with one, so the two cannot collide.

## Why

The Adapter recognises a command from the **start** of the prompt. Observed 2026-09-30: a bare
`/compact` ran, and `[Room: probe] Human: /compact` was answered as ordinary text. So a command is
exactly a prompt that opens with `/`, and everything else must not. That single fact is what makes a
Message safe to use as the carrier, provided every ordinary prompt is guarded against opening with `/`
(specification D-9).

Isolation removes the Human's own Claude skills from the advertised list (83 → 51), which is why the
feature can be built before the wider isolation checks pass. It does not remove `/config`, `/mcp`,
`/model` or `/effort`, which is why an allowlist is still needed.

## Consequences

- **A command Turn is an ordinary Turn** with a different prompt. It posts no reply text of its own
  (`/compact` sends none), so the Teammate posts one fixed line reporting the outcome.
- **A command Turn must not drain anything it does not send.** Catch-up, own-post lines and File
  Changes stay queued for the next ordinary Turn.
- **`Huddle.Acp` gains one event record and one mapper case**, which the ACP effort must be told of.
- **The config key is public.** Renaming `Commands` later breaks an installation that set it.
- **A bare `/compact` stays open** for a later version. It needs the composer to know the live list.

## Rejected

**A button or menu that enqueues a Turn directly.** A second door into a Turn bypasses the Reply Gate
and leaves the Room with no record of what ran. It would have to re-implement what a Message already
gets for free.

**An allowlist in Persona frontmatter.** Every non-underscore frontmatter field is shown to other Agents
through `list_agents`, a list-typed `Persona` member breaks `NeedsRestart`'s record equality, and editing
it would restart the session and lose what the Teammate remembers. What a command name means is also a
fact about the Adapter, like `ReadsFiles`.

**One global allowlist.** `compact` means one thing on one Adapter and nothing on another; a single list
cannot be right for two.

**Offering every advertised command.** 51 remain under isolation, and most are wrong to reach from a chat
Room. Two, `/model` and `/effort`, duplicate Huddle's own settings.

**A bare `/compact` taken by the runner in a Room with one Agent.** It needs the composer to tell
Huddle's `/word` from an Adapter's, and makes a legitimate message that starts with `/` ambiguous.

**Two prompt blocks (the command, then Room context).** It changes `PromptAsync`'s signature in the
ACP subtree, and `/compact`'s input is summarisation guidance, which Room context would pollute.
