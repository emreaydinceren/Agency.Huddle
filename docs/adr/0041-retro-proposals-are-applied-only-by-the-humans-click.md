---
status: accepted
date: 2026-10-08
---

# Retro proposals are applied only by the Human's click

After a retrospective, the Team's manager or Retro agent may propose up to three edits to the
Team's own files. An Agent can propose, but never edit: the proposal is held as data, the Human
sees it as a card with a diff, and Huddle makes the edit when the Human clicks **Accept**. The
Human's view is in the [User Guide](../Huddle.UserGuide.md) (*Retrospectives*); the original Teams
specification has been retired.

## The problem

A retro exists to change how the Team works, and the files that say how are plain Markdown and
JSON: `Process.md`, `_team.md` and the Agent files. An Agent that could edit them directly
could rewrite its own gates, its own role or its own prompt, and the next retro would be graded
by the process it just loosened. The Human wants the retro to suggest, and to stay the one who
changes the rules.

A second problem is the record. A retro that decides "do X" and never checks X leaves nothing
that tells the next retro whether the change worked.

## The decision

**The tool stores a proposal; it writes no Team file.** `propose_retro_changes` takes up to
three changes, each a `targetFile` (`Process.md`, `_team.md` or a member's `Name.md`), a text to
find and a text to replace it with. `RetroProposalStore` keeps the set under
`Teams/<Team>/_proposals/retro/<id>.json`, so a restart keeps it. The folder is separate from the
Sprint proposals' `_proposals` files, which are read as Sprint proposals.

**Only the manager or the Retro agent may propose.** `RetroCaller.Check` requires the caller to
be a member of the named Team, to hold the `retrospective` Skill, and to be the Team's manager
or the member whose role is the type's `retroRole`. A specialist that holds the Skill is refused.

**Accept re-reads the file and applies exactly one match.** The card goes through
`RetroProposalService.AcceptAsync`, which, under one lock:

- re-reads the target file as it is now, not as it was when the proposal was made;
- refuses the change when the text to replace is absent or occurs more than once, and changes
  nothing;
- for `_team.md`, parses the edited text with `TeamFile.TryParse`, refuses when it would be
  unreadable, and writes through `UpdateTeamFile` rather than the raw text;
- for `Process.md`, writes through a temporary file and a move over the original;
- for an Agent file, refuses an edit whose `name:` no longer matches the Agent, so a retro
  cannot rename anyone.

**The register row is Huddle's, not the agent's.** On a successful edit, Huddle appends a row to
the retro register with the proposer, the change title, `Edited <file>` and the status Done, and
removes the change from the pending set. A failed append is logged and does not undo the edit.
**Reject** drops the change and writes nothing to the Team's files or register.

**The register has five statuses, and a later retro closes them.** A row is Open, Done, Partial,
Verified or Changed. `RetroVerifier.Due` returns the rows marked Done or Partial for the next
retro to check. The `mark_retro_row` tool, offered to the same callers, moves one of them to
Verified, Partial or Changed, and Verified is refused unless the evidence text links a Task id,
a `logs/...#L12` line or a `_retros/` file. A row is therefore never verified on opinion alone.

## Rejected

| Alternative | Why not |
| --- | --- |
| The Retro agent edits the files, the Human reviews afterwards | The agent would be changing the process it is graded by, and a review after the fact is a revert, not a gate |
| Apply the diff computed when the proposal was made | The file may have changed since. Re-reading and requiring exactly one match makes a stale proposal fail loudly instead of corrupting the file |
| Let the agent write its own register row | The row records what was done. Huddle wrote the file, so Huddle states what changed |
| Auto-accept small changes | The size of an edit does not tell whether it loosens a gate |

## Consequences

- **A retro changes the Team only when the Human says so.** Without a click, the proposals wait,
  and nothing is applied.
- **A stale proposal is refused, not forced.** If the file moved on, the card says so and the
  proposer can propose again.
- **`_team.md` and the Agent files cannot be left half-written or unreadable by a retro.**
  The first is validated and written through the Team file path; the others are replaced whole.
- **The next retro starts from a list.** The Done and Partial rows are the questions it has to
  answer with evidence before it opens new ones.
- **The register is kept by the app.** An agent can mark a row, with evidence, but cannot add one.

Code: `src/Huddle.App/Teams/RetroProposals.cs`, `RetroProposalService.cs`, `RetroFiles.cs`,
`RetroVerifier.cs`, and `src/Huddle.App/Acp/Tools/ProposeRetroChangesTool.cs`,
`MarkRetroRowTool.cs`, `RetroCaller.cs`.
