---
status: accepted
date: 2026-10-12
---

# Search keeps no index of its own

> **Records decision D-21 of the [Global Search specification](../Huddle.GlobalSearch-Specifications.md)**
> (approved as OP-12). The product owner's brief was *"nothing fancy, no index, just grep style"*.
> The Top Bar the search box lives in is already recorded in the addendum to
> [ADR-0046](0046-a-group-mounts-its-own-page-and-the-url-names-the-focused-one.md).

## The defect

Global Search finds Teams, Teammates, Library entries, Tasks and Rooms from one box. Doing that fast
invites an index: the names of every Library entry, the text of every file, the lines of every
Transcript, built once and queried many times. An index has a price the product does not need to pay.
It must be **built** (a first search that waits, or a background job that competes for the disk), it
must be **invalidated** (every file a Human or an agent edits, every Room that gains a Message, every
Persona that is rewritten), and it must be **kept in step** with the thing it copies, or search
answers from a state that is gone. Each of those is a place for a stale hit, and a stale hit is worse
than a slow one.

## The decision

**Search keeps no index of its own. Every search re-reads what it needs, from memory or from disk, and
keeps nothing between searches.**

What is rejected is any structure that holds names or text for the purpose of search: an inverted
index, a name table, a cache of file contents, a Transcript digest, and the code that builds and
invalidates them. The sources that are already in memory (Teams, Teammates, Tasks) are read where they
are. The Library is walked from disk on every search. A Room's Transcript is read whole.

### Why re-reading is acceptable

At Huddle's sizes it costs little (D-21). A Library Root holds hundreds to a few thousand entries, a
Transcript is one JSONL file per Room, and a search starts only after three typed characters and a
200 ms pause. A walk that is a little slow on a cold disk is paid once per pause, not once per
keystroke, and the groups that are cheap appear first (D-17).

### What bounds it

Re-reading is cheap only while it is bounded, so each part has a limit:

| Bound | Where | What it caps |
| --- | --- | --- |
| `Team:Search:MaxVisitedEntries` | per Library Root walk | How many entries one root's walk visits before it stops and the group says `Results may be incomplete.` |
| `Team:Search:MaxContentFileBytes` | per Library file | Above this length a file is matched by **name only**, never read |
| The 200 ms debounce | the box | A search starts when typing pauses, not on each keystroke |
| Cancellation per keystroke | the orchestrator | A new query cancels the one in flight, so a stale walk stops at its next token check |

The 3-character minimum and the result caps (5 per group in All, 50 under a single filter) are
constants and bound the work further (D-24).

### Not to be confused with `WikiLinkIndex`

The Library already has one index: `WikiLinkIndex`, the per-root index of wikilinks, built lazily,
cached per root and invalidated when a root changes. It holds **links, not text**, it refuses to
build above `Library:MaxIndexedFiles`, and Global Search does not use it. This decision does not
touch it, and it is not an exception to this one: it serves backlinks, not search.

### Assumed: the visit budget has its own key

The visit budget is a **new** key, `Team:Search:MaxVisitedEntries` (default `5000`, the present value
of `MaxIndexedFiles`), rather than a second use of `Library:MaxIndexedFiles`, which already means two
things (the wikilink index cap and the Library explorer's `FindAsync` budget). **This is assumed.**
The owner has not answered OP-2, and the separate key is the recommended option, applied so work can
start. If the owner chooses to reuse `MaxIndexedFiles`, only the key in the Library provider and the
configuration table change; the decision above does not.

## What does not change

- `FindAsync` and the Library explorer's own search box are unchanged and keep `Library:MaxIndexedFiles`.
- The Library's hiding rules apply to search, because the walk is the Library's one walk (spec §8.3).
- `WikiLinkIndex` keeps its three jobs: backlinks, link resolution and its own cap.

## Consequences

- There is no index to build, invalidate or keep in step, so no hit can be stale; a file saved a
  second ago is found by the next search.
- A search costs disk reads every time. The Library walk is the expensive part; the visit budget and
  the content size cap are what keep it bounded, and a root cut short shows `Results may be
  incomplete.`
- **Every Room's Transcript is read on every search** (OP-18, accepted for V1). `ReadAllAsync` reads
  the whole file under the Room's lock, so a very large Transcript is read in full each time. The
  timing check in GS-M3 measures this; if it fails, the answer is a new decision, not a quiet index.
- If a size is ever reached where re-reading is too slow, an index is added by a later ADR that
  supersedes this one, with its invalidation designed, not grown in place.
