---
status: accepted
date: 2026-09-24
---

# The Library edits Markdown as source and never rewrites it

The Library edits a Markdown file as **source text** in CodeMirror 6, with a Read, Edit and Split
toggle, and renders it through Huddle's existing safe Markdig pipeline. A save writes back
exactly what is in the editor, and nothing else in the file changes. Obsidian syntax is
**kept, not yet rendered**, apart from wikilinks. The full design is
[Huddle.Library-Specifications.md](../Huddle.Library-Specifications.md) (§6.4–§6.7).

## The problem

The files the Library opens are shared with two other writers. Agents write them with their own
tools, and a pinned root may be an Obsidian vault the Human also edits in Obsidian. An editor
that normalises what it saves would change files nobody asked it to change. It could reflow
Markdown, reorder frontmatter keys, swap CRLF for LF or drop a BOM, and the Agents would then be
told those files changed ([ADR-0023](0023-an-agent-learns-of-file-changes-on-its-next-turn.md)).

## The decision

**The editor is CodeMirror 6.** It is vendored into `wwwroot/lib/codemirror/` and loaded as one ES
module behind a single JS interop boundary (`library-editor.js`). It uses the Markdown language
mode, has Ctrl+S to save and is read-only for every file type except `.md`.

**A save is byte-exact.** The file's encoding, its BOM and its dominant line ending are detected
on load and restored on save. The text in between is what the Human typed. There is no
formatter, no frontmatter round-trip through a YAML model and no trailing-newline fix-up.

**Rendering reuses `MarkdownRenderer`.** The pipeline keeps `DisableHtml()` and the link
rewriter, and never calls `UseAdvancedExtensions()` ([rules.md](../agencyteam/rules.md)).
Wikilinks are added the same way Task ids were: a pass over the parsed document's
`LiteralInline`s with an `ILibraryReferenceResolver`, beside the existing
`LinkTaskReferences`. There is no new Markdig parser extension.

**Obsidian syntax other than wikilinks shows as written.** Callouts (`> [!note]`), embeds
(`![[x]]`), `==highlight==`, `%%comments%%` and `#tags` render as plain Markdown would render
them. They are never stripped, so the file stays correct in Obsidian.

**Renaming or moving a note is the one multi-file write.** Every `[[link]]` in the same Library
Root that resolves to the old path is rewritten to the new one, as Obsidian does. The confirm
dialog names the count first ("Rename and update 7 links in 4 notes"). Each rewritten file gets a
byte-exact save of the minimal change: only the link text differs.

## Rejected

| Alternative | Why not |
| --- | --- |
| WYSIWYG (Tiptap, Milkdown) | Round-trips Markdown through a document model, so opening and saving a file reformats it |
| Obsidian-style live preview | The most JS of any option, for a first version whose job is quick fixes |
| A `MudTextField` with a preview toggle | No syntax highlighting, no line numbers and no read-only code view. It was accepted for Task descriptions, which are short |
| Monaco | Several MB, built for code rather than prose, and heavier to theme against MudBlazor variables |
| Markdig extensions for callouts, embeds and highlight in v1 | Each one is new HTML output that has to be checked against the injection rule. Deferred until the Library is in use |
| `UseAdvancedExtensions()` to get Obsidian features cheaply | Generic attributes make `{onclick=…}` live. Forbidden by `rules.md` |
| Warning on rename instead of rewriting links | The Human chose Obsidian's behaviour; a vault shared with Obsidian would otherwise drift from it |

## Consequences

- **Huddle ships its first third-party JS library** besides MudBlazor. It is vendored, pinned and
  loaded only when the Library pane is open. CodeMirror's theme reads only `--mud-palette-*` and
  `--mud-typography-*` variables, so the two stylesheet tests keep passing.
- **A link rewrite on rename touches files the Human did not open,** and can collide with an
  Agent writing one of them at the same moment. Under
  [ADR-0029](0029-between-the-human-and-an-agent-the-last-write-wins.md) the later write wins.
- **Callouts and embeds look plain** until a later version renders them. That is a display gap,
  not data loss.
