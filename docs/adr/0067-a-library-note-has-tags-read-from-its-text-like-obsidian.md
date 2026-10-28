---
status: accepted
date: 2026-10-28
---

# A Library note has tags, read from its text like Obsidian

A Library note can carry **tags**: `#tag` in its body and a `tags` list in its frontmatter,
merged into one set per note, with nested tags (`#work/active`) and case-insensitive matching, as
Obsidian does. Huddle only **reads** them. The tags are collected during the pass that already
builds `WikiLinkIndex` and are shown in a Right-sidebar Tags panel. No file is ever rewritten to
add, rename or normalise a tag. The design is
[Huddle.Library-Specifications.md](../Huddle.Library-Specifications.md) (§6.17).

## The problem

The Library opens files that a Human also edits in Obsidian, and a pinned root may be a whole
vault. The Human's tags there are already written.
[ADR-0028](0028-the-library-edits-markdown-as-source-and-never-rewrites-it.md) left `#tags` as
plain text, and the Library has no way to ask "which notes are tagged `project`?". A Human who
tags notes in Obsidian expects the same tags to mean the same thing here.

A Task's `tags` ([Huddle.Tasks-Specifications.md](../Huddle.Tasks-Specifications.md) §7.2) are a
separate field on a different kind of file. They are not touched.

## What Obsidian documents, and what it does not

The [Obsidian tags page](https://obsidian.md/help/tags) documents: inline `#tag` and the `tags`
property (as a YAML list), nesting on `/` with a search for `inbox` matching `inbox/to-read`,
letters, numbers, `_`, `-`, `/` and "commonly accepted Unicode characters, including emojis and
other symbols", the rule that a tag needs at least one non-numeric character, case-insensitivity,
and display with the casing first created.

It does not document the word boundary before `#`, trailing or doubled slashes, escapes, comments,
a `#` inside frontmatter or a comma-separated `tags` string. (Obsidian's Properties page documents the singular `tag` as deprecated in 1.4 and dropped as a default property in 1.9.) The spec takes
those rules from the Human's statement of Obsidian's behaviour and marks each one **verify against
a vault**. Where a vault disagrees, the vault wins.

## The decision

**Two sources, one set.** A note's tags are the union of its inline `#tag`s and its frontmatter
`tags`. A tag written both ways counts once. The singular `tag` key is not read, because
Obsidian deprecated it in 1.4 and dropped it as a default property in 1.9.

**Inline syntax.** A tag is `#` followed by characters up to the first whitespace or ASCII
punctuation other than `_`, `-` and `/`. Unicode letters, combining marks, digits, symbols and
emoji are allowed. At least one character must be a non-digit. The `#` does not start a tag when
the character before it is a letter or a digit (`word#tag`), but does after whitespace or
punctuation (`(#tag)`, `**#tag**`, `,#tag`), except `&` and `#` (`&#x2014;`, `##tag`). The ASCII
terminators are listed in the spec, so `#c++` is `c`. Escaped `\#tag`, a `#` inside a wikilink, code,
`%% … %%` or `<!-- … -->` is not a tag; a `#x` in a heading is.

**Slashes.** A trailing `/` is dropped (`#work/` is `work`), a leading `/` is not a tag, and a
doubled `//` ends the tag (`#a//b` is `a`).

**Where the frontmatter is.** One helper, `NoteFrontmatter.Split`, decides: a first line of exactly
`---` and a closer that is exactly `---` or `...`. A block that never closes is body. The tag pass,
`NoteTagReader` and `LibraryDocument` all use it, and Markdig is run over the body only, so a stray
fence in the frontmatter cannot hide a body tag.

**Frontmatter syntax.** `tags` is a YAML list. The flow list `[a, b]` and the comma string
`a, b` are also read, as deliberate leniency. A quoted `"#a"` is the tag `a`; an unquoted `#a`
is a YAML comment and gives no tag. A value is skipped, not truncated, when it contains a character
that would end an inline tag. A malformed `tags` entry gives no frontmatter tags; the other keys
are not validated.

**Matching is case-insensitive and nested.** `#Inbox` and `#inbox` are one tag. Asking for `work`
returns the notes tagged `work` or anything under it. Obsidian displays the casing from creation
time, which a read-only index does not have, so the display casing is a house rule: frontmatter
before body, then notes in ordinal path order.

**A Task id is not a tag.** `#PLAT-0042` satisfies the tag syntax, but an id is a Task's name and the
renderer may link it when Tasks are enabled and it resolves. A tag whose whole text or first
segment is a valid Task id (`TaskId.TryParse`) is left out of the index, from either source, so
the renderer and the index cannot drift.

**One frontmatter reader.** `NoteTagReader` reads the `tags` key's lines itself, because
`PersonaFrontmatter` loses the quoting and does not treat `#` as a comment. The Properties block
(`LibraryProperties`) draws its `tags` row from the same reader, so the chips it shows and the
tags the index holds cannot disagree. When the reader finds nothing for the entry, the row shows
the raw value as plain text, never as chips.

**The index is derived and shared.** Tags are collected in the build of `WikiLinkIndex`, from the
text it already reads, and stored in that root's cached index, which gains two internal
accessors. `LibraryTagIndex` builds the counts and display casing from them and drops them on
`WikiLinkIndex.Invalidated`. It has the same lifetime, invalidation and `MaxIndexedFiles` limit,
and a note is not read a second time.

**The surface is the Right sidebar.** A Tags panel shows the open note's tags and every tag in the
Library Root with a count. A tag opens, inside the panel, the list of notes carrying it. The panel
loads off the circuit and has a Refresh button. Tags are edited as text in the editor.

**Nothing is rewritten.** Renaming or moving a note does not touch tags. There is no "rename tag"
command in v1.

## Rejected

| Alternative | Why not |
| --- | --- |
| Frontmatter only | A vault written in Obsidian is mostly inline tags |
| Inline `#tag` only | Obsidian's Properties editor and Agents write the frontmatter list |
| `LibraryTagIndex` as a second index with its own walk | A second read and parse of every note, its own race handling, and every caller that invalidates `WikiLinkIndex` would have to invalidate it too |
| `PersonaFrontmatter` for the `tags` key | It drops quote information and does not treat `#` as a comment, so `- "#a"` and `- #a` look the same. It also does not split a comma string |
| Leaving `LibraryProperties` alone | It would draw `- #a` as a chip while the index finds no tag |
| Case-sensitive tags | Obsidian merges `#Inbox` and `#inbox` |
| A chip editor for the frontmatter `tags` | It would round-trip the YAML, which ADR-0028 forbids |
| A "rename tag" command | A multi-file write over files the Human did not open. Deferred |
| Counting `#PLAT-0042` as a tag | Matches Obsidian but adds a noisy tag per Task id, and the renderer already treats it as a reference |
| Tags in the explorer tree | A larger UI change than v1 needs; the sidebar already hosts Backlinks |

## Consequences

- **[ADR-0028](0028-the-library-edits-markdown-as-source-and-never-rewrites-it.md) is amended, not
  reversed.** `#tags` are still shown as written in a note; they are now also indexed. The file
  stays byte-exact.
- **`WikiLinkIndex`'s build changes.** It runs the tag reader beside the wikilink parser and
  stores the result, and the code-range helper that `WikiLinkParser` uses is made shared. Only
  code ranges are shared: the tag pass adds its own for the frontmatter, `%%` and `<!-- -->`.
- **`LibraryProperties` and `LibraryDocument` change.** The Properties block takes the tag list
  for its `tags` row, and `LibraryDocument` splits frontmatter with the shared helper, which
  accepts a `...` closer where `PersonaFrontmatter` does not.
- **Tags can be stale.** The link index is dropped by a Library write and by a roots change, and
  only the open file's disk change invalidates on focus. A tag added in Obsidian shows after
  Refresh in the panel or the next Huddle write in that root. Refresh rebuilds the whole root's
  index, so Backlinks refresh too. There is no refresh-on-expand, because the sidebar host does not
  tell a panel when it is expanded.
- **Known false positives.** `<a href="#x">` and a scheme-less `example.com/#a` count as tags.
- **The word "tag" is in the product for a Library note.** The glossary defines **Library tag** and
  keeps the rule that a Mention or a Task reference is never called a tag
  ([Huddle.AtTagging-Specifications.md](../Huddle.AtTagging-Specifications.md) D-21). The job pool
  already uses "tags" for its own clouds.
- **Tag search is a list, not full-text search.** Quick switcher and full-text search stay
  deferred.
