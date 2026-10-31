---
status: accepted
date: 2026-10-13
---

# An upload is saved to the Library and sent as its path

The Human can attach a file to a Message: pick it, paste it or drop it on the composer. The
file is saved at once into the Room's upload folder in the Library, and when the Message is
sent, its path is added to the Message text. Everything after that is the prompt-block
pipeline that already exists. The full design is
[Huddle.ChatUploads-Specifications.md](../Huddle.ChatUploads-Specifications.md).

This amends [ADR-0027](0027-the-library-sees-only-configured-roots.md), which names three kinds
of Library Root: **Uploads** is a fourth, built in like Teams and Agents.

## The problem

The Human cannot give an Agent a file from the chat. They must save it into a Library folder
by hand and type its path. Roadmap item 23 B and PromptBlocks D-12 left three things open:
which route, where a Room's files live and when they are cleaned, and what the Agent receives.

## The decision

**Save, then name it** (PromptBlocks D-12). The composer uploads the file into the Room's
folder and holds it as a chip. On send, each file's absolute path is appended to the Message
text as its own whole-path code span. The Message stays plain text: no attachment field on
`ChatMessage`, no new Transcript line format, no change to the wire.

**The Room's Scope picks the folder** (ADR-0052):

| Scope | Folder |
| --- | --- |
| Root | `{DataDir}/Uploads/<roomId>/`, under a new built-in Library Root **Uploads** |
| Team | `{DataDir}/Teams/<Team>/Uploads/<roomId>/`, in the Team's Library |

**What reaches the Agent is decided by the existing planner, unchanged.** An image becomes an
image block when the Adapter advertised images; text is inlined for an Adapter that cannot
read files; anything else is a path line the Agent opens with its own tools. ACP gives no
better route: `claude-agent-acp` drops an embedded blob resource and turns a `resource_link`
into the same path mention, and Huddle advertises no client file-system methods.

**A file is identified by its bytes**, never by its extension or the browser's content type.
SVG and executables are refused. Everything else is accepted up to a size cap enforced while
the bytes stream in.

**The files belong to the Room.** Deleting the Room sends its upload folder to the Recycle
Bin; archiving keeps it.

**The chat is the notification.** The Message carrying the path is how members learn of an
upload. File Changes never reports an upload folder: a Team's `Uploads` is pruned from scans,
and the Uploads root cannot be declared a Watched Folder.

## Why not an attachment field on the Message

It needs a new `ChatMessage` field, a Transcript line format (ADR-0002), a byte store, a wire
change for pipe Agents and its own rendering, and it would still end in a path for the
Agent, because no ACP route carries arbitrary bytes. Saving to the Library reuses the
resolver, the path-safety rules, the explorer, the chat's Library links and the prompt-block
planner, all of them built and tested.

## Why the Library and not a hidden store

The Human can see, open, move and reuse an uploaded file like any other document, and an
Agent's file tools reach it at a real path. A hidden store would need its own viewer, its own
clean-up and its own route to the Agent.

## Rejected

| Alternative | Why not |
| --- | --- |
| `ChatMessage.Attachments` and a byte store | See above: a new format, store and renderer, and the Agent still gets a path |
| Embedded blob or `resource_link` blocks | `claude-agent-acp` drops the first and reduces the second to a path mention |
| Inline the bytes as base64 text | Breaks the prompt for any file worth attaching, and is no help for binaries |
| Insert the path into the textarea at once | The Human sees a long path they did not type, and removing an attachment means editing text |
| Hidden `_uploads` folders | Chosen against by the Human in ADR-0052: uploads are visible in the Library |
| Trust the extension or `IBrowserFile.ContentType` | Both are supplied by the client (rules.md) |
| A folder per Room named after the Room | Names change on rename; the id never does |
| Delete a Room's files for good with the Room | The Library never hard-deletes; the Recycle Bin keeps a mistake recoverable |

## Consequences

- `LibraryRootKind` gains `Uploads`, with the built-in id `uploads`. Every switch on the kind is
  reviewed, and ADR-0027's table gains a row.
- `LibraryFileService` gains its first byte writer: create-new into a folder, stream through a
  temp file, cap the size, pick a free name.
- New keys `Team:Library:MaxUploadBytes` and `Team:Library:MaxUploadsPerMessage`.
- Upload folders are silent in File Changes. Otherwise every member would be told of every
  upload in every Room, uploads would crowd the 50-line list, and a Team folder past 5,000
  files would stop being checked at all. The cost, accepted by the Human: a later edit or
  delete of an uploaded file is not announced either.
- The watch refusal is its own check in `WatchedFolderResolver`, not a `ReservedFolders` entry,
  because `LibraryPathResolver` shares that list and would hide the Uploads root.
- A Message names a file the Human may later move or delete; the link then shows as missing,
  as any Library link does.
- An Agent whose Adapter cannot read files still gets nothing usable for a PDF or other binary.
  This is the existing PromptBlocks limit, recorded in `known-limits.md`.
