---
status: accepted
date: 2026-10-29
---

# `@all` Mentions every Agent in the Room

In a Group Room an Agent answers only when it is Mentioned
([ADR-0003](0003-mention-gated-replies-and-membership-defined-direct-rooms.md)). Asking every Agent the same
question meant writing each Name. This record adds one handle that Mentions them all.

## The decision

**`@all` Mentions every Agent Member of the Room. It is resolved by `MentionParser`, into the same list of
Mentioned Members a Name resolves to, so nothing downstream knows it was written.**

- It Mentions Agents only, never the Human. It asks the Agents for a Turn; a Mention of the Human would
  raise a Needs You card for every `@all` an Agent writes.
- It obeys the Name rules: matched case-insensitively, after an `@` that is not joined to the character
  before it, and ending at a word boundary. `@allison` and `team@all` are not `@all`.
- A Name or Alias wins over it. A Member called `All Hands` is read as that longer Name, and a Member called
  `All` is read as that Member, because Names and Aliases sit ahead of `@all` in the candidate list. `all`
  is therefore not a reserved Name.
- The Human and the Agents may both write it. The chat rules and `get_help` tell every Agent what `@all`
  means and to write it only when every other Agent's answer is needed.
- It is drawn as a Mention pill, from the same handle list the parser uses.

The reply decision is unchanged: `ReplyGate` still sees a `mentioned` flag per Agent. That keeps the server
labelling and each Agent deciding (ADR-0003), and a pipe client gets the expanded `mentions` list on
`MessagePosted` with no protocol change.

## Consequences

- One `@all` from an Agent wakes every other Agent at once, so it spends the Budget faster than a single
  Mention. The Budget ([ADR-0006](0006-a-room-has-a-budget-for-agent-replies.md)) is still checked first and
  still caps the run; `@all` adds no loop the Budget does not already stop.
- If any Agent in the Room is unreachable, the Room reports `MentionedUnreachable` for the whole Message, as
  it does when that Agent is Named. The reachable Agents still answer.
- `@all` is not an Adapter command target: `@all /compact` runs no command, because a command is addressed
  to one Agent's own handles ([ADR-0035](0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md)).
- In a Direct Room `@all` changes nothing; the one Agent answers every Message anyway
  ([ADR-0004](0004-direct-rooms-reply-without-mention.md)).
