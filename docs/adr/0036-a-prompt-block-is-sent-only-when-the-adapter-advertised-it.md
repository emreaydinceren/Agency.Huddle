---
status: accepted
date: 2026-10-01
---

# A Prompt block is sent only when the Adapter advertised it, only from the Message that started the Turn

A Turn's prompt used to be one text block, and a Library file a Message names reached an Agent as a path
line. An image cannot be handed to a model by a path alone, so a Turn can now carry **Prompt blocks**:
an ACP `image` block for a named image and, for an Adapter with no file tools, a document's text as an
embedded `resource`. The full design is
[Huddle.PromptBlocks-Specifications.md](../Huddle.PromptBlocks-Specifications.md).

Three decisions are hard to reverse and surprising without this context, so they are recorded together.

## The decisions

**1. A block is sent only when the Adapter advertised it, and every guard falls back to the path line.**
The capability comes from `initialize` (`agentCapabilities.promptCapabilities`), is carried on
`AgentHostInfo` and `IPersonaHost`, and is combined with a per-profile kill switch
(`Team:Acp:Adapters:*:PromptBlocks`, default `true`) that can only turn blocks **off**. An Adapter that
advertised nothing receives today's prompt byte for byte. An image that is too large, too many, too wide,
not really an image or from the wrong Message becomes the path line it already was. A Prompt block
problem never fails a Turn that would have succeeded without it.

**2. Only the Message that started the Turn can contribute a block.** Never catch-up, the Transcript or
an earlier Message. Otherwise one old image is re-sent, as pixels, on every Turn that mentions it, and a
single poison image would be re-delivered until the Room is archived.

**3. The seam carries `AgentPrompt`, and the string overload stays as a default interface method.**
`IAgentSession.PromptAsync(AgentPrompt, ct)` is the one method an implementer must write, so none can
handle the string form and silently drop blocks. The DotAcp layer owns base64, URIs and the wire types;
the planner in `Huddle.App` never sees them, which is also what keeps ACP v2's reshaped capability
object confined to one mapping.

## Why

`claude-agent-acp` 0.75.1 was read in source: it advertises `image` and `embeddedContext`; uses an
image's `data` and **silently drops** one that carries only a non-`http` `uri`; reduces a
`resource_link` to a link of its URI; turns a text `resource` into a link plus an appended `<context>`
block; and **ignores** a blob `resource` and audio. A design that "sent a `file://` image" or "sent a PDF"
would therefore succeed on the wire and show the model nothing, with no error anywhere. Bytes, never
URIs; no `resource_link`; no PDF until a live check says an Adapter honours one.

## Consequences

- **`Huddle.Acp` gains** three records, one interface member and one trailing optional `AgentHostInfo`
  member. The ACP effort must be told. `FakeAcpAgent` is not edited.
- **An image stays in the Adapter session's context** after the Turn and counts against the token
  Budget like any other input. `TraceWire` logs the base64. See [Known limits](../engineering/known-limits.md).
- **The config keys are public:** `Team:Library:MaxImageBytes`, `MaxImagesPerTurn`,
  `MaxImageBytesPerTurn`, `MaxImageEdgePixels` and `Team:Acp:Adapters:*:PromptBlocks`.
- **SVG, PDF and audio are never sent.** SVG is XML that can carry script (the avatar rule); the other
  two are dropped by the Adapter we run.
- **B (attach or paste in the composer)** reuses this: save the file under a Library Root and insert the
  path. Roadmap item 23.

## Rejected

**Sending every referenced file as a block whenever the Adapter advertises the capability.** Costs
pixels on every catch-up Turn and gives the Human no way to say "only this one".

**An `image` block with a `file://` `uri`.** Silently dropped by the Adapter we run.

**Retrying a rejected prompt without its blocks.** Cannot tell why the Adapter refused, and a retry
doubles the cost of a failure that may be the image itself.

**A per-Persona switch.** The Adapter is what advertises and honours a capability.
