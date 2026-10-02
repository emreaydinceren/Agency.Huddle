# Agency.Huddle — domain context

A Slack-style chat surface where one human converses with several agent
processes. Exists so agent experiments share one persistent, multi-party
conversation UI and one stable wire contract.

This page holds what has no other source: the dialogue below, and the note on
the old words. It used to carry a copy of the glossary as well, which drifted —
the **Invitation** definition sat a door and a half behind the source for as
long as `InviteAsync` had three callers and this file said one.

> [!IMPORTANT]
> The vocabulary lives in [Language](language.md) and the cardinality rules in
> [Relationships](../Huddle.EngineeringGuide.md#relationships). Link to those; do not copy
> them back here. A second copy has no compiler behind it and goes stale in
> exactly one direction.

Everything else — architecture, the rules that are not visible in the code,
traps, configuration, how to build and run — is reachable from
[Huddle.EngineeringGuide.md](../Huddle.EngineeringGuide.md).

## Example dialogue

How the terms interact, and where the boundaries between them fall:

> **Dev:** "When the Human invites a second Agent into `echo`'s two-Member
> **Room**, does `echo` lose its private line?"
> **Domain expert:** "That Room now has three Members, so the **Reply Gate**
> starts asking for a **Mention** there. The next time `echo` reconnects, a new
> two-Member Room named `echo` is created. Nothing is lost; the larger Room keeps
> its **Transcript**."
>
> **Dev:** "Do all Agents reply to every **Message** in a Room of three or more?"
> **Domain expert:** "Every **Member** receives every **Message**. Each Agent
> sees whether it was **Mentioned**; only a Mentioned Agent replies once a Room
> has three or more Members. In a two-Member Room there is only the Human and one
> Agent, so the Agent answers every Message regardless of Mention — there is
> nobody else it could be for. The server labels, it never decides."

## Where the old words went

The vocabulary changed on 2026-09-11. The full mapping, and the reasoning behind
it, is in [`decisions.md`](decisions.md) — including the three earlier
ambiguities this file used to flag: "user" for both the person and the bots,
"agent" for both the process and the chat identity, and
"chat"/"channel"/"conversation" for one thing. Every other Markdown file in the
repository predates the change and is kept as a dated record; do not read its
wording as current.
