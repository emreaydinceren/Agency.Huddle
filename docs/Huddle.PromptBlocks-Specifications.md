# Huddle.PromptBlocks — Design Specification

**Date:** 2026-10-01 · **Status:** Built 2026-10-01 (A, phases 0 to 6; phase 7's live checks answered
in Appendix C) · **Decision record:**
[ADR-0036](adr/0036-a-prompt-block-is-sent-only-when-the-adapter-advertised-it.md) · **Vocabulary:** [language.md](agencyteam/language.md)
(term **Prompt block**)

This is the design for **Prompt blocks**: parts of a Turn's prompt that are not text. Today a Turn
sends the Adapter one text block, and a Library file a Message names reaches an Agent as a path
line, or as pasted text when the Adapter has no file tools. This design lets the same Turn carry,
beside that text, the image the Human pointed at and, for an Adapter with no file tools, a
document's text as a typed resource, **but only where the Adapter has said it can take one**.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Read §5 for the shape, §6 for the parts, §14 for the decisions and the alternatives each
beat, and Appendix A for the ordered, test-first task list.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](agencyteam/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file.
> [traps.md](agencyteam/traps.md) is binding before touching `Huddle.Acp`, and this spec does.
> Nothing here overrides any of them.

> [!NOTE]
> **Scope.** V1 is **A**: a Library file the Human's own Message names. It is sent as a Prompt block
> only for that Turn. **B**, the Human attaching or pasting a file in the composer, is
> [roadmap item 23](agencyteam/roadmap.md) and not built here; §3 and §14.12 say how B would reuse
> this work. Phase 5 (a document's text as a resource) ships separately from Phases 1 to 4 and can be
> dropped without touching them.

> [!NOTE]
> **Ownership.** `src/Huddle.Acp` belongs to the ACP effort. This spec adds three small records and
> one interface member there, extends `AgentHostInfo` with one trailing optional member, and changes
> how `DotAcpAgentSession.PromptAsync` builds its request (§6.1, §6.2). **Announce it first.** The
> shared `FakeAcpAgent` is **not** edited: its `OnInitialize` hook and `PromptContext.Params` already
> let a test advertise a capability and read the blocks that arrived, so the two-assembly rule in
> `CLAUDE.md` is not triggered. The two `FakeAgentSession` classes (one in `tests/Huddle.Acp.Tests/Fakes`,
> one in `tests/Huddle.Tests/Acp/Fakes`) and `FakePersonaHost` are edited, because they implement the
> changed interfaces.

> [!NOTE]
> **Sequencing.** Written against `main` at `28ee9f8`, after Work Modes, Adapter commands, Turn detail
> and Questions. Work Modes added `ModeOptions` and `CurrentModeId` to `IAgentSession` and an
> optional trailing `mode` to `AgentSessionOptions`; Adapter commands added a `Command` work item
> and a guard on ordinary prompts. This design touches the same interface and the same prompt
> assembly, and §6.6 says exactly where it sits relative to the command guard. Whichever of this and
> any later change to `IAgentSession` lands second rebases onto the first; the changes are additive.

**Source of the Adapter facts.** Everything marked *(source)* was read on 2026-10-01 from the
installed `claude-agent-acp` 0.75.1 (`tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/
dist/acp-agent.js`) and from the `dotacp.protocol` 2026.7.19 assembly, which was loaded and made to
serialise the three block types. **No prompt with a block was sent to a live Adapter.** Appendix B
holds the evidence, and Appendix C lists what is still open. Nothing in V1 rests on a shape that was
not read in source, except where a task says so.

---

## 1. Goal

Let an Agent see the image a Human refers to, in the Turn that refers to it, without a tool call and
without Huddle guessing what an Adapter can do.

Concretely:

1. **A named image is shown.** The Human writes `@Nova what is wrong with
   E:\Huddle\Teams\Design\checkout.png`. Nova's Adapter receives the sentence **and** the image,
   as an ACP `image` block, in the same prompt.
2. **The Adapter decides what is sent.** A block goes only when the Adapter advertised
   `promptCapabilities.image` (or `embeddedContext`) at `initialize`. An Adapter that advertised
   nothing receives exactly the prompt it receives today.
3. **Every guard falls back to today.** An image that is too large, too many, not really an image,
   or from the wrong Message becomes the path line it already is. A Prompt block problem never fails
   a Turn that would have succeeded without it.
4. **The Agent is told what it was given.** Each Library document line says whether the file is
   included with the message, so the model neither opens a file it already holds nor wonders whether
   it saw one.
5. **Nothing new crosses the pipe, the Transcript or the wire.** The Message text is unchanged, the
   Transcript is unchanged, and `ProtocolVersion` does not move.

**Why this matters.** The Library exists so a Human and a Teammate can share files, and chat links
to them ([ADR-0027](adr/0027-the-library-sees-only-configured-roots.md)). Images are the one kind a
model cannot be handed by a path alone: an Adapter with file tools can open one only if the model
chooses to, at the cost of a tool round trip, and an Adapter without them cannot at all. The ACP
fields for this exist, the adapter we run advertises them *(source)*, and the library `dotacp`
already types them, yet `DotAcpAgentSession.PromptAsync` builds exactly one `TextContent`.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| P1 | The Human writes `@Nova what is wrong with E:\Huddle\Teams\Design\checkout.png`; the Adapter advertises `image` | Nova's prompt is the text plus one `image` block. The path line reads `…: included with this message` |
| P2 | The same, on an Adapter that does not advertise `image` and has file tools | The prompt is today's, byte for byte: text with the path line |
| P3 | The same, on an Adapter that does not advertise `image` and has **no** file tools (`ReadsFiles: false`) | The path line is replaced by `…: an image you cannot see`, so the model does not try to read it |
| P4 | A Message names three PNGs | Three `image` blocks, in the order first written |
| P5 | A Message names six PNGs, with the default of four per Turn | The first four are blocks. The other two are today's path lines. One Information line is logged |
| P6 | One PNG is 9 MiB, with the default cap of 3 MiB | Not sent, not read past the cap. Path line as today |
| P7 | A PNG is 9,000 × 9,000 pixels and 200 KiB | Not sent: the edge cap is 8,000. Path line as today |
| P8 | The Human names `E:\…\mock.svg` | Path line only. An SVG is never an image block (§14.5) |
| P9 | The Human names `E:\…\report.pdf` | Path line only. PDF is V2 (§3) |
| P10 | A file named `shot.png` holds HTML | The Library already classifies it `Other` because the magic bytes disagree. Path line only |
| P11 | `shot.png` was named in an **earlier** Message that now arrives as catch-up | Path line only. Only the Message that started this Turn can put an image in the prompt (D-2) |
| P12 | The same path is written twice in one Message | One block (the collector already dedupes by resolved path) |
| P13 | The image is deleted between the Message and the Turn | The collector skips it, as it skips any unreadable document. No block, no line |
| P14 | `@Nova /compact` | A Command Turn never collects Library documents, so it carries no block (§6.6) |
| P15 | A Greeting | Never collects Library documents, so no block |
| P16 | An Agent posts a Message naming an image to another Agent | The Message is ordinary for the receiver, and the same rules and caps apply (§12, E-12) |
| P17 | The Library is disabled | No collector is passed to the Turn, so nothing changes |
| P18 | The Adapter Profile sets `PromptBlocks: false` | The prompt is today's, even though the Adapter advertised `image` |
| P19 | `ReadsFiles: false`, the Adapter advertises `embeddedContext`, and the Message names `notes.md` (6 KB) | The text goes as one embedded `resource` block with a `file:///` URI, in place of the fenced inline text (Phase 5) |
| P20 | The same with a 40 KB file | Cut at `MaxInlineBytes` (16 KiB) as today; the existing cut note follows the line |
| P21 | `ReadsFiles: false` and `embeddedContext` not advertised | Today's fenced inline text |
| P22 | The Human clicks Stop while the image is being read | The read is cancelled by the Turn's token. Nothing is sent |
| P23 | The Turn is the first after a resume | Unchanged. The Transcript catch-up lists earlier mentions as text, and only this Message's images are blocks |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **B: the Human attaches or pastes a file in the composer** | New composer UI, a store for the bytes, a field on `ChatMessage` and a Transcript line format. [Roadmap item 23](agencyteam/roadmap.md). The cheapest route reuses this design (§14.12) |
| **PDF and other blob resources** | `claude-agent-acp` **ignores** a blob `resource` *(source, Appendix B.2)*, so a PDF would be dropped without an error. V2, behind a live check (V-6) |
| **SVG** | XML that can carry script; most vision models do not accept it as an image; the Library never serves one as an image either. [rules.md](agencyteam/rules.md) refuses it for avatars on the same argument |
| **Audio** | No source for it, and the adapter ignores it *(source)* |
| **`resource_link` blocks** | Baseline for every Adapter, but `claude-agent-acp` reduces one to a link of its URI *(source)*, so it carries nothing the path line does not (D-4) |
| **Resizing or re-encoding an image** | Needs an image library: a dependency, a licence, an attack surface. A header read is enough to refuse (D-9) |
| **An image in a catch-up or Transcript Message** | Re-sends old pixels on every Turn that mentions them (D-2) |
| **An `image` block with only a `uri`** | The adapter drops one unless it begins with `http` *(source)*, and Huddle's files are local (D-3) |
| **A Turn detail chip for what was sent** | A wire addition and UI work. V2, together with B (D-14) |
| **A per-Persona switch** | The switch belongs to an Adapter, which is what advertises and honours a capability (D-6) |
| **Special accounting for image tokens** | They arrive in `usage_update` like any other and count against the Budget unchanged (§11) |
| **A `ProtocolVersion` bump or new Envelope** | The runner is in-process and the Message does not change |
| **Migrating to ACP v2's `capabilities.session.prompt`** | A draft. §14.16 says where the change would land |
| **Retrying a rejected prompt without its blocks** | Cannot tell why an Adapter refused. OQ-2 |

---

## 4. Design principles

1. **Advertised, not assumed.** A block is sent only when the Adapter said it takes one. In ACP an
   omitted capability means unsupported, so an Adapter that says nothing receives nothing.
2. **Never worse than today.** Every guard resolves to the path line a Turn already carries. The
   cost of any Prompt block problem is one image the model did not see, never a failed Turn.
3. **The text block is first, and it is always there.** An Adapter reads a command only from the
   start of a prompt ([Commands design](Huddle.Commands-Specifications.md), Appendix B.2). Blocks
   follow the text and never precede it.
4. **Only what the Human's Message names, only this Turn.** A block comes from the Message that
   started the Turn, never from catch-up, the Transcript or another Agent's earlier post.
5. **Bytes come through the Library's resolver and nowhere else.** The sandbox is the Library Roots
   ([ADR-0027](adr/0027-the-library-sees-only-configured-roots.md)). A file is an image because its
   **magic bytes** say so, never because of its extension or a client-supplied type.
6. **Code decides; no prompt asks a model to behave.** Which files become blocks, in which order,
   within which caps, is C#. The words around them are configuration
   ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)); the choice is not.
7. **The prompt describes what was sent.** A document line says whether the file is included. Silence
   would leave the model to guess, and a guess costs either a redundant tool call or a missed file.
8. **Bound everything.** Count, bytes per image, bytes per Turn and pixels per edge are options with
   defaults, because an image is the first input of this app that can be megabytes.

---

## 5. Architecture overview

```text
  Human Message  "@Nova what is wrong with E:\Huddle\Teams\Design\checkout.png"
        |
        v
  RoomSession consumer, at Turn start (existing; I/O happens before the idle watchdog is armed)
        |
        |-- File Changes collect                                   (existing)
        |-- Transcript catch-up read                               (existing)
        |
        |-- LibraryDocumentCollector.CollectAsync(texts, delivery, ct)
        |       |  1. find candidate paths in the texts            (existing)
        |       |  2. resolve through LibraryPathResolver          (existing)
        |       |  3. read kind, size, and text when inlining      (existing)
        |       |  4. which of them are eligible for a block?  -->  only paths written in texts[0]   [NEW]
        |       v
        |   PromptBlockPlanner.Plan(items, delivery)   -- pure, no I/O                               [NEW]
        |       |  decides per document: Path | Block | Unavailable, within the caps
        |       v
        |   LibraryFileService.ReadImageAsync(path, limits, ct)  -- bytes + mime + size or refusal   [NEW]
        |
        v
  WorkItem.LibraryDocuments  (items now carry an optional AgentPromptBlock)
        |
        |-- RoomSession.BuildPrompt(item)        -> string   (document lines now say "included")      [CHANGED]
        |-- RoomSession.BuildPromptBlocks(item)  -> blocks   (collected from the items)                [NEW]
        v
  IAgentSession.PromptAsync(new AgentPrompt(text, blocks), ct)                                         [NEW overload]
        |
        v
  DotAcpAgentSession   ContentBlock[] { TextContent, ImageContent{ Data = base64, MimeType }, ... }    [CHANGED]
        |  stdio JSON-RPC
        v
  Adapter (claude-agent-acp)   promptToClaude: [ text, image(base64), ..., <context> for resources ]

  Capability path, once per host:
  initialize --> response.AgentCapabilities.PromptCapabilities
             --> AgentHostInfo.PromptCapabilities                              (Huddle.Acp)             [NEW]
             --> IPersonaHost.PromptCapabilities  (beside CanResume)           (Huddle.App)             [NEW]
             --> RoomSession --> `delivery` above, combined with ReadsFiles and AdapterProfile.PromptBlocks
```

Three things to hold on to:

- **Planning is pure, reading is at the edge.** The planner takes items, capabilities and limits and
  returns a decision per document; only `ReadImageAsync` touches the disk. The planner is a table
  test, and the reader a filesystem test.
- **The path line is the floor.** Every branch that is not "send a block" produces the line the
  collector produces today.
- **The seam carries a value, not a string.** `AgentPrompt` is what crosses
  `IAgentSession.PromptAsync`; the DotAcp layer owns base64, URIs and the wire types.

---

## 6. System components

### 6.1 Capability discovery (`Huddle.Acp`; announce first)

**Purpose.** Learn, once per host, what prompt content the Adapter takes.

**Responsibilities.**

- Read `InitializeResponse.AgentCapabilities.PromptCapabilities` (`Image`, `Audio`,
  `EmbeddedContext`) in `DotAcpAgentHost.StartAsync`, next to where `LoadSession` and
  `SessionCapabilities.Resume` are read today.
- Expose it as `AgentHostInfo.PromptCapabilities`, a trailing optional member, so every existing
  positional call site compiles unchanged.
- Expose it to `Huddle.App` as `IPersonaHost.PromptCapabilities`, mirroring `CanResume`
  (`DotAcpPersonaHost.CanResume => this.inner.Info.SupportsResumeSession`).

**Inputs / outputs.**

| Type | Shape |
| --- | --- |
| `AgentPromptCapabilities` | `sealed record AgentPromptCapabilities(bool Image, bool EmbeddedContext)`, with a static `None`. `Audio` is read by nobody, so it is not carried (V2 if it ever matters) |
| `AgentHostInfo` | gains `AgentPromptCapabilities PromptCapabilities = null` as the last positional parameter; a `null` member means `None` |
| `IPersonaHost` | gains `AgentPromptCapabilities PromptCapabilities { get; }` |

**Internal flow.** `response.AgentCapabilities?.PromptCapabilities` is `null` when the Adapter said
nothing, and the mapping is `None`. A `PromptCapabilities` object with `Image: false` maps to
`Image: false`; ACP says the same thing both ways.

**Implementation notes.**

- Use the **stable** `dotacp.protocol.PromptCapabilities`, the type the existing
  `InitializeResponse.AgentCapabilities` already exposes, and not `dotacp.protocol.unstable`.
- `RoomSession` already holds an `IPersonaHost?`. A `null` host (every pre-resume caller) means `None`.
- `AgentHostInfo` is available only once the host has started (`DotAcpAgentHost.Info` throws before
  that), and a Room Session opens its session, which needs a started host, before it collects
  documents. The read is safe at that point; a test pins the order.

**Constraints.** Read at `initialize` only. An Adapter that changes its mind mid-session has no
protocol notification for it.

**V1 vs V2.** V1 reads `Image` and `EmbeddedContext`. V2 reads `Audio` only if something sends audio.
ACP v2 reshapes the field to `capabilities.session.prompt` with per-type objects; only this mapping
changes (§14.16).

### 6.2 The prompt value and the seam (`Huddle.Acp`; announce first)

**Purpose.** Let one Turn's prompt carry more than a string, without the abstraction learning
base64 or file URIs.

**Responsibilities.**

- Define what a prompt is: the text and an ordered list of Prompt blocks.
- Change `IAgentSession` so a session can be given one.
- Turn that value into ACP `ContentBlock`s in the DotAcp layer, and nowhere else.

**Inputs / outputs.**

| Type | Shape |
| --- | --- |
| `AgentPrompt` | `sealed record AgentPrompt(string Text, IReadOnlyList<AgentPromptBlock>? Blocks = null)` |
| `AgentPromptBlock` | `abstract record`, with two sealed cases below |
| `AgentImageBlock` | `(string MimeType, ReadOnlyMemory<byte> Data)`. Raw bytes, never base64 |
| `AgentTextResourceBlock` | `(string Uri, string MimeType, string Text)`. Phase 5 |
| `IAgentSession` | `Task<PromptResult> PromptAsync(AgentPrompt prompt, CancellationToken ct)`; the existing `PromptAsync(string, CancellationToken)` becomes a **default interface method** that calls it with `new AgentPrompt(text)` |

**Internal flow in `DotAcpAgentSession.PromptAsync(AgentPrompt, …)`.**

1. The in-flight guard, the linked cancellation source and the rest are untouched.
2. `Prompt = [ new TextContent { Text = prompt.Text }, ..blocks ]`, built by one private mapper:
   - `AgentImageBlock` becomes `new ImageContent { Data = Convert.ToBase64String(data.Span), MimeType = … }`.
     `Uri` stays `null`.
   - `AgentTextResourceBlock` becomes `new EmbeddedResource { Resource = new TextResourceContents { Uri, MimeType, Text } }`.
3. With no blocks the array is exactly `[ TextContent ]`, so a text-only Turn is byte-identical on the
   wire to today's.

**Implementation notes.**

- The three `dotacp` types set their own `type` discriminator (`"image"`, `"text"`, `"resource"`),
  confirmed by instantiating them *(source, Appendix B.3)*. This is the trap
  `SetSessionConfigOptionRequest.Type` fell into, so a wire test still pins the literal JSON (T1.4).
- `Convert.ToBase64String` runs once per image, on the thread that already awaits the prompt.
- Both `FakeAgentSession` classes record every `AgentPrompt` (their `Prompts` lists of strings stay,
  for the many existing tests) and gain a `PromptBlocks` list.
- `Huddle.Console` calls the string overload and needs no change.

**Constraints.**

- `IAgentSession` has three implementers today: `DotAcpAgentSession` and two `FakeAgentSession`
  classes, one under `tests/Huddle.Acp.Tests/Fakes` and one under `tests/Huddle.Tests/Acp/Fakes`. Each
  is forced to implement the new method, which is the point.
- The default interface method is the one place the interface carries behaviour. The alternative,
  keeping two abstract methods, would let an implementer handle one and silently drop the other.

**V1 vs V2.** V1 has two block cases. A third (a blob resource) arrives with PDF in V2 and is an
additive subclass.

### 6.3 Reading an image (`LibraryFileService`)

**Purpose.** Return an image's bytes, safely, or say why not.

**Responsibilities.**

- Re-resolve the path against the current Library Roots, as `ReadAsync` does, so a caller's stale
  `FullPath` is never trusted.
- Confirm by magic bytes that the file is an image and learn its MIME type, with
  `LibraryFileKinds.ImageContentType(head)`, the function that already classifies Library images and
  that the Library's own file endpoint serves them by.
- Refuse a file over the byte cap **before** reading it all, and again while reading, because a file
  can grow between the `Stat` and the read.
- Learn the pixel size from the header and refuse an image over the edge cap.

**Inputs / outputs.**

```csharp
internal Task<LibraryImageResult> ReadImageAsync(LibraryPath file, int maxBytes, int maxEdgePixels, CancellationToken ct);

internal abstract record LibraryImageResult;
internal sealed record LibraryImageRead(string MimeType, byte[] Bytes, int Width, int Height) : LibraryImageResult;
internal sealed record LibraryImageRefused(LibraryImageRefusal Reason) : LibraryImageResult;
internal enum LibraryImageRefusal { NotAnImage, TooLarge, TooManyPixels, Unreadable }
```

A refusal is data, not an exception, because the caller wants to continue with the path line.

**Internal flow.**

1. `TryResolve`; a failure is `Unreadable`.
2. Open with `FileShare.ReadWrite | FileShare.Delete`, the flags `ReadAsync` uses.
3. If `stream.Length > maxBytes`, return `TooLarge` without allocating.
4. Read the head (the Library already reads one for kind detection), call `ImageContentType`; `null`
   is `NotAnImage`.
5. Read the rest with a hard bound of `maxBytes + 1` bytes; reaching the bound is `TooLarge`.
6. `ImageHeader.TryReadSize(bytes, out width, out height)` (§8.4). Failure to parse is
   `NotAnImage`; a size over `maxEdgePixels` on either side is `TooManyPixels`.
7. Return `LibraryImageRead`.

**Implementation notes.** `IOException` and `UnauthorizedAccessException` map to `Unreadable`, as
`LibraryDocumentCollector.TryBuildItemAsync` already treats them. `OperationCanceledException`
propagates.

**Constraints.** SVG is never an image here, for the reason `LibraryFilesEndpoint` gives. An image's
bytes are never held past the Turn (§7).

**V1 vs V2.** V1 reads whole images up to the cap. A V2 that resizes would live behind this method
with the same result type.

### 6.4 The planner (`PromptBlockPlanner`, pure)

**Purpose.** Decide, per Library document, how it reaches the model.

**Responsibilities.** Apply the delivery table below and the caps, in first-seen order.

**Inputs.**

```csharp
internal sealed record PromptDelivery(
    bool ReadsFiles,            // AdapterProfile.ReadsFiles
    bool Images,                // capabilities.Image && profile.PromptBlocks
    bool EmbeddedText,          // capabilities.EmbeddedContext && profile.PromptBlocks && !ReadsFiles
    int MaxImagesPerTurn, long MaxImageBytesPerTurn);
```

**Outputs.** For each document one of `Path`, `Block` or `Unavailable`, plus the block when it is
`Block`.

**The delivery table.**

| Kind | `ReadsFiles` | Condition | Result |
| --- | --- | --- | --- |
| Image | any | `Images`, in the Message's own text, and within every cap | **Block** (`AgentImageBlock`) |
| Image | `true` | otherwise | **Path** (today's line) |
| Image | `false` | otherwise | **Unavailable** (`turn.libraryImageUnavailable`) |
| Markdown, Text | `true` | any | **Path** (today's line) |
| Markdown, Text | `false` | `EmbeddedText`, text read, not too large | **Block** (`AgentTextResourceBlock`), Phase 5 |
| Markdown, Text | `false` | otherwise | today's inline text, or the too-large line |
| Svg, Other | any | any | **Path** (today's line) |

**Internal flow.** Walk the items in first-seen order. For an image the table allows, ask the reader
for the bytes; accept it only if the running count is under `MaxImagesPerTurn` and the running bytes
plus these are within `MaxImageBytesPerTurn`. A refusal or an over-cap image does not consume the
budget, so a later, smaller image can still fit. Log one Information line when anything was
withheld, with counts and the reason, never with a path's contents.

**Implementation notes.** The planner is a static class over its inputs plus an injected
`Func<LibraryPath, CancellationToken, Task<LibraryImageResult>>` for the read, so the table is
tested without a filesystem (T3.1).

**Constraints.** Documents that are not eligible for a block (anything first written outside the
Message's own text) are always **Path**, and for a `ReadsFiles: false` image **Unavailable**.

**V1 vs V2.** V2 adds PDF as a row and a hash-based "already sent this session" check (OQ-5).

### 6.5 The document lines and their prompts (`Huddle.App/Prompts`)

**Purpose.** Tell the model what it was given.

**Responsibilities.** Two new prompt keys, in `PromptCatalog`, beside the six `turn.libraryDoc*`
keys. Both are `PromptTiming.Live`, so an edit takes effect without a restart
([ADR-0007](adr/0007-model-facing-text-is-configuration.md)).

| Key | Default | Placeholders (all required) |
| --- | --- | --- |
| `turn.libraryDocIncluded` | `- {{path}} ({{location}}, {{size}}): included with this message` | `{{path}}`, `{{location}}`, `{{size}}` |
| `turn.libraryImageUnavailable` | `- {{path}} ({{location}}, {{size}}): an image you cannot see` | `{{path}}`, `{{location}}`, `{{size}}` |

**Internal flow.** `RoomSession.AppendLibraryDocumentsBlock` renders by the item's delivery:
`Path` is `turn.libraryDoc` as today, `Block` is `turn.libraryDocIncluded` (and, for a text resource
that was cut, the existing `turn.libraryDocTruncated` line after it), `Unavailable` is
`turn.libraryImageUnavailable`. A `Block` item renders no `turn.libraryDocInline` fence, because its
text is in the block.

**Implementation notes.** Adding keys changes two checked-in artefacts: the defaults file that
`PromptDefaultsFileTests` compares byte-for-byte, and nothing else in the goldens, because a Turn
with no Library document renders none of these lines. `PromptValidator` already checks that a
`Live` prompt keeps its required placeholders.

**V1 vs V2.** V2 may want a per-kind wording ("the image is attached below") if the model confuses
the order; the key shape allows it.

### 6.6 Turn assembly (`RoomSession`)

**Purpose.** Carry the blocks from the collector to `PromptAsync`, and stay out of the command guard.

**Responsibilities.**

- Pass the delivery bundle to the collector: `new PromptDelivery(readsFiles, capabilities.Image &&
  profile.PromptBlocks, …)`, built once per session from `IPersonaHost.PromptCapabilities`, the
  profile and `LibraryOptions`.
- Add `BuildPromptBlocks(WorkItem item)`, returning the blocks of `item.LibraryDocuments` in order,
  or an empty list.
- Call `activeSession.PromptAsync(new AgentPrompt(prompt, blocks), turnCancellation.Token)`.

**Internal flow, and where it sits.**

1. The collector call is the one that exists, guarded by `item.Kind == WorkItemKind.Message`. A
   Command or a Greeting never reaches it, so **neither can carry a block** (P14, P15). No new
   branch is needed, and a test pins it.
2. `BuildPrompt` is unchanged in shape. Its command guard (`CommandGuardMarker`, which prefixes a
   framed prompt that begins with `/`) concerns the **text** only. Blocks are separate array
   members that follow the text, so the guard and the blocks cannot interact.
3. The text block stays first (principle 3, and Appendix B.2: `promptToClaude` inspects only text
   blocks for commands).

**Constraints.** `WorkItem` is not changed. The blocks live on the existing
`LibraryDocumentsReport`, whose items gain one optional trailing member (§7), so a `WorkItem` is
exactly as large as before.

**V1 vs V2.** Unchanged.

### 6.7 Configuration

| Key | Default | Meaning |
| --- | --- | --- |
| `Team:Library:MaxImageBytes` | `3145728` (3 MiB) | The most bytes of one image sent as a block. 3 MiB is 4 MiB once base64-encoded, under the 5 MB Anthropic documents as its per-image limit (V-1) |
| `Team:Library:MaxImagesPerTurn` | `4` | The most image blocks in one prompt; the rest are path lines |
| `Team:Library:MaxImageBytesPerTurn` | `8388608` (8 MiB) | The most raw image bytes in one prompt. About 11 MB on the wire |
| `Team:Library:MaxImageEdgePixels` | `8000` | The longest side of an image sent as a block. Anthropic documents 8,000 as the limit (V-1) |
| `Team:Acp:Adapters:*:PromptBlocks` | `true` | Whether to send blocks to an Adapter that advertises it can take them. Set `false` for an Adapter that advertises a capability it does not honour. The synthesised legacy profile is `true` |

All five are read at startup. `LibraryOptions` validates nothing today and V1 adds no validation:
the planner treats a limit of zero or less as "send none", which is the safe direction and falls
back to the path line. None is a collection, so the "no initialiser" rule does not apply. `PromptBlocks` joins `ReadsFiles` and `SessionPerRoom` as a trailing optional member of
`AdapterProfile` and `AdapterProfileOptions`.

### 6.8 What each side does not change

| Part | Unchanged |
| --- | --- |
| `ChatMessage`, the Transcript JSONL, `team.db`, `room-sessions` | Everything. No field, no table, no file |
| The pipe protocol and `ProtocolVersion` | Everything. Nothing new crosses it |
| The Reply Gate, Budget, Stop, the idle watchdog | Everything. A block is part of a prompt, not a new kind of Turn |
| `FakeAcpAgent` | Not edited. Its hooks already suffice |

---

## 7. Data model and storage

**No storage changes.** Nothing is persisted: not the bytes, not that a block was sent. The Transcript
keeps the Message text, which already names the file.

New in-memory types, all internal to their assemblies except the three in `Huddle.Acp.Abstractions`:

| Type | Assembly | Lifetime |
| --- | --- | --- |
| `AgentPromptCapabilities` | `Huddle.Acp` | Per host, immutable |
| `AgentPrompt`, `AgentPromptBlock`, `AgentImageBlock`, `AgentTextResourceBlock` | `Huddle.Acp` | One `PromptAsync` call |
| `PromptDelivery` | `Huddle.App` | Per Room Session, immutable |
| `LibraryImageResult` and its cases, `LibraryImageRefusal` | `Huddle.App` | One read |
| `LibraryDocumentItem.Block` | `Huddle.App` | The Turn's `WorkItem` |

`LibraryDocumentItem` gains `AgentPromptBlock? Block = null` and a `LibraryDocumentDelivery Delivery`
(an enum: `Path`, `Block`, `Unavailable`; default `Path`) as trailing optional members, so every
existing construction still compiles.

**Where the bytes live.** The collector runs when the Turn **starts** (`RoomSession`, before the
idle watchdog is armed), not when the Message is enqueued, so a queued Message holds no pixels. The
bytes exist from the read until `PromptAsync` returns. At the default caps that is at most 8 MiB raw,
11 MB as one base64 string, and a similar size again in the serialised request: a transient peak of
roughly 30 MB per running Turn, on the large object heap, with `MaxConcurrentTurns` at 1 by default.

**Differences between Adapters.**

| Adapter | `image` | `embeddedContext` | Notes |
| --- | --- | --- | --- |
| `claude-agent-acp` 0.75.1 | advertised *(source)* | advertised *(source)* | Converts `image` to an Anthropic base64 source, a text `resource` to a link plus a `<context>` block, ignores a blob `resource` and audio (Appendix B.2) |
| `agency-acp` | unknown | unknown | Reported `loadSession: false`; prompt capabilities not recorded (V-5). Until known, `PromptBlocks: false` is the safe configuration for a profile that turns out to advertise without honouring |
| `Huddle.MockAdapter`, `FakeAcpAgent` | scripted | scripted | A test advertises whatever it needs through `OnInitialize` |

---

## 8. Core algorithms and processing logic

### 8.1 Resolving the delivery bundle

```text
caps       = host?.PromptCapabilities ?? None
images     = caps.Image            AND profile.PromptBlocks
embedded   = caps.EmbeddedContext  AND profile.PromptBlocks AND NOT profile.ReadsFiles
delivery   = (profile.ReadsFiles, images, embedded, options.MaxImagesPerTurn, options.MaxImageBytesPerTurn)
```

An Adapter that reads files never gets text as a resource: its path line already lets it read the
file, and a second copy of the text in the context is a cost without a benefit.

### 8.2 Collecting and planning

1. Find candidate paths in all the texts, resolve each through the Library, dedupe by resolved path,
   and cap at `MaxReferencedDocuments`. **Unchanged.**
2. Mark each kept path `FromTrigger` when it was found in `texts[0]`, which is always the Message that
   started the Turn.
3. For every kept path, read kind, size and (when inlining) text. **Unchanged.**
4. Run the planner over the kept items in first-seen order (§6.4). For each image it admits, call
   `ReadImageAsync`; an accepted read stores the block on the item.
5. Return the report. An item with no block renders exactly as it does today.

### 8.3 Building the request

1. `BuildPrompt(item)` returns the text, with each document line rendered by delivery.
2. `BuildPromptBlocks(item)` returns `item.LibraryDocuments.Items.Where(i => i.Block is not null)
   .Select(i => i.Block)`, in order.
3. `DotAcpAgentSession` maps `[ text, ..blocks ]` to `ContentBlock[]`.

### 8.4 Reading an image's size from its header

No image library is used; only the first bytes are read, and a failure to parse is a refusal (the
file falls back to the path line).

| Format | Where the size is |
| --- | --- |
| PNG | Bytes 16 to 23 of the `IHDR` chunk: width then height, big-endian 32-bit |
| GIF | Bytes 6 to 9: logical screen width then height, little-endian 16-bit |
| WebP | `VP8 ` (lossy): the 14-bit fields after the frame tag; `VP8L` (lossless): the packed 14-bit fields after the signature byte; `VP8X` (extended): 24-bit canvas width and height, each minus one |
| JPEG | Scan the marker segments from `SOI`, skip each by its length, and read height then width (big-endian 16-bit) from the first `SOF0` to `SOF15` marker other than `DHT` (`C4`), `JPG` (`C8`) and `DAC` (`CC`) |

The parser reads at most the first 64 KiB for JPEG and gives up past it. A pathological file that
puts its `SOF` later is refused, which is the safe direction.

### 8.5 The file URI (Phase 5)

`new Uri(fullPath).AbsoluteUri`, for example `file:///E:/Huddle/Teams/Design/notes%20v2.md`. The
`Uri` class handles drive letters, spaces and non-ASCII. The MIME type is `text/markdown` for
`Markdown` and `text/plain` for `Text`.

### 8.6 Error handling

| Where | Failure | Result |
| --- | --- | --- |
| `ReadImageAsync` | Not an image, too large, too many pixels, unreadable | `LibraryImageRefused`; the planner falls back to the path line (or `Unavailable`) and logs once per Turn |
| `ReadImageAsync` | Cancellation | `OperationCanceledException`; the Turn is cancelled as any Stop |
| The collector | Any exception that is not cancellation | Already caught in `RoomSession` and logged; the Turn proceeds with **no** Library block at all, as today |
| `DotAcpAgentSession` | The Adapter answers `session/prompt` with an error | `AgentException`, mapped as today. No retry (D-11) |

---

## 9. Incremental versus full processing

Prompt blocks are **per Turn and stateless**: nothing is cached and nothing carries over.

- The Adapter's session already holds an image sent in an earlier Turn in its own context. Huddle
  does not track that, so a later Message naming the same file sends it again, as a new Message
  would. A "sent in this session" check keyed on a content hash is V2 (OQ-5).
- A resumed session is no different: the first Turn after a resume builds its prompt from that Turn's
  Message, and the Transcript catch-up lists earlier mentions as text.
- There is no full-versus-incremental distinction to make. The work done per Turn is bounded by the
  caps: at most four image reads and a header parse each.

---

## 10. Background workers and async components

**None added.** All of this runs inline in the Room Session consumer, at Turn start.

- **Where.** After the File Changes collect and the Transcript read, with the Library collector, and
  **before** the idle watchdog is armed. A slow read is therefore never counted as Adapter silence;
  this is the reasoning the collector already relies on.
- **Cancellation.** The consumer's `ct` flows into `ReadImageAsync`, which passes it to every await.
  A Stop during a read ends the Turn before any prompt is sent.
- **Concurrency.** The planner is pure and the reader holds no shared state, so two Room Sessions can
  plan at once. The only shared resource is the disk, read with `FileShare.ReadWrite | FileShare.Delete`
  so an editor holding the file is not blocked, and a save in progress is read as it is.
- **The Adapter side.** `DotAcpAgentSession` still permits one prompt in flight. Nothing about a block
  changes that.

---

## 11. Performance expectations

| Quantity | Expectation |
| --- | --- |
| Reading a 3 MiB image | Milliseconds from a local disk. Dominated by the file system, not the code |
| Header parse | Microseconds; at most 64 KiB read for JPEG |
| Base64 | +33%, one `Convert.ToBase64String` per image |
| One `session/prompt` line | Up to about 11 MB at the default caps. Whether the writer and the adapter's reader handle a line that large is V-2 |
| Memory per running Turn | A transient peak of about 30 MB, on the large object heap (§7) |
| Turn latency | Upload time to the model provider is added to time-to-first-token; seconds, not minutes |
| Tokens | Roughly width × height ÷ 750 per image, and a provider scales large images down first, so from a few hundred to about 1,600 each by Anthropic's published guidance. **Not measured here** (V-1) |
| Budget | Image tokens arrive in `usage_update` and are counted by the token Budget like any other rise |

**An image stays in the session's context.** After a Turn that sent one, each later Turn in that
session carries it as cached context. That is real, recurring spend that nothing in Huddle shows; it
goes in [Known limits](agencyteam/known-limits.md), and `@Nova /compact`
([Commands design](Huddle.Commands-Specifications.md)) is the way to shed it.

**Scaling.** `MaxConcurrentTurns` (default 1) bounds simultaneous peaks. Raising it multiplies the
§7 peak.

---

## 12. Edge cases and failure modes

| # | Case | Expected behaviour |
| --- | --- | --- |
| E-1 | `shot.png` whose bytes are not an image | The Library classifies it `Other`; **Path**. The reader would also say `NotAnImage` |
| E-2 | A PNG whose header is truncated or whose size cannot be parsed | `NotAnImage`; **Path**. Fails closed |
| E-3 | A well-formed header over a corrupt body | Passes every check here and may be refused by the provider. The Turn fails like any Adapter error, once, for that Message only (D-2 means it cannot recur). Residual risk, V-4 |
| E-4 | An animated GIF or WebP | Treated as an image. What the provider does with frames is V-1 |
| E-5 | The file changes size between the collector's stat and the read | `ReadImageAsync` is authoritative: it enforces the cap while reading |
| E-6 | A path outside every Library Root, or a link out of one | The resolver does not resolve it, so there is no item and no block. A test pins it at the collector (T3.3) |
| E-7 | The Adapter advertises `image` but `PromptBlocks` is `false` | **Path**, exactly V0 |
| E-8 | The Adapter's `initialize` has no `promptCapabilities` | `None`. Nothing is sent |
| E-9 | `RoomSession` built without a host | `None`. Nothing is sent |
| E-10 | Two queued Turns, and the image is deleted before the second runs | The second collector skips it (P13) |
| E-11 | The same image in the Message and in catch-up | One item; eligible because it is in the Message |
| E-12 | An Agent's Message names images | Same rules and caps. Reply Gate and Budget bound how often a Turn starts, and a Library file is already readable by every Agent with file tools |
| E-13 | `Team:Acp:TraceWire` is on | The wire trace logs the whole request, **including the base64**, at Trace level. Documented in Known limits; V1 adds no redaction (the listener sees a string, not a block) |
| E-14 | Stop during the read | Cancelled, no prompt sent |
| E-15 | The Adapter rejects the prompt | `AgentException`; counts toward the consecutive-failure streak like any failed Turn; no retry (D-11) |
| E-16 | The path is inside a code span or plain text | Found by the existing collector rules |
| E-17 | A path with spaces or non-ASCII, with Phase 5 | Encoded by `Uri.AbsoluteUri` (§8.5). An `image` block has no URI at all |
| E-18 | A text document whose bytes are not decodable | `content.Text` is `null`; no resource and no inline text, as today |
| E-19 | `ReadsFiles: false`, an image, `image` advertised | **Block**, which is the case the whole feature serves |
| E-20 | Shared session mode (`SessionPerRoom: false`) | No difference: capabilities are per host and the bundle per session |
| E-21 | Library disabled | No collector; no block |
| E-22 | A tiny file with an enormous declared size (a decompression bomb) | `TooManyPixels` from the header; never decoded here |
| E-23 | EXIF orientation | Ignored. The provider sees the stored orientation |
| E-24 | Text inside an image tells the model to do something | Not a new surface: the same file is readable today by any Agent with file tools, and Messages already instruct Agents. Noted for a reviewer, not mitigated |

---

## 13. End-to-end flow

The Human writes, in a Room with Nova (a Claude Teammate, `ReadsFiles: true`):

> `@Nova what is wrong with E:\Huddle\Teams\Design\checkout.png`

1. The Message is posted and delivered as any Message; the Reply Gate lets it through.
2. Nova's Room Session consumer picks the item up and opens its session. At `initialize` the Adapter
   advertised `promptCapabilities { image: true, embeddedContext: true }`, so
   `IPersonaHost.PromptCapabilities` is `(true, true)`.
3. The delivery bundle is `(ReadsFiles: true, Images: true, EmbeddedText: false, 4, 8 MiB)`.
4. `LibraryDocumentCollector` finds `checkout.png` in the Message text, resolves it under the
   *Teams* root, sees `Kind = Image`, 412 KB, and marks it `FromTrigger`.
5. The planner allows an image block; `ReadImageAsync` reads 412 KB, confirms `image/png`, parses
   1,280 × 800, and returns it. Count 1 of 4, 412 KB of 8 MiB.
6. `BuildPrompt` renders the Library block with the line
   `- E:\Huddle\Teams\Design\checkout.png (Team Design, 412 KB): included with this message`.
7. `PromptAsync(new AgentPrompt(text, [image]))` sends:

```json
{"sessionId":"…","prompt":[
  {"type":"text","text":"Library documents mentioned in these messages:\n- E:\\Huddle\\Teams\\Design\\checkout.png (Team Design, 412 KB): included with this message\n\n[Room: …] Human: @Nova what is wrong with E:\\Huddle\\…\\checkout.png"},
  {"type":"image","data":"iVBORw0KGgo…","mimeType":"image/png"}
]}
```

8. The adapter turns it into an Anthropic message `[ text, image(base64) ]` and Nova answers from the
   picture. The reply streams, posts and is stored exactly as any reply.
9. The bytes are released when `PromptAsync` returns. Nothing about the image is stored.

The same Message to a profile with `ReadsFiles: false` and `image` not advertised renders
`…: an image you cannot see` and sends the text block alone.

---

## 14. Design notes and rationale

### 14.1 D-1 — Images are blocks for every Adapter that takes them, including file-readers

*Decided 2026-10-01.* A Claude Teammate can open `checkout.png` with its `Read` tool, so a path line
would be enough, in principle. But the model has to decide to look, and the look costs a tool round
trip and a second prompt. The Human said "look at it" and the Agent should be looking. The cost, image
tokens, is paid only in the Turn that names the file. *Alternatives:* **path only** (zero change, but
the model may never look); **`resource_link` only** (the adapter reduces it to a link of the URI, so
it is the path line again).

### 14.2 D-2 — Only the Message that started the Turn can contribute a block

Catch-up and Transcript Messages are context, and a block is a cost of megabytes and tokens that
recurs for as long as the session lives. Letting a catch-up Message contribute would re-send old
pixels on every Turn that mentions them and make the prompt depend on how many Messages were missed.
*Alternative:* any Message in the prompt; rejected for exactly that dependence. It also bounds E-3 to
one Message: a poison image cannot fail a later Turn.

### 14.3 D-3 — `data`, never `uri`

`promptToClaude` uses an image's `data`, and falls back to `uri` only when it begins with `http`
*(source)*. A `file://` URI would be dropped without an error. So the block carries bytes. This is
also why the block has no `uri` at all: a half-honoured field is worse than an absent one.

### 14.4 D-4 — No `resource_link`

Every Adapter must accept it, which makes it tempting as the portable form. `claude-agent-acp`
reduces it to `formatUriAsLink(uri)` and discards the name, MIME type and size *(source)*. It adds
nothing the path line does not say, and a second line saying the same thing is noise.

### 14.5 D-5 — SVG is never an image block

An SVG is XML that can carry script, a provider's image input does not take one, and the Library
already refuses to serve one as an image. Whether to send an SVG as text is a separate question,
because the model may read the markup usefully. V2.

### 14.6 D-6 — The capability comes from `initialize`; the profile can only turn it off

The Adapter is the authority on what it takes, so the capability is read, not configured. A profile
switch, `PromptBlocks`, exists for one reason: an Adapter that advertises a capability it does not
honour must be fixable without a code change. It defaults to `true` and can only reduce what is sent.
*Alternative:* configure it per profile with no discovery; rejected, because it would be a second
source of truth that drifts from what the Adapter says.

### 14.7 D-7 — The seam carries `AgentPrompt`; the string overload stays

`PromptAsync(string, …)` is called by `Huddle.Console` and many tests. Making it a default interface
method that wraps `new AgentPrompt(text)` leaves every caller untouched and makes the new method the
only one an implementer must write. *Alternatives:* **a second abstract method** (an implementer can
handle one and drop the other); **`PromptAsync(string, IReadOnlyList<…>)`** (a parallel parameter list
that grows with each block kind); **blocks on `AgentSessionOptions`** (wrong lifetime: options are
fixed at `session/new`, a prompt is per Turn).

### 14.8 D-8 — Capabilities live on `IPersonaHost`, beside `CanResume`

`RoomSession` already asks the host `CanResume` and `Profile.Id`. A capability is a fact about the
host, so it sits there. *Alternative:* `IAgentSession.PromptCapabilities`, as `ModeOptions` does; it
would work but would make every session re-state a per-host fact, and `ModeOptions` is per session
because it depends on the model.

### 14.9 D-9 — Dimensions are read from the header, with no image library

A 9,000 × 9,000 flat-colour PNG is under 3 MiB and the provider refuses it, so the byte cap alone
would let one Message fail one Turn. An image library would read dimensions but brings a dependency
in `Directory.Packages.props`, a licence to check and a decoder to attack. A header read is a few
hundred lines, table-tested, and fails closed. *Alternative:* skip the check and accept the failed
Turn; rejected because it hands a user a Turn that fails for a reason the app could see.

### 14.10 D-10 — Fall back to the path line, never fail the Turn

Every refusal produces the line the Turn already has. The principle is simple to state and to test:
for any input, the prompt is either V0's or V0's plus blocks.

### 14.11 D-11 — No retry without blocks when the Adapter rejects a prompt

`session/prompt` failures arrive as one exception type with a message, so Huddle cannot tell an image
refusal from a quota error. A retry would spend a second Turn on the wrong guess. The failed Turn is
reported like any other and cannot recur from the same Message (D-2). Revisit if V-4 shows the
failure is common (OQ-2).

### 14.12 D-12 — B reuses this design; it is not part of it

The Human attaching a file needs a composer control, a store, a `ChatMessage` field and a pipe-side
story for non-ACP Agents. The cheapest version needs none of that: the composer saves the file
under a Library Root, in the Room's own folder, and inserts its path into the Message text. This
design then handles it unchanged, because the Message names a Library file. The costs are
Library-folder housekeeping and the decision about where a Room's folder is. That is the shape
roadmap item 23 records. *Alternative:* a first-class attachment on the Message; heavier, and it
touches the Transcript format ([ADR-0002](adr/0002-jsonl-file-per-room.md)).

### 14.13 D-13 — The limits sit in `LibraryOptions`, beside `MaxInlineBytes`

They bound what leaves the Library for a prompt, and the existing prompt-size caps are there.
*Alternative:* `AcpOptions`; rejected because the limits are about files, and `AcpOptions` is about
sessions.

### 14.14 D-14 — No Turn detail chip

Showing "sent 1 image, 412 KB" under a Draft would be good, and it is an optional field on
`ToolActivity` or a new message ([ADR-0034](adr/0034-turn-detail-rides-on-tool-activity-as-optional-fields.md)
set the precedent). V1 leaves it: the document line already tells the model, the Information log
tells the operator, and the Human wrote the path themselves. It belongs with B, where the Human
did not write a path.

### 14.15 D-15 — ADR-0036 records three things that are hard to reverse

1. A Prompt block is sent only when the Adapter advertised it, and a guard falls back to the path.
2. Only the Message that started the Turn can contribute a block.
3. The seam carries `AgentPrompt`, with the string form as a default interface method.

They are surprising without context (an adapter silently dropping a `file://` image, a blob resource
or a `resource_link`) and each beat a real alternative above. Task D0 writes it before any code.

### 14.16 D-16 — What changes under ACP v2

The v2 draft moves prompt capabilities to `capabilities.session.prompt`, makes each an object with a
`MediaType`, and keeps the `ContentBlock` types. Only `DotAcpAgentHost`'s mapping (§6.1) and the
mapper (§6.2) change; `AgentPromptCapabilities`, the planner and everything in `Huddle.App` do not.
That is the argument for keeping the wire types out of the planner.

### 14.17 V1 versus V2

| | V1 (this spec) | V2 and later |
| --- | --- | --- |
| Source of a block | A Library file named in the Message that started the Turn | B: the Human attaches a file (roadmap item 23) |
| Kinds | PNG, JPEG, GIF, WebP; text as a resource on Adapters with no file tools (Phase 5) | PDF as a blob resource (V-6), SVG as text, audio if anything sends it |
| Capability | `image`, `embeddedContext` | `audio`; ACP v2's object form |
| Sizing | Cap and refuse | Resize or re-encode, if an image library is ever justified |
| Repetition | Re-sent when named again | A content-hash "already in this session" check (OQ-5) |
| Visibility | A log line; the model is told in the prompt | A Turn detail chip |
| On rejection | The Turn fails once | A retry without blocks (OQ-2) |

### 14.18 Open questions

| # | Question | Default until answered |
| --- | --- | --- |
| OQ-1 | What are the real byte and pixel limits the provider enforces behind `claude-agent-acp`, and does 3 MiB / 8,000 px pass? | The defaults in §6.7 |
| OQ-2 | Should a rejected prompt be retried without its blocks? | No (D-11) |
| OQ-3 | Does `agency-acp` advertise `image` or `embeddedContext`, and does its model take images? | Treated as `None` when it says nothing |
| OQ-4 | Should the Human see what was sent? | No in V1 (D-14) |
| OQ-5 | Should Huddle remember a content hash per session and not resend? | No in V1 |

---

## Appendix A — Test-first task plan

Every implementation task is preceded by the test that specifies it, per
[agents/Testing.md](../agents/Testing.md) and `agents/CSharpPrinciples.md`. A test must be seen to
fail before the code it covers exists. Names follow `Method_Scenario_Expectation`. **Announce the
`Huddle.Acp` phase before starting it.**

### Phase 0: the decision record

| # | Task | Files |
| --- | --- | --- |
| D0 | Write ADR-0036 (§14.15) and mark this spec's header accepted | `docs/adr/0036-…md`, this file |

### Phase 1: capabilities and the seam (`Huddle.Acp`; announce first)

| # | Task | Files |
| --- | --- | --- |
| T1.1 | **Test.** `FakeAcpAgent.OnInitialize` advertises `promptCapabilities {image:true, embeddedContext:false}`: `Info.PromptCapabilities` is `(true, false)`. With the field absent it is `None`. With `{image:false}` it is `(false, false)` | `tests/Huddle.Acp.Tests/DotAcp/DotAcpAgentHostCapabilityTests.cs` |
| T1.2 | **Implement.** `AgentPromptCapabilities`; the trailing member on `AgentHostInfo`; the mapping in `StartAsync` | `src/Huddle.Acp/Abstractions/AgentPromptCapabilities.cs`, `AgentHostInfo.cs`, `DotAcp/DotAcpAgentHost.cs` |
| T1.3 | **Test.** A text-only `AgentPrompt` sends exactly one block, `{"type":"text","text":…}`, identical to today's request. The string overload sends the same | `tests/Huddle.Acp.Tests/DotAcp/DotAcpAgentSessionTests.cs` |
| T1.4 | **Test (wire pin).** Reading `context.Params["prompt"]` in a `FakeAcpAgent.OnPrompt` script: `[ text, image ]` arrives with `type:"image"`, `data` equal to the base64 of the bytes, `mimeType`, and no `uri` value; a text resource arrives as `type:"resource"` with `resource.uri`, `mimeType` and `text` | same |
| T1.5 | **Implement.** `AgentPrompt`, `AgentPromptBlock`, `AgentImageBlock`, `AgentTextResourceBlock`; the `IAgentSession` member and default method; the mapper in `DotAcpAgentSession`; `FakeAgentSession` records blocks | `src/Huddle.Acp/Abstractions/`, `DotAcp/DotAcpAgentSession.cs`, `tests/Huddle.Acp.Tests/Fakes/FakeAgentSession.cs`, `tests/Huddle.Tests/Acp/Fakes/FakeAgentSession.cs` |

### Phase 2: reading an image (`Huddle.App/Library`)

| # | Task | Files |
| --- | --- | --- |
| T2.1 | **Test.** `ImageHeaderTests`, table-driven with checked-in minimal fixtures: PNG, baseline JPEG, progressive JPEG (`SOF2`), GIF, WebP lossy, lossless and extended; a truncated header, an empty span, text, a JPEG whose `SOF` lies past 64 KiB | `tests/Huddle.Tests/Library/ImageHeaderTests.cs` |
| T2.2 | **Implement.** `ImageHeader.TryReadSize` | `src/Huddle.App/Library/ImageHeader.cs` |
| T2.3 | **Test.** `ReadImageAsync`: returns bytes, MIME and size for a PNG; a text file named `.png` is `NotAnImage`; a file over `maxBytes` is `TooLarge` **without reading it** (assert by stream position or a growing-file fixture); an edge over the cap is `TooManyPixels`; a path outside the root is `Unreadable`; a deleted file is `Unreadable`; an SVG is `NotAnImage`; cancellation throws | `tests/Huddle.Tests/Library/LibraryFileServiceImageTests.cs` (new, beside `LibraryFileServiceReadTests.cs`) |
| T2.4 | **Implement.** `LibraryFileService.ReadImageAsync`, `LibraryImageResult`, `LibraryImageRefusal` | `src/Huddle.App/Library/` |
| T2.5 | **Test.** The four new `LibraryOptions` members have the §6.7 defaults and bind from `Team:Library` | `tests/Huddle.Tests/Library/LibraryOptionsTests.cs` |
| T2.6 | **Implement.** The options | `src/Huddle.App/Library/LibraryOptions.cs` |

### Phase 3: planning and collecting

| # | Task | Files |
| --- | --- | --- |
| T3.1 | **Test.** `PromptBlockPlannerTests`, one row per line of the §6.4 table, plus: the caps apply in first-seen order; a refused or over-cap image does not use up the budget; four images with a cap of four; the fifth is Path (or Unavailable); per-Turn bytes; a cap of zero or less sends none | `tests/Huddle.Tests/Library/PromptBlockPlannerTests.cs` |
| T3.2 | **Implement.** `PromptDelivery`, `PromptBlockPlanner`, the delivery on `LibraryDocumentItem` | `src/Huddle.App/Library/` |
| T3.3 | **Test.** The collector: only a path written in `texts[0]` is eligible; the same path in catch-up and the Message is eligible; an SVG, a PDF and an `Other` file are never blocks; a path outside every Library Root yields nothing; `ReadsFiles: false` with an image and `Images: true` is a Block; with `Images: false` is Unavailable | `tests/Huddle.Tests/Library/LibraryDocumentCollectorTests.cs` |
| T3.4 | **Implement.** The collector overload taking `PromptDelivery`; the existing one delegates with `Images: false` so every current caller is unchanged | `src/Huddle.App/Library/LibraryDocumentCollector.cs` |

### Phase 4: lines, assembly and the Room Session

| # | Task | Files |
| --- | --- | --- |
| T4.1 | **Test.** `BuildPrompt` renders `turn.libraryDocIncluded` for a Block and `turn.libraryImageUnavailable` for Unavailable; the existing golden prompts and every existing `BuildPrompt` test are unchanged; `PromptDefaultsFileTests` includes the two new keys; `PromptValidator` flags a missing placeholder | `tests/Huddle.Tests/Prompts/PromptDefaultsFileTests.cs`, `tests/Huddle.Tests/Acp/Sessions/BuildPromptCommandTests.cs`, `tests/Huddle.Tests/Acp/PromptGoldenTests.cs` |
| T4.2 | **Implement.** The two keys in `PromptCatalog`; the rendering in `AppendLibraryDocumentsBlock`; the defaults file | `src/Huddle.App/Prompts/PromptCatalog.cs`, `RoomSession.cs` |
| T4.3 | **Test.** `AdapterCatalog`: `PromptBlocks` defaults to `true` on the legacy and configured profiles; `false` is carried; a change to the options does not change the profile | `tests/Huddle.Tests/Acp/AdapterCatalogTests.cs` |
| T4.4 | **Implement.** `AdapterProfile.PromptBlocks`, `AdapterProfileOptions.PromptBlocks`, the catalog copy | `src/Huddle.App/Acp/AdapterProfile.cs`, `AdapterProfileOptions.cs`, `AdapterCatalog.cs` |
| T4.5 | **Test.** With a scripted `FakeAgentSession` and a host advertising `image`: a Turn whose Message names a PNG calls `PromptAsync` with the text and exactly one image block, text first; a catch-up-only mention carries none; a Command Turn and a Greeting carry none and never call the collector; `PromptBlocks: false` and a `None` host and a `null` host each carry none | `tests/Huddle.Tests/Acp/PersonaRunnerLibraryDocsTests.cs`, `tests/Huddle.Tests/Acp/Sessions/CommandTurnTests.cs` |
| T4.6 | **Implement.** `IPersonaHost.PromptCapabilities` and `DotAcpPersonaHost`; the delivery bundle in `RoomSession`; `BuildPromptBlocks`; the `PromptAsync(AgentPrompt, …)` call; the Information log | `src/Huddle.App/Acp/IPersonaHost.cs`, `DotAcpPersonaHost.cs`, `Sessions/RoomSession.cs`, `tests/Huddle.Tests/Acp/Fakes/FakePersonaHost.cs` |
| T4.7 | **Test.** A throwing `ReadImageAsync` (an `IOException`) leaves the Turn running with the path line; cancellation during the read sends no prompt | `tests/Huddle.Tests/Acp/PersonaRunnerLibraryDocsTests.cs` |

### Phase 5: a document's text as a resource (separable; stop here if cut)

| # | Task | Files |
| --- | --- | --- |
| T5.1 | **Test.** Planner and collector: `ReadsFiles: false` with `EmbeddedText` makes a Markdown or Text document a Block carrying a `file:///` URI (with a space and a non-ASCII character encoded), the right MIME type, and text cut at `MaxInlineBytes` with the existing cut note; without `EmbeddedText` the inline fenced text is unchanged; `ReadsFiles: true` never makes one | `tests/Huddle.Tests/Library/PromptBlockPlannerTests.cs`, `LibraryDocumentCollectorTests.cs` |
| T5.2 | **Implement.** The `AgentTextResourceBlock` case in the planner and the URI helper | `src/Huddle.App/Library/` |
| T5.3 | **Test.** `PromptAsync` with a resource block sends it as T1.4 expects (already in T1.4; here an end-to-end through a Room Session) | `tests/Huddle.Tests/Acp/PersonaRunnerLibraryDocsTests.cs` |

### Phase 6: documents

| # | Task | Files |
| --- | --- | --- |
| D6 | Add **Prompt block** to `language.md`. Mark this spec's hub-map row built and add the five configuration rows to the hub's table. Add a `known-limits.md` entry: an image stays in the session's context; `TraceWire` logs the base64; PDF, SVG and audio are not sent. Update `docs/acp/agent-guide.md` where it says prompts are text only. Add `manual-tests/prompt-blocks.md` covering P1, P2, P5, P6, P11 and P18. Mark roadmap item 23's A part built | `docs/agencyteam/language.md`, `docs/AgencyTeam.md`, `known-limits.md`, `docs/acp/agent-guide.md`, `docs/agencyteam/manual-tests/` |

### Phase 7: live checks (paid, small)

V-1 to V-4 in Appendix C. They run after Phase 4 and before the feature is called delivered. V-6 is a
precondition of PDF, not of V1.

---

## Appendix B — What the source showed

All *(source)*, read 2026-10-01. **No prompt with a block was sent to a live Adapter.**

### B.1 What `claude-agent-acp` 0.75.1 advertises

`dist/acp-agent.js`, in the `initialize` result:

```js
promptCapabilities: {
    image: true,
    embeddedContext: true,
},
mcpCapabilities: { http: true, sse: true },
```

`audio` is not advertised.

### B.2 How it converts each block (`promptToClaude`)

| ACP block | What the adapter does |
| --- | --- |
| `text` | Pushed as an Anthropic text block, after rewriting a leading `/mcp:server:command` form. The only block it inspects for commands |
| `image` with `data` | An Anthropic `image` block with a **base64** source: `{type:"base64", data, media_type: mimeType}` |
| `image` with only a `uri` | Used **only** if the `uri` starts with `http`, as a URL source. Otherwise the block is dropped without an error |
| `resource_link` | Reduced to one text block of `formatUriAsLink(uri)`. Name, MIME type and size are discarded |
| `resource` with `text` | A text link of the URI, plus a `<context ref="uri">…text…</context>` text block appended **after** every other block |
| `resource` with a `blob` | **Ignored**, with the comment "(unsupported)" |
| `audio`, anything else | Ignored |

The message carries `origin: {kind: "human"}`. The conclusions that follow: bytes, not URIs, for an
image (D-3); no `resource_link` (D-4); a blob resource is a silent drop, so PDF waits for a live check
(D-5, V-6); and the text block is first and unique in being read for commands (§6.6).

### B.3 What `dotacp.protocol` 2026.7.19 emits

The assembly was loaded and `ImageContent`, `TextContent` and `EmbeddedResource` (with a
`TextResourceContents`) were instantiated and serialised with Newtonsoft's default settings:

```json
[{"type":"text","_meta":null,"annotations":null,"text":"hi"},
 {"type":"image","_meta":null,"annotations":null,"data":"QUJD","mimeType":"image/png","uri":null},
 {"type":"resource","_meta":null,"annotations":null,"resource":{"_meta":null,"mimeType":"text/markdown","text":"# t","uri":"file:///E:/a%20b/n.md"}}]
```

The `type` discriminator is set by each class. `PromptCapabilities` has `Audio`, `EmbeddedContext`
and `Image`. `EmbeddedResourceResource` is the base of `TextResourceContents` and
`BlobResourceContents`, with a JSON converter. The connection's own settings may omit the `null`
members; T1.4 pins what actually crosses the wire.

### B.4 What the Library already provides

- `LibraryFileKinds.ImageContentType(ReadOnlySpan<byte> head)` returns `image/png`, `image/jpeg`,
  `image/gif` or `image/webp` from magic bytes, and `null` otherwise. A file is `LibraryFileKind.Image`
  only when its extension **and** its magic bytes agree.
- `LibraryFileService.ReadAsync` returns no bytes for an image: `Text` is `null` and only the length
  and kind come back. `ReadImageAsync` is therefore new.
- `LibraryOptions` already has `MaxInlineBytes` (16,384), `MaxReferencedDocuments` (10) and
  `MaxEditableBytes` (2 MiB).
- `LibraryDocumentCollector` runs only for `WorkItemKind.Message`, so a Command or a Greeting never
  reaches it.

---

## Appendix C — Verification tasks

| # | Question | How | Status |
| --- | --- | --- | --- |
| V-1 | Does a 3 MiB, 8,000 px PNG sent as a base64 `image` block reach the model through `claude-agent-acp` 0.75.1 and get described correctly? What does the provider enforce on bytes and pixels, what does the image cost in `usage_update`, and what does an animated GIF do? | A raw JSON-RPC harness against the real Adapter, as the Commands spec ran one; a few cheap turns | **Answered 2026-10-01** by `RealAdapterPromptBlockTests` (through the real `DotAcpAgentSession`, not a raw harness). A solid red 64 px PNG was described as `red`. A 3,001,523-byte PNG (1000 × 1000, incompressible) was accepted and the Turn ended `EndTurn`. Image cost in `usage_update`: the context fill moved from about 58,000 to about 59,300 with the 3 MB image and to about 61,400 with four of 1.97 MB each, so roughly **1,000 tokens an image whatever its size**: the provider downscales. The 8,000 px edge limit and an animated GIF were **not** run; the edge cap is Anthropic's documented figure and a GIF is sent as its bytes |
| V-2 | Does a `session/prompt` line of about 11 MB cross `dotacp`'s writer and the adapter's reader intact? | A harness sending four 2 MiB images to the real Adapter, and to `FakeAcpAgent` for the writer | **Answered 2026-10-01.** Four noise PNGs of 1.97 MB each (7,877,912 raw bytes, about 10.5 MB of base64 on one line) crossed the writer and the adapter's reader, and the model replied `FOUR` |
| V-3 | Does `@Nova` with a named PNG work end to end in the real app, on a Claude Persona, with the reply describing the picture? | `manual-tests/prompt-blocks.md` PROMPTBLOCKS-01; about $0.05 | **Answered 2026-10-01**, in the app on a Haiku Teammate: `BANANA 4821` and the circle's colour read out of a named PNG with no tool call; and a 4.3 MB PNG fell back to its path with `TooLarge=1` logged. PROMPTBLOCKS-02 (four of six images, `ImageCountCap=2`) and -05 (`PromptBlocks=false` gives `CANNOT SEE`) passed in the app the same day; PROMPTBLOCKS-04, which needs two Teammates in one Room, is deferred to user acceptance |
| V-4 | What does the Adapter do with a corrupt body behind a valid header: a refusal, or a hang? Does the Turn fail cleanly once? | A truncated PNG with a valid `IHDR` | **Answered 2026-10-01.** The Turn did not fail and did not hang: it ended `EndTurn` once, and the reply said `API Error: an image in the conversation could not be processed and was removed.` The Adapter drops the image from the conversation, so it cannot poison later Turns |
| V-5 | Does `agency-acp` advertise `image` or `embeddedContext`, and does its model accept an image? | Point the harness at `agency-acp`; free | Open. Not a V1 gate |
| V-6 | Does any Adapter we run honour a blob `resource` (PDF)? | As V-1, with a small PDF | Open. A V2 gate |
| V-7 | Does `agency-acp` or any other Adapter ignore a text `resource` when `embeddedContext` is advertised? | As V-5 | **Answered for `claude-agent-acp` 2026-10-01:** a text resource with `embeddedContext` advertised is read, the model replied `ZEBRA-4821` from it (and remarked that the `file:///` path does not exist on disk, which is expected for a probe). Still open for `agency-acp` |
