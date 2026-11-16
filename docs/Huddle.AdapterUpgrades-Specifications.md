# Huddle.AdapterUpgrades — Specification

What `claude-agent-acp` 0.76–0.100 can do that Huddle does not use yet, which of it Huddle takes, and
how. Written 2026-11-03, after PR #336 (`feat/acp-session-notices`) moved
`tools/acp` from 0.75.1 to 0.100.0 and added session notices.

Every wire fact below was read from the vendored packages on 2026-11-03
(`tools/acp/node_modules`: `claude-agent-acp` 0.100.0, `@agentclientprotocol/sdk` 2.4.0,
`@anthropic-ai/claude-agent-sdk` 0.3.316) or from `dotacp.protocol` 2026.7.19 by reflection. A
claim marked **Assumed** was not observed and is checked by a task before code depends on it.

## 1. Goal and scope

Take the adapter improvements that make Huddle better for the Human, at the lowest risk first:

| § | Feature | Why it matters |
| --- | --- | --- |
| §3 | PDF and other file attachments | A PDF the Human names reaches the model, instead of a path line it cannot read |
| §4 | Sign-in and start-up failures, named | An expired login mid-Turn says "sign in again" instead of a generic failure |
| §5 | Tool names on tool calls | Memory-tool attribution keys on the tool's name, not on its title |
| §6 | The model that answered | The context-fill line shows what an alias such as `haiku` resolved to |
| §7 | Deleting the old session on a fresh start | Answers Long-running Rooms Q-4; old transcripts stop piling up in the Adapter |
| §8 | Compaction updates | Not taken: advertising the capability would lose the legacy tool call's token figures |

Out of scope, with reasons, in §9.

## 2. What the adapter offers, and what dotacp can carry

dotacp 2026.7.19 is built on ACP schema 1.19.0 and has had no release since July. Its
`SessionUpdate` discriminator knows exactly: `agent_message_chunk`, `agent_thought_chunk`,
`available_commands_update`, `config_option_update`, `current_mode_update`, `plan`,
`session_info_update`, `tool_call`, `tool_call_update`, `usage_update`, `user_message_chunk`. Any
other kind is **silently dropped** (traps.md, "dotacp drops a `session/update` kind newer than its
schema"). PR #336 established the pattern for a newer kind: advertise the capability, rename the line
in `NdjsonMethodRewriteStream` to an underscore method, and handle it in `DotAcpClientAdapter`,
marking it dispatched in the `WireOrderLedger`.

| Offer | Wire shape (adapter 0.100.0) | dotacp 1.19 carries it? | Needs the rename pattern? |
| --- | --- | --- | --- |
| Blob resources in prompts (0.99) | An embedded `resource` with `blob` (base64) and `mimeType`. `blob-resources.js` `blobResourceToClaude`: `image/png|jpeg|gif|webp` → image, `application/pdf` → Anthropic `document`, text types → decoded text, anything else → dropped | Yes: `BlobResourceContents` | No (outbound) |
| `authRequired` mid-Turn (0.99.1) | `session/prompt` fails with ACP error `-32000` (`RequestError.authRequired`), message "Run /login…" style | Yes: `ErrorCode.AuthenticationRequired` | No |
| Start-up failure reason (0.100) | `session/new` fails with the CLI's own message and `data.startupFailureReason` (`startup-failure.js`); sign-in reasons come back as `authRequired` | Yes: `RemoteInvocationException.Message` | No |
| Tool name (0.77) | `tool_call.name` at the top level, **and** `tool_call._meta.claudeCode.toolName` for every non-AIR client (`renderer.js` `toolUseMeta`) | `name`: no. `_meta`: yes (`ToolCall.Meta`) | No: read `_meta` |
| Model on usage (0.81.1) | `usage_update._meta["_claude/model"]`, set once the turn has an assistant message (`attachUsageModel`) | Yes: `UsageUpdate.Meta` | No |
| `session/delete` (stable; 0.91 lets it succeed with no transcript) | ACP method `session/delete`; advertised in `agentCapabilities.sessionCapabilities.delete` | Yes: `Connection.DeleteAsync`, `SessionCapabilities.Delete` | No |
| Compaction (schema 1.25, stable) | `compaction_update` `{compactionId, status, summary?, error?, _meta?}` and `compaction_summary_chunk` `{compactionId, content}`. Sent only when the client advertises `clientCapabilities.session.compaction`; otherwise the adapter keeps reporting a `tool_call` "Compact conversation" (Commands spec §6.6) | **No** | **Yes**, for both kinds |

## 3. PDF and other file attachments

**Today.** [PromptBlocks](Huddle.PromptBlocks-Specifications.md) sends a Library image as an `image`
block and a text document as a text `resource` (§6.4). A PDF gets the path line only: row P9 and
§3 "PDF and other blob resources" parked it, because adapter 0.75.1 ignored a blob `resource`
(Appendix B.2). V-6 asked whether any Adapter honours one.

**Decision.** A PDF the Human names in the Message that started the Turn goes as an embedded blob
resource when the Adapter advertised `embeddedContext` and the Adapter Profile allows it, under its
own count and byte caps. Everything else about PromptBlocks holds: D-2 (only the triggering Message),
D-3 (bytes, never a URI), D-10 (fall back to the path line, never fail the Turn), D-11 (no retry
without blocks).

- **Kind.** `LibraryFileKind` gains `Pdf`. `LibraryFileKinds.Detect` returns it for the `.pdf`
  extension **and** a head starting `%PDF-` (bytes `25 50 44 46 2D`); a `.pdf` without that magic is
  `Other`.
- **Block.** `AgentBlobResourceBlock(string Uri, string MimeType, ReadOnlyMemory<byte> Data)`, an
  `AgentPromptBlock` in `Huddle.Acp.Abstractions`. `DotAcpAgentSession.MapPrompt` maps it to
  `EmbeddedResource { Resource = BlobResourceContents { Uri, MimeType, Blob = base64(Data) } }`.
- **Caps.** `LibraryOptions.MaxDocumentsPerTurn` (default `2`) and
  `LibraryOptions.MaxDocumentBytesPerTurn` (default `10485760`, 10 MiB). A PDF larger than the
  remaining bytes falls back to the path line with `PromptBlockWithheld.TurnBytesCap`.
  **Assumed:** 10 MiB is under what the provider accepts behind the adapter (Anthropic documents a
  32 MB request cap). Task 1.7 checks one real PDF.
- **Delivery.** `PromptDelivery` gains `bool Documents`, `int MaxDocumentsPerTurn`,
  `long MaxDocumentBytesPerTurn`. `Documents` is `embeddedText && !readsFiles`, the same gate as a
  text resource (PromptBlocks §6.4), so an Adapter with file tools still reads the file itself.
- **Withheld reasons.** Reuse `PromptBlockWithheld.TurnBytesCap` and `.Unreadable`; add
  `.DocumentCountCap`.
- **V-6** is answered by the live test in task 1.7.

## 4. Sign-in and start-up failures, named

**Today.** `DotAcpAgentHost` maps `AuthenticationRequired` to `AgentAuthenticationRequiredException`
on `session/new` and resume only. `DotAcpAgentSession.PromptAsync` (`DotAcpAgentSession.cs`
:244-246) wraps every `RemoteInvocationException` as `AgentException("session/prompt failed: …")`,
so an expired login during a Turn reads as an ordinary failure. `PersonaSupervisor.DescribeStartFailure`
(`PersonaSupervisor.cs` :398-406) already turns the start-time exception into Human copy.

**Decision.**

- `DotAcpAgentSession.PromptAsync` maps `ErrorCode.AuthenticationRequired` to
  `AgentAuthenticationRequiredException`, with an empty `AuthMethods` list and the adapter's message
  kept as the exception's `Message`. That needs a second constructor:
  `AgentAuthenticationRequiredException(IReadOnlyList<AuthMethodInfo> authMethods, string message)`.
- The Turn failure that reaches `PersonaHealth` for this exception says
  **"Claude needs you to sign in again. Run `claude` in a terminal and sign in, then send the Message again."**
  (copy is bound by `docs/engineering/language.md`; task 2.3 checks it there).
- A start-up failure keeps using the adapter's own message, which since 0.100 names the fix (for
  example a bad `HTTPS_PROXY`). Task 2.4 pins that `DescribeStartFailure` passes it through unchanged.

## 5. Tool names on tool calls

**Today.** `RoomSession.RecordMemoryToolPath` (`RoomSession.cs` ~:2401) recognises a `memory` App Tool
call by `kind == Other` **and** its title passing `MemoryTool.IsToolName(title)`. The title is
Adapter-chosen display text.

**Decision.** `ToolCallStarted` and `ToolCallUpdated` gain a trailing optional `string? ToolName = null`.
`SessionUpdateMapper` reads it from `Meta["claudeCode"]["toolName"]` when that is a string, else null.
`RecordMemoryToolPath` uses `ToolName` when it is non-null and falls back to the title otherwise (an
Adapter that sends no `_meta`, such as `agency-acp`, keeps today's behaviour). The wire contract
`ToolActivity` is **not** changed.

## 6. The model that answered

**Today.** `UsageUpdated(SessionId, Size, Used, Cost)` carries no model. The Agent card shows the stored
Model id (`haiku`), not what it resolved to (Haiku 5.5 since SDK 0.3.316). The context-fill line
(`ContextFillText`, drawn on the newest Message in `MessageList.razor` :109 and in
`MembersPanel.razor` :45) shows only how full the window is.

**Decision.**

- `UsageUpdated` gains a trailing optional `string? Model = null`, read by `SessionUpdateMapper` from
  `Meta["_claude/model"]` when that is a string.
- `ContextFillReading` gains a trailing optional `string? Model = null`. `RoomSession.CountUsage`
  passes the latest non-null model through `ContextFill`.
- `ContextFillText.Tooltip` appends **" Answered by {Model}."** when a model is known, so both places
  that draw it pick it up; `ContextFillText.Label` (the percentage) is unchanged. Exact copy is bound
  by `language.md` (task 4.4).
- **Not** in scope: Spend by model. `persona_spend` would need a sibling table (rules.md row on
  persisted state), and nothing asks for the breakdown yet (§11 OQ-2).

## 7. Deleting the old session on a fresh start

**Today.** A fresh start (Long-running Rooms §6) opens a new session and leaves the old one in the
Adapter. LR §6 line 489 and Q-4 deferred `session/delete` until `IAgentSession` supports it.

**Decision.**

- `AgentHostInfo` gains `bool SupportsDeleteSession`, from `agentCapabilities.sessionCapabilities.delete is not null`
  (the same shape as `CanResume`).
- `IAgentSession` gains `Task DeleteAsync(CancellationToken cancellationToken)` as a default interface
  member returning `Task.CompletedTask`, so fakes and other implementers need no change.
  `DotAcpAgentSession` sends `session/delete` when its host's `SupportsDeleteSession` is true, and treats
  `ResourceNotFound` as success.
- `RoomSession`'s fresh-start teardown (`RoomSession.cs` :440-560, `pendingFreshTeardown`) calls
  `DeleteAsync` on the old session **after** it is closed. A failure is logged at Warning and never
  fails or delays the fresh start.
- A session already evicted (closed by idle eviction) before the fresh start is not deleted: only its
  id remains in the store, so the old id stays in the Adapter. The same holds when a fresh start finds no
  live `RoomSession` at all (`RoomSessionPool.cs` :386-394, for example after an app restart): the store forgets
  the id only and nothing is deleted. Accepted.
- Only a **fresh start** deletes. A Restart, an app restart and a Persona edit keep today's behaviour
  (rules.md row "Restart forgets Room Sessions; an app restart resumes them").

## 8. Compaction updates — not taken (decided 2026-11-03 after the 6.1 spike)

**Planned.** Advertise `clientCapabilities.session.compaction`, publish an `AgentCompaction` event per
`compaction_update`, and show a compaction as itself with its token figures.

**Observed** (adapter 0.100.0, Haiku, a `/compact` Command Turn):

| | Advertised | Not advertised |
| --- | --- | --- |
| Compaction messages | `compaction_update` `{compactionId, status, summary?}`: `in_progress`, then `completed` with an inline `summary` (text content blocks), then a second `completed` without one | `tool_call` "Compact conversation" (`_meta.claudeCode.toolName: "compact"`) and its updates |
| Token figures and duration | None: `_meta` is attached only for JetBrains AIR clients, and Huddle must not impersonate AIR (Adapters-Vendors D-V12) | `rawOutput {trigger: "manual", preTokens, postTokens, durationMs}` |
| `compaction_summary_chunk` | Never sent; the summary rides in `compaction_update.summary` | Not applicable |
| Legacy `tool_call` | **Removed** | Sent |
| `usage_update` after it | Sent (`used` falls, e.g. 47224 to 10525) | Sent |

**Why not taken.** Advertising the capability removes the legacy tool call, the only source of the
before and after token figures and the duration that a `/compact` Command Turn's outcome text
(Commands §6.6, §8.3) reads. The replacement carries none of them, so the change would lose
information. Without the capability the legacy path is unchanged, so nothing is built:
`AdvertiseCompaction`, `HuddleSessionCapabilities`, the `_session/compaction` rewrite and `AgentCompaction`
are not planned.

**Follow-up (unverified).** The legacy `rawOutput` carries `trigger: "manual"` for `/compact`. An
automatic compaction during an ordinary Turn was not observed, so whether it shows `trigger: "auto"` is
unknown. If it does, Huddle could spot automatic compactions without advertising and use them as a usage
baseline (`usageBaselinePending`) and a "Context compacted" Notice row. Today an automatic compaction is
invisible: the next `UsageUpdated` simply drops.
## 9. Not taken, and why

| Offer | Why not |
| --- | --- |
| Prompt suggestions (0.92) | Published only to JetBrains AIR clients (`acp-agent.js` `if (!this.toolCallCapabilities.air.client) return;`). Getting them means impersonating AIR, which Adapters-Vendors D-V12 forbids for the same family of keys |
| Session fork into another folder (0.99.1) | `session/fork` is unstable and dotacp has no method for it |
| Subagent reporting, background tasks, monitors (0.87–0.100) | Behind the unstable subagents RFD and AIR extensions; revisit when stable |
| Session titles (`session_info_update.title`, 0.95) | Huddle names Rooms itself (Room auto-naming); a second source of titles is a product decision nobody asked for |
| Thinking display, ultracode, Remote Control (0.93–0.99) | Claude Code features with no Huddle surface; each would be its own decision |
| Compaction updates (schema 1.25, §8) | Advertising `session.compaction` removes the legacy tool call and its `rawOutput` figures, and the `compaction_update` has no `_meta` figures outside AIR (6.1 spike, 2026-11-03) |
| Moving to ACP v2 | Still alpha (`schema-v2.0.0-alpha.13`); PromptBlocks §14.16 already records what changes |

## 10. Configuration

| Key | Default | § |
| --- | --- | --- |
| `Library:MaxDocumentsPerTurn` | `2` | §3 |
| `Library:MaxDocumentBytesPerTurn` | `10485760` | §3 |

Each key gets a row in the Engineering Guide's configuration table in the same task that adds it.

## 11. Open questions

| # | Question | Default until answered |
| --- | --- | --- |
| OQ-1 | Should a PDF also go to an Adapter with file tools (`readsFiles`)? | No, as PromptBlocks D-1 decided for text |
| OQ-2 | Spend by model? | Not built (§6) |
| OQ-4 | Does Haiku 5.5 now offer Effort levels? The user guide says Haiku has none | **Answered 2026-11-03: yes, Haiku 5.5 offers `low`, `medium`, `high`, `xhigh` and `max`.** Checked with the SDK's `supportedModels()` (`tools/acp`): the `haiku` row (Haiku 5.5) has `supportsEffort: true` and those five `supportedEffortLevels`. The older `claude-haiku-4-5-20251001` row (Haiku 4.5) has no effort fields. The user guide's Effort row is corrected to say so |

## 12. Verification

- Unit tests for each mapping, planner rule and option, written first (plan conventions).
- Live checks behind `TEAM_E2E=1`, a few cheap Haiku turns each: a PDF code word (§3, V-6), the model
  on usage (§6), `session/delete` (§7), and the compaction spike (§8, done; not taken).
- Manual test rows added to the existing pages: `prompt-blocks.md` (a PDF) and `turn-detail.md`
  (dropped with §8).
