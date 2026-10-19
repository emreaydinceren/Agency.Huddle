---
status: accepted
date: 2026-10-18
---

# A document's history is what its editors report

Each Markdown document in the Library has a history: who created it, edited it, reviewed it or objected to it,
and a one-line summary of each. Agents add to it by calling an App Tool. Huddle adds an entry for the Human's own
saves. Huddle does not check any entry against the file. The full design is in
[Huddle.DocHistory-Specifications.md](../Huddle.DocHistory-Specifications.md).

## The problem

Several Teammates work on one document: Miriam writes it, Mira edits it, Ruth checks its citations and Quinn
objects to a finding. The Human opening it wants to know who did what, and when, without reading a chat transcript.

Huddle cannot see this reliably by itself. Agents are not sandboxed and write files through `Write`, `Edit` or
`Bash` ([rules.md](../engineering/rules.md), *The Work Dir is not a jail*). ACP tool calls name the file for some
Adapters and some tools, never for a `sed -i` in `Bash`. Two Agents may write in the same folder during the same
minute. And "Ruth fact-checked this" or "Quinn objects" changes no byte at all, so no file diff can show it.

## The decision

1. **Agents report their own changes.** A new App Tool, `log_change(path, kind, summary)`, adds one entry to the
   document's history. It is offered to every Agent whose Adapter can read files.
2. **Huddle does not verify, infer or snapshot.** It does not compare an entry with the file, attribute writes from
   tool calls or folder scans, or keep old versions. An edit nobody logs is not in the history, and an entry with no
   matching edit stays there. This is an informed choice to keep the feature small, not a gap to fill later.
3. **The actor is never an argument.** The tool is built per Agent, like `watch_folder`, and writes that Agent's
   Name. An Agent cannot log as someone else.
4. **The Human's saves log themselves.** A save in the Library adds an `Edited` entry under the Human's Name.
5. **Edits outside Huddle are not recorded.** A change made in Obsidian or another editor leaves no entry.
6. **`kind` is one of `Created`, `Edited`, `Reviewed`, `Objected`.** `Reviewed` and `Objected` may describe no change
   to the file.
7. **The history is a machine file, not a document.** It is JSON Lines under `{DataDir}/doc-history/`, keyed by
   Library Root and relative path, never beside the Markdown. Only the App writes it.
8. **A rename or move in the Library carries the history.** A rename outside Huddle leaves it behind.

## Options

- **Infer from ACP tool calls and File Changes scans.** Reaches more edits without the Agent's help, but misses
  `Bash` writes, guesses between Agents working at the same time, and still cannot see a review or an objection.
- **Git commits per save.** Gives diffs and restore, but most Library Roots are not repositories, Agents run `git`
  themselves, and automatic commits in the Human's repository would be unwelcome.
- **Frontmatter in the document.** Rejected by [ADR-0028](0028-the-library-edits-markdown-as-source-and-never-rewrites-it.md):
  the Library never rewrites Markdown.
- **Snapshots of every version.** Would make history a recovery tool for
  [ADR-0029](0029-between-the-human-and-an-agent-the-last-write-wins.md)'s lost writes. A separate feature if lost
  writes happen in practice.
- **Agents report, Huddle records (chosen).** One tool, one file per document, no heuristics, and the only kind of
  history that can say "reviewed" or "objected".

## Consequences

- **The history is a record of claims, not an audit trail.** The UI never presents it as proof of what changed.
- **Completeness depends on the Agents.** The `log_change` description asks for a call after each
  edit; nothing else in the prompts does. An Agent that skips it is invisible.
- **The bet is measured before it is built.** A paid probe drives a real Adapter through seven scenarios, five runs
  each, and must reach its pass rates before `log_change` is implemented (the spec's §10, *Live tests*).
- **The byline and the Recent edits panel read only the history.** Neither looks at the file.
- **Deferred:** telling the Human how many Teammates have been notified of an edit, letting Agents read the history,
  and rejoining history after a rename outside Huddle.
