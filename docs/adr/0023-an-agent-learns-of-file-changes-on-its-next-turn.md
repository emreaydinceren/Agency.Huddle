---
status: accepted
date: 2026-09-22
---

# An Agent learns of file changes on its next Turn, from a saved snapshot

On each Turn, an Agent's prompt opens with the files added, changed or deleted in its
**Watched Folders** since its previous Turn, one full path per line. The Agent reads the ones
that matter with the Adapter's own tools. Nothing wakes it. The full design is
[Huddle.FileChanges-Specifications.md](../Huddle.FileChanges-Specifications.md), and this
replaces the delivery plan in [roadmap item 11](../engineering/roadmap.md#11-telling-an-agent-which-watched-files-changed--delivered-code-2026-09-23).

## The problem

An Agent keeps working files, such as a Keeper's note per client. When the Human edits one in
an editor, or another Agent revises a shared one, the Agent has no reason to look again. It can
re-read any file on any Turn, but it does not know that it should.

## Why roadmap item 11's plan could not be built

Item 11 proposed a `FileSystemWatcher` that posts a notification **Message** in the Agent's Room
with the Human, where the Reply Gate always passes. Every possible sender of that Message is
ruled out:

- **The Human.** A Human Message resets the Room's Budget, so every file save would switch off
  the cap on agent-to-agent conversation. Item 11 already said so.
- **A new `system` sender.** `users.kind` is `CHECK (kind IN ('human', 'agent'))`, and changing
  that needs a fresh database. Item 11 already said so too.
- **The Agent itself.** It is the only other Member of that Room, and `AgentGateway` never
  delivers a Message to its own sender. The Message would be posted and no Turn would follow.

Each surviving variant also spent a billed Turn per save.

## The decision

**The change list rides on the Agent's next Turn**, built inside `PersonaRunner` like Catch-up,
ahead of it in the prompt. Nothing is posted, nothing crosses the pipe, and no Turn is spent that
would not have been.

**Changes come from comparing snapshots, not from listening.** For each Watched Folder, Huddle
keeps every file's relative path, size and modified time. At Turn start it scans the folder and
compares.

**A baseline is kept per Room, while the files belong to the Agent.** The main use case: Nova
keeps `memory.md` in its own Work Dir. It adds to it in its Room with Alex, and on its next Turn
in its Room with Kelly it should be told `changed …\memory.md`. So what Nova was last *shown* is
kept per Room. An edit is listed in every Room except the one it was made in.

**An Agent's own edits are recognised from its own tool calls.** At Turn end, that Room's
baseline becomes the **start** scan, plus the current state of only the paths this Agent's
`Edit`, `Delete` and `Move` tool calls touched during the Turn. A Teammate's write during the same
Turn is not taken in, and is listed next time. A change made through `Bash` isn't attributed, and
comes back as one extra line: never a missed change.

**The snapshot is saved as JSON, one file per Agent,** at `{DataDir}/file-state/<Name>.json`,
holding every Room's baseline and the folders the Agent subscribed to itself. A file edited while
Huddle is closed is reported on the next Turn after it starts.

**Deliberate memory is part of the same design.** An Agent keeps what it chooses to remember as
one Markdown file per fact in `{WorkDir}/memory/`, written with its own tools, with the fact on
the first line. Huddle builds an index from those first lines into every new session's system
prompt, so a restart or a new Room's session starts knowing them. After that, File Changes carries
each edit to the Agent's other Rooms. An edit made by another copy of the same Agent is marked
*by you, in Room 'X'*, from the same tool-call record. The Prompt also tells the Agent truthfully
what it does and does not remember. A live test had an Agent deny carrying context between Rooms
while doing so. This depends on Persona sessions no longer using the Human's Claude Code
configuration, whose auto-memory would otherwise be a second, invisible memory.

**This holds under either session model.** Today one session per Persona spans every Room, so
Nova's context already holds its own edit from the Alex Room, and the list confirms what it may
half-remember. If sessions ever become one per (Persona, Room), the list becomes the only way one
instance learns what another wrote. Nothing in this design changes.

**A Watched Folder is** the Agent's own Work Dir, always; each entry in the Persona's `watches`
frontmatter; and each folder the Agent passed to `watch_folder`. An entry is a Teammate's Name,
meaning their Work Dir, or a folder inside `DataDir`.

**Each line is a kind and a full path:** `added`, `changed` or `deleted`. No contents, no diff and
no Huddle tool to read the file. The Adapter's `Read`, `Edit` and `Bash` already do that better
than anything Huddle would rebuild.

## Why a snapshot and not a watcher

A watcher loses everything that happens while Huddle is closed. It drops events when its buffer
overflows, needs a debounce so that five writes become one line, and needs a grace period so that
an Agent's last write is not reported as someone else's. A comparison at two known moments has
none of those problems. Its cost is one folder walk per Turn start and end, which pruned ignore
folders and a file cap keep small.

## Rejected

| Alternative | Why not |
| --- | --- |
| A notification Message (item 11) | No valid sender, and a billed Turn per save |
| A new Envelope that wakes the Agent | A `ProtocolVersion` bump for a wake-up nobody asked for; waking should cost a decision, not a save |
| A table in `team.db` | The state is per Agent and rewritten whole each Turn. That is a file's shape, and it needs no migration |
| One shared JSON file | Every Agent's Turn would rewrite everyone's state |
| The state file inside the Work Dir | The Agent would see it, could edit it, and it would appear in its own list |
| Diffs or file contents in the prompt | Tokens on every Turn for files the Agent may not need |
| Forgetting `watch_folder` on restart, like `follow_room` | The list would silently stop, and its snapshot would go stale |
| One baseline per Agent, and never list its own edits | Hides the main use case: Nova's edit in one Room is exactly what its other Rooms need to hear |
| Rescan at Turn end to take in the Agent's own edits | Also takes in any Teammate's write made during the Turn, which would then never be listed |
| A `remember` App Tool, or Claude Code's auto-memory, as the memory | The tool duplicates the Agent's own `Write` and hides memory from the Human. Auto-memory sits in the Human's profile, outside `DataDir`, where Huddle cannot see or move it |
| One `memory.md` per Agent | Every copy of the Agent would rewrite one file; one fact per file never collides |

## Consequences

- An Agent reacts to a change only when something else wakes it. That is right for a Keeper's
  notes. It is too slow for anything that must react the moment a file is saved, and that stays
  out of scope.
- Own-edit recognition depends on the Adapter reporting file writes as `Edit`, `Delete` or `Move`
  tool calls with the path in their raw input. That must be checked against `claude-agent-acp`
  before it is relied on. If it fails, own edits are listed back in their own Room, and nothing
  is lost.
- The state file grows with the number of Rooms an Agent is in, holding one snapshot of each
  Watched Folder per Room. A deleted Room's baseline is pruned at the Agent's next commit.
- An Adapter Profile gains `ReadsFiles`. `agency-acp` has no file tools, so it gets no list and
  no `watch_folder`.
- `PersonaRenameCascade` gains one more per-Persona store, as the roadmap's ordering note
  predicted every such store would.
- Roadmap item 11's "2 before 11" dependency no longer applies. Nothing here spends a Turn, so
  the Budget has nothing to cap.
