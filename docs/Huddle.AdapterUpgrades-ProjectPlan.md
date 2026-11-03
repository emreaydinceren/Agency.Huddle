# Huddle.AdapterUpgrades — Project Plan

This plan breaks [`Huddle.AdapterUpgrades-Specifications.md`](Huddle.AdapterUpgrades-Specifications.md)
(the **Spec**) into atomic, self-contained tasks for subagents with **no project context**. Each task
names exact paths, types, signatures and acceptance criteria, and cites the Spec section it builds.

**8 deliverables · 55 tasks · 17 of them sized for Haiku (31%) · 3 retrospectives.** Every
implementation task (`.i`) follows its test task (`.t`). A `.t` ends **red for the right reason**, and
its `.i` partner ends **green**. Chore, docs, live and verification tasks have no test partner.

> [!IMPORTANT]
> **Starts after PR #336 (`feat/acp-session-notices`) is merged.** Every task
> assumes `claude-agent-acp` 0.100.0 in `tools/acp`, `AgentNotice`, `NoticeSessionCapabilities` and
> the `_session/notice` rename are on `main`.

| Stream | Deliverables | Spec | May start |
| --- | --- | --- | --- |
| **A — Setup** | D0 | — | First |
| **B — PDF attachments** | D1 | §3 | After D0 |
| **C — Sign-in failures** | D2 | §4 | After D0; parallel with B |
| **D — Tool names, then model** | D3, D4 | §5, §6 | After D0. D4.1 after D3.1 (both edit `SessionUpdateMapper.cs` and `AgentEvent.cs`) |
| **E — Session delete** | D5 | §7 | After D0; parallel with B, C, D |
| **F — Compaction** | D6 | §8 | After D5 (both edit `RoomSession`'s session lifecycle) and after D4.2 (both edit `CountUsage`) |
| **G — Close** | D7 | §10–§12 | Last |

---

## How this plan is run

The **manager** (the orchestrating session) dispatches one subagent per task, reviews the result and
runs the retrospectives. Subagents never dispatch each other. The playbook is
[`agents/DeliveryPlaybook.md`](../agents/DeliveryPlaybook.md); its scripts are in
[`agents/scripts/`](../agents/scripts/README.md).

### Model assignment

| Risk tag | Implementer | Architect review |
| --- | --- | --- |
| `data` (records, enums, options, pure mappings whose whole rule table is in the task, doc rows) | **Haiku** | None |
| `logic` | **Sonnet** | None |
| `boundary` (protocol, wire, session lifecycle, concurrency) | **Sonnet** | **Opus**, once per deliverable, before the deliverable is called done |

A Haiku agent that finds a task ambiguous, or its test impossible as written, **stops and reports**
instead of improvising; the manager reissues it to Sonnet with the clarification. A Haiku agent that
reaches 40 tool calls without a red or a green stops too. Every implementer stops at **60 tool calls**.

### Dispatch protocol

1. A `.t` prompt starts with *STOP at red: write no `src/` code*. The agent saves the red with
   `agents/scripts/Run-Tests.ps1 -FilterClass <X> -Label <task> -RedTask <task> -NewNames "<names>"`
   and stops. The manager freezes it (`agents/scripts/Freeze-Tests.ps1 -Freeze`), then resumes the
   **same** agent with `SendMessage` for the `.i`, unless it is past ~120K context or the `.i` is
   `boundary`: then a fresh agent gets the `.i`.
2. A test that passes on arrival is not done: the agent breaks the product line it covers, shows the
   red, and restores it by inverse edit (never `git checkout`, which wipes the feature's other edits).
3. Live tasks (marked **Paid**) spend real tokens. The manager asks the Human before dispatching each one.
4. The final report of every agent is at most 25 lines: files touched, test names, red/green
   evidence, anything it could not do.

### Retrospectives

After tasks **15, 30 and 45** are done, an **Opus** agent runs a retrospective. It does **not** read raw
transcripts end to end. The manager hands it:

- the token tally from `Execute-Project-Plan` (Appendix D), and
- the paths of the subagent logs of the **three most expensive agents** since the last retrospective,
  to grep for repeated reads and repeated searches.

It reports (a) what several agents spent time finding that the plan could have stated up front, with
the exact sentence to add to the **Conventions** section or to named upcoming tasks, and (b) token
cost: which agents grew their context most and why (orientation reading whole documents, agents kept
past the cap, oversized reports, Opus or Sonnet used where Haiku would do), with one fix each. The
manager then **edits this plan**: it updates Conventions and the **Read first** of the affected
upcoming tasks, and records the change in [Retrospective log](#retrospective-log).

---

## Conventions (read once; tasks refer here)

| Topic | The fact | Where |
| --- | --- | --- |
| Build | `dotnet build Huddle.slnx` from the repo root. Warnings are errors | `CLAUDE.md` "C# code" |
| Test | `dotnet test Huddle.slnx --`: the trailing `--` is required. One class: `tests/<Project>/bin/Debug/net10.0/<Project>.exe -class <FullName>` | `CLAUDE.md`; traps.md "Extra flags before the trailing `--`" |
| House style | `this.` on members, file-scoped namespaces, `sealed`, explicit type on the left with `new()`, no `!`, guards with throw helpers, `ConfigureAwait(false)` in `src` (not in `Components/`) | `agents/CSharpPrinciples.md` "Rules that fail the build most often" |
| Logging | `Huddle.Acp`: `[LoggerMessage]` partials only. `Huddle.App`: direct `this.logger.LogX` is allowed, but guard with `IsEnabled` when an argument is computed (CA1873) | `agents/CSharpPrinciples.md` "Logging" |
| Tests | Every test has a `///` summary, `TestContext.Current.CancellationToken`, exact asserts (`Assert.Equal` on text, whole collections), and a `// contains-ok:` comment on any `Contains` over non-markup text | `agents/Testing.md` lines 117–157 |
| Shared helpers | `RecordingLogger<T>`, `Eventually.UntilAsync`, `TestBudgets`, `TempDataDir`, `TestOptions.Create`, `TestPersonas.Nova` | `agents/Testing.md` lines 264–367 |
| ACP fake (raw wire) | `FakeAgentProcessLauncher` + `FakeAcpAgent`. `launcher.Agent.OnPrompt = async context => { await context.SendUpdateAsync(new JsonObject{...}); return "end_turn"; }`. `launcher.Agent.Received` lists every message the host sent | `tests/Huddle.Acp.Tests/Fakes/PromptContext.cs` :73; `tests/Huddle.Acp.Tests/DotAcp/DotAcpAgentHostNoticeWireTests.cs` (whole file, ~150 lines, the model to copy) |
| **Linked fakes** | `FakeAcpAgent.cs`, `PromptContext.cs` and `FakeRpcError.cs` are compiled into **two** assemblies (`Huddle.Acp.Tests` and `mock-acp`). Editing one means running `Huddle.Acp.Tests` **and** the `MockAdapter/` folders of `Huddle.Tests.Agents` and `Huddle.Tests.Personas` | `CLAUDE.md` "Two projects" |
| App-side session fake | `FakeAgentHostFactory` + `FakeAgentSession`: `factory.Session.EnqueueToolActivity(params AgentEvent[])` publishes any events before the next reply; `EnqueueReply("text")` | `tests/Huddle.Tests.Common/Acp/Fakes/FakeAgentSession.cs` :160–300 |
| Runner test shape | `FakePersonaServer` + `PersonaRunner` + `ReceiveUntilAsync<ToolActivity>` / `<PostMessage>` | `tests/Huddle.Tests.Agents/Acp/PersonaRunnerTests.cs`, test `AdapterNotice_IsWrittenAsANoticeRow_AndKeptOutOfTheReply` |
| Live test shape | `[Fact(Timeout = 240000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]`, `[Collection("E2E")]`, real `DotAcpAgentHost` over `new AgentProcessLauncher(...)` with `E2E.AdapterEntry` | `tests/Huddle.Acp.Tests/E2E/RealAdapterNoticeTests.cs` (whole file, ~140 lines) |
| Reading `_meta` in the mapper | dotacp hands `Meta` as `Dictionary<string, object>`: an object or array value arrives as a Newtonsoft `JToken`, but a top-level primitive arrives as a plain .NET value (a `string`, not a `JValue`; found by the 4.5 live run). Accept both. Read a nested string with `meta.TryGetValue("claudeCode", out object? v) && v is JObject o && o.Value<string>("toolName") is { } name` | `src/Huddle.Acp/DotAcp/DotAcpClientAdapter.cs` `ReadString`, `PublishNotice` |
| Docs | Adding or renaming a page under `docs/` changes `docs/Index.md` in the same commit. A new `Team:` key gets a row in `docs/Huddle.EngineeringGuide.md`'s configuration table (the `Acp:AdvertiseSessionNotices` row is the model) | `CLAUDE.md` "Orientation" |
| Checks | `pwsh agents/scripts/Check-All.ps1` before every commit | `docs/engineering/rules.md` last row |
| Commits | Only the manager commits, once per deliverable, conventional message, no attribution lines | `agents/DeliveryPlaybook.md` |

---

## D0 — Setup

### Task 0.1 — Branch and baseline [Haiku]

- **Goal:** Start the delivery from a green `main` that already carries PR #336, as the Spec's header requires.
- **Read first:** this plan's Conventions table; `tools/acp/package.json`.
- **Risk:** `data`.
- **Deliverable:** `git fetch origin`; confirm `git merge-base --is-ancestor <PR #336 head> origin/main`; create `feat/adapter-upgrades` from `origin/main`. Run `pwsh tools/acp/install.ps1`, then build and the full test suite. Record the pass/skip counts in the report.
- **Acceptance:** `tools/acp/package.json` pins `0.100.0`; `src/Huddle.Acp/DotAcp/NoticeSessionCapabilities.cs` exists; build 0 warnings; suite `failed: 0`.

---

## D1 — PDF and other file attachments (Spec §3)

### Task 1.1.t — A PDF is detected as `Pdf` [Haiku]

- **Goal:** Pin Spec §3 "Kind": `.pdf` with `%PDF-` magic is `LibraryFileKind.Pdf`; without it, `Other`.
- **Read first:** `src/Huddle.App/Library/LibraryFileKinds.cs` `Detect` (lines 80–130); `tests/Huddle.Tests.Library/Library/LibraryFileKindsTests.cs` (the first two tests, for shape).
- **Risk:** `data`.
- **Deliverable:** In `LibraryFileKindsTests`, add `Detect_PdfWithMagic_IsPdf` (`"report.pdf"`, head `"%PDF-1.7\n"u8`), `Detect_PdfUpperCaseExtension_IsPdf` (`"REPORT.PDF"`), `Detect_PdfWithoutMagic_IsOther` (`"report.pdf"`, head `"hello"u8`).
- **Acceptance:** Red with `CS0117` (`LibraryFileKind` has no `Pdf`).

### Task 1.1.i — Add `LibraryFileKind.Pdf` [Haiku]

- **Goal:** Make 1.1.t green per Spec §3 "Kind".
- **Read first:** `src/Huddle.App/Library/LibraryFileKind.cs`; `LibraryFileKinds.cs` `Detect` and `IsSupported` (lines 60–130).
- **Risk:** `data`.
- **Deliverable:** Append `Pdf` to `LibraryFileKind` with a `///` summary. In `Detect`, before the image check, return `Pdf` when the extension is `.pdf` (`StringComparison.OrdinalIgnoreCase`) and `head.StartsWith("%PDF-"u8)`. Then `grep -rn "LibraryFileKind\." src/Huddle.App` and, in every `switch` over the enum, give `Pdf` the same arm as `Other` (D1.4 changes the planner's arm).
- **Acceptance:** 1.1.t green; `LibraryFileKindsTests` all green; build clean.

### Task 1.2.t — A blob block reaches the wire as an embedded blob resource [Sonnet]

- **Goal:** Pin Spec §3 "Block": `AgentBlobResourceBlock` maps to `{"type":"resource","resource":{"uri","mimeType","blob"}}`.
- **Read first:** `src/Huddle.Acp/Abstractions/AgentPrompt.cs` (whole, ~25 lines); `src/Huddle.Acp/DotAcp/DotAcpAgentSession.cs` `MapPrompt` (lines 170–205); `tests/Huddle.Acp.Tests/DotAcp/DotAcpAgentSessionTests.cs` class `Harness` (~line 730) and any test that reads `launcher.Agent.Received` for `session/prompt`.
- **Risk:** `boundary`.
- **Deliverable:** In `DotAcpAgentSessionTests`, add `PromptAsync_BlobBlock_SentAsEmbeddedBlobResource`: prompt `new AgentPrompt("see attached", [new AgentBlobResourceBlock("file:///E:/r.pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 })])`; assert the received `session/prompt` `params.prompt[1]` deep-equals `{"type":"resource","resource":{"uri":"file:///E:/r.pdf","mimeType":"application/pdf","blob":"JVBERg=="}}`.
- **Acceptance:** Red with `CS0246` (`AgentBlobResourceBlock`).

### Task 1.2.i — `AgentBlobResourceBlock` and its mapping [Sonnet]

- **Goal:** Make 1.2.t green per Spec §3 "Block".
- **Read first:** same files as 1.2.t.
- **Risk:** `boundary`.
- **Deliverable:** In `AgentPrompt.cs` add `public sealed record AgentBlobResourceBlock(string Uri, string MimeType, ReadOnlyMemory<byte> Data) : AgentPromptBlock;` with `///` docs mirroring `AgentTextResourceBlock`. In `MapPrompt` add `case AgentBlobResourceBlock blob:` adding `new dotacp.protocol.EmbeddedResource { Resource = new dotacp.protocol.BlobResourceContents { Uri = blob.Uri, MimeType = blob.MimeType, Blob = Convert.ToBase64String(blob.Data.Span) } }`. If `BlobResourceContents.Blob` is named differently, find it by reflection on `dotacp.protocol.dll` and say so in the report.
- **Acceptance:** 1.2.t green; `DotAcpAgentSessionTests` all green.

### Task 1.3.t — Document caps and delivery flags [Haiku]

- **Goal:** Pin Spec §3 "Caps" and "Delivery".
- **Read first:** `src/Huddle.App/Library/LibraryOptions.cs` lines 25–45; `src/Huddle.App/Library/PromptDelivery.cs`; `src/Huddle.App/Library/LibraryDocumentCollector.cs` `DeliveryFor` (lines 75–83); `tests/Huddle.Tests.Library/Library/LibraryOptionsTests.cs` (shape).
- **Risk:** `data`.
- **Deliverable:** Tests: `LibraryOptions_DocumentDefaults` (`MaxDocumentsPerTurn == 2`, `MaxDocumentBytesPerTurn == 10485760`); in `LibraryDocumentCollectorTests`, `DeliveryFor_Documents_FollowEmbeddedTextAndNotReadsFiles` as a `[Theory]` over `(readsFiles, embeddedText) -> Documents`: `(false,true)->true`, `(true,true)->false`, `(false,false)->false`, plus the two caps copied from options.
- **Acceptance:** Red with `CS1061`/`CS0117` on the new members.

### Task 1.3.i — Add the caps and flags [Haiku]

- **Goal:** Make 1.3.t green per Spec §3.
- **Read first:** same as 1.3.t.
- **Risk:** `data`.
- **Deliverable:** `LibraryOptions`: `public int MaxDocumentsPerTurn { get; set; } = 2;` and `public int MaxDocumentBytesPerTurn { get; set; } = 10485760;` with `///` docs in the style of `MaxImagesPerTurn`. `PromptDelivery`: append `bool Documents, int MaxDocumentsPerTurn, long MaxDocumentBytesPerTurn`. `DeliveryFor`: `Documents = embeddedText && !readsFiles`. Fix every other `new PromptDelivery(` (grep `tests/` too). Add `DocumentCountCap` to `PromptBlockWithheld` with a summary. Add both keys to the Engineering Guide configuration table (`Library:` rows).
- **Acceptance:** 1.3.t green; build clean; Engineering Guide has two new rows.

### Task 1.4.t — The planner's PDF rules [Sonnet]

- **Goal:** Pin Spec §3 decisions as a rule table for `PromptBlockPlanner`.
- **Read first:** `src/Huddle.App/Library/PromptBlockPlanner.cs` (whole, ~130 lines); `tests/Huddle.Tests.Library/Library/PromptBlockPlannerTests.cs` (helpers at the bottom and two image tests); `docs/Huddle.PromptBlocks-Specifications.md` §6.4 (lines 366–411).
- **Risk:** `logic`.
- **Deliverable:** `PlanAsync` will take a new parameter `Func<LibraryPath, CancellationToken, Task<ReadOnlyMemory<byte>?>> readDocument` after `readImage`. Tests, one per row, a PDF input with `FromTrigger` true unless stated: (1) delivered as `AgentBlobResourceBlock` with `application/pdf` and the file URI; (2) `Documents` false → fallback (`Path` when `ReadsFiles`, else `Unavailable`), reader **not** called; (3) `FromTrigger` false → fallback, reader not called; (4) third PDF with cap 2 → fallback, `Withheld == DocumentCountCap`; (5) bytes over the remaining cap → `TurnBytesCap`; (6) reader returns null → `Unreadable`; (7) PDFs do not consume the image caps (an image after two PDFs still goes).
- **Acceptance:** Red with `CS1501` (no overload with `readDocument`).

### Task 1.4.i — `PlanDocumentAsync` [Sonnet]

- **Goal:** Make 1.4.t green per Spec §3.
- **Read first:** same as 1.4.t.
- **Risk:** `logic`.
- **Deliverable:** Add the `readDocument` parameter and a `private static async Task<PlannedDocument> PlanDocumentAsync(...)` shaped like `PlanImageAsync`, with its own `documentCount`/`documentBytes` counters in `PlanAsync`. Fix the one production caller only enough to compile (pass `static (_, _) => Task.FromResult<ReadOnlyMemory<byte>?>(null)`; D1.5 wires the real reader).
- **Acceptance:** 1.4.t green; `PromptBlockPlannerTests` all green; build clean.

### Task 1.5.t — The collector reads a named PDF [Sonnet]

- **Goal:** Pin Spec §3 end to end inside the collector: a PDF named in the trigger Message becomes a blob block.
- **Read first:** `src/Huddle.App/Library/LibraryDocumentCollector.cs` (find the `PlanAsync` call and the image reader it passes); `tests/Huddle.Tests.Library/Library/LibraryDocumentCollectorTests.cs` (one test that delivers an image block).
- **Risk:** `logic`.
- **Deliverable:** `Collect_PdfInTrigger_IsABlobBlock`: write `%PDF-1.4\n%fake\n` to a Library file, name its path in the trigger Message, delivery with `Documents` true; assert one `AgentBlobResourceBlock` whose `Data` equals the file bytes. `Collect_PdfOverMaxImageBytes_UsesDocumentCapNotImageCap` (a 4 MiB PDF still goes under the 10 MiB document cap).
- **Acceptance:** Red: today the collector passes a reader that returns null, so the block is absent.

### Task 1.5.i — Wire the document reader [Sonnet]

- **Goal:** Make 1.5.t green per Spec §3.
- **Read first:** same as 1.5.t; the Library file read the image path uses (grep `ReadImageAsync` in `src/Huddle.App/Library/`).
- **Risk:** `logic`.
- **Deliverable:** A reader that reads the whole file when its length is `<= MaxDocumentBytesPerTurn`, else returns null, through the same Library file service the image reader uses (never `File.ReadAllBytes` on an unchecked path). Pass it to `PlanAsync`.
- **Acceptance:** 1.5.t green; `Huddle.Tests.Library` all green.

### Task 1.6 — PromptBlocks and guide docs [Haiku]

- **Goal:** Record Spec §3 where readers look.
- **Read first:** `docs/Huddle.PromptBlocks-Specifications.md` lines 100–130, 835–850, 953–968, 1000–1010; `docs/Huddle.UserGuide.md` (grep "image" in the Library or attachments section).
- **Risk:** `data`.
- **Deliverable:** PromptBlocks: P9 row → "Blob resource since 2026-11 (AdapterUpgrades §3)"; §3 PDF row and §14.17 V2 PDF entry → built, link the Spec; B.2 → add a line for 0.100.0 (`blob-resources.js`: image, `document`, text). User guide: one sentence that a PDF named in a Message reaches Claude, up to two per Message.
- **Acceptance:** `Check-All.ps1` clean.

### Task 1.7 — Live: a PDF's code word reaches the model (V-6) [Sonnet] **Paid**

- **Goal:** Answer PromptBlocks V-6 and verify Spec §3 against the real adapter.
- **Read first:** `tests/Huddle.Acp.Tests/E2E/RealAdapterNoticeTests.cs`; `tests/Huddle.Acp.Tests/E2E/RealAdapterPromptBlockTests.cs` (the text-resource test, ~line 84).
- **Risk:** `boundary`.
- **Deliverable:** In `RealAdapterPromptBlockTests`, `PdfBlobResource_CodeWord_ReachesTheModel`: build a minimal valid one-page PDF in code whose text is `ZEBRA-4821` (hand-written PDF 1.4 objects with a correct `xref`), send it as an `AgentBlobResourceBlock` with the prompt "What code word is in the attached PDF? Reply with the word only." on model `haiku`. Assert the reply contains `ZEBRA-4821` (`// contains-ok: model reply text, not markup`). Record V-6 as answered in PromptBlocks Appendix C with the date.
- **Acceptance:** Passes with `TEAM_E2E=1`; skipped without.

---

## D2 — Sign-in and start-up failures, named (Spec §4)

### Task 2.1.t — `authRequired` during a prompt is an authentication exception [Sonnet]

- **Goal:** Pin Spec §4: `session/prompt` failing with `-32000` throws `AgentAuthenticationRequiredException` carrying the adapter's message.
- **Read first:** `src/Huddle.Acp/DotAcp/DotAcpAgentSession.cs` lines 225–250; `src/Huddle.Acp/Abstractions/Exceptions/AgentAuthenticationRequiredException.cs`; `tests/Huddle.Acp.Tests/Fakes/FakeRpcError.cs`; one test in `DotAcpAgentSessionTests` whose `OnPrompt` throws a `FakeRpcError`.
- **Risk:** `logic`.
- **Deliverable:** `PromptAsync_AuthRequired_ThrowsAuthenticationRequired`: `OnPrompt = _ => throw new FakeRpcError(-32000, "Run /login to sign in again.")`; assert `AgentAuthenticationRequiredException`, `Message == "Run /login to sign in again."`, `AuthMethods` empty. `PromptAsync_OtherRpcError_StillAgentException` (code `-32603`).
- **Acceptance:** Red: today it is a plain `AgentException`.

### Task 2.1.i — Map it [Sonnet]

- **Goal:** Make 2.1.t green per Spec §4.
- **Read first:** same as 2.1.t.
- **Risk:** `logic`.
- **Deliverable:** Add `public AgentAuthenticationRequiredException(IReadOnlyList<AuthMethodInfo> authMethods, string message)` (guarded). In `PromptAsync` add `catch (RemoteInvocationException ex) when (ex.ErrorCode == (int)dotacp.protocol.ErrorCode.AuthenticationRequired)` **before** the general catch, throwing `new AgentAuthenticationRequiredException([], ex.Message)`.
- **Acceptance:** 2.1.t green; `Huddle.Acp.Tests` all green.

### Task 2.2.t — A Turn that needs sign-in tells the Human how [Sonnet]

- **Goal:** Pin Spec §4's Human copy on a Turn failure.
- **Read first:** `src/Huddle.App/Acp/PersonaRunner.cs` lines 200–245 and 870–900 (health reporting); `src/Huddle.App/Acp/PersonaSupervisor.cs` `DescribeStartFailure` (lines 388–407); `tests/Huddle.Tests.Agents/Acp/PersonaSupervisorHealthTests.cs` (one Turn-failure test).
- **Risk:** `logic`.
- **Deliverable:** `TurnFails_AuthenticationRequired_ReasonSaysSignInAgain`: the fake session throws `new AgentAuthenticationRequiredException([], "Run /login")` from `PromptAsync`; assert the reported health reason equals exactly `Claude needs you to sign in again. Run claude in a terminal and sign in, then send the Message again.`
- **Acceptance:** Red: the reason is the raw exception text.

### Task 2.2.i — Describe it [Sonnet]

- **Goal:** Make 2.2.t green per Spec §4.
- **Read first:** same as 2.2.t.
- **Risk:** `logic`.
- **Deliverable:** Where the Turn failure's reason is built, map `AgentAuthenticationRequiredException` to that sentence, held once as a `const` next to `DescribeStartFailure` and used by both the start path (only when `AuthMethods` is empty) and the Turn path.
- **Acceptance:** 2.2.t green; `PersonaSupervisorHealthTests` and `PersonaRunnerTests` green.

### Task 2.3 — Copy check [Haiku]

- **Goal:** Hold Spec §4's sentence to `language.md`.
- **Read first:** `docs/engineering/language.md` (grep "sign in", "Adapter", "Agent"); the `const` from 2.2.i.
- **Risk:** `data`.
- **Deliverable:** If `language.md` prefers other words (for example "Adapter" over "Claude"), change the `const` and 2.2.t's expected text together and say why in the report. Add a troubleshooting row to `docs/Huddle.UserGuide.md`.
- **Acceptance:** Build and 2.2.t green; `Check-All.ps1` clean.

### Task 2.4 — Pin the start-up failure passthrough [Haiku]

- **Goal:** Pin Spec §4: a start-up failure's own message reaches the Human unchanged.
- **Read first:** `PersonaSupervisor.cs` `DescribeStartFailure` (lines 388–407); `PersonaSupervisorHealthTests` (one start-failure test).
- **Risk:** `data`.
- **Deliverable:** `StartFails_AdapterNamesTheFix_MessagePassesThrough`: factory throws `new AgentException("session/new failed: Invalid proxy URL in HTTPS_PROXY: x. Fix or unset HTTPS_PROXY and restart Claude Code.")`; assert the reason equals that text. It should pass on arrival: prove it with a mutation (make the `AgentException` arm return a constant), then restore.
- **Acceptance:** Test green, and the mutation red is shown in the report.

---

## D3 — Tool names on tool calls (Spec §5)

### Task 3.1.t — The mapper reads the tool name [Haiku]

- **Goal:** Pin Spec §5: `ToolName` comes from `_meta.claudeCode.toolName`.
- **Read first:** `src/Huddle.Acp/DotAcp/SessionUpdateMapper.cs` lines 48–75; `tests/Huddle.Acp.Tests/DotAcp/SessionUpdateMapperTests.cs` (one `ToolCall` test); Conventions row "Reading `_meta` in the mapper".
- **Risk:** `data`.
- **Deliverable:** Tests for both `ToolCall` and `SessionUpdateToolCallUpdate`: `Meta = { ["claudeCode"] = new JObject { ["toolName"] = "Bash" } }` → `ToolName == "Bash"`; no `Meta` → null; `claudeCode` not an object → null; `toolName` a number → null.
- **Acceptance:** Red with `CS1061` (`ToolName`).

### Task 3.1.i — Carry it [Haiku]

- **Goal:** Make 3.1.t green per Spec §5.
- **Read first:** same as 3.1.t; `src/Huddle.Acp/Abstractions/AgentEvent.cs` `ToolCallStarted`, `ToolCallUpdated`.
- **Risk:** `data`.
- **Deliverable:** Append `string? ToolName = null` to both records (with `<param>` docs). Add `private static string? ReadClaudeCodeToolName(Dictionary<string, object>? meta)` to the mapper and pass it in both cases.
- **Acceptance:** 3.1.t green; `Huddle.Acp.Tests` green.

### Task 3.2.t — Memory attribution prefers the tool name [Sonnet]

- **Goal:** Pin Spec §5's fallback rule in `RecordMemoryToolPath`.
- **Read first:** `src/Huddle.App/Acp/Sessions/RoomSession.cs` `RecordMemoryToolPath` (~lines 2395–2425); `tests/Huddle.Tests.Agents/Acp/PersonaRunnerMemoryToolFileChangesTests.cs` (one test that sends a memory tool call).
- **Risk:** `logic`.
- **Deliverable:** `MemoryCall_ToolNameSet_TitleIsDisplayText_StillAttributed` (title `"Remember a fact"`, `ToolName` the memory tool's prefixed name); `MemoryCall_ToolNameOther_TitleLooksLikeMemory_NotAttributed` (title is the memory tool name, `ToolName = "Bash"`); `MemoryCall_NoToolName_TitleFallbackStillWorks`.
- **Acceptance:** Red on the first two.

### Task 3.2.i — Use it [Sonnet]

- **Goal:** Make 3.2.t green per Spec §5.
- **Read first:** same as 3.2.t.
- **Risk:** `logic`.
- **Deliverable:** Pass `ToolName` into `RecordMemoryToolPath` from both call sites in `HandleAgentEventAsync`; test `MemoryTool.IsToolName(toolName ?? title)`.
- **Acceptance:** 3.2.t green; the memory and file-changes runner tests green.

---

## D4 — The model that answered (Spec §6)

### Task 4.1.t — The mapper reads the model [Haiku]

- **Goal:** Pin Spec §6: `UsageUpdated.Model` comes from `_meta["_claude/model"]`.
- **Read first:** `SessionUpdateMapper.cs` (the `UsageUpdate` case, ~line 79); `SessionUpdateMapperTests` (one usage test).
- **Risk:** `data`.
- **Deliverable:** `{"_claude/model": "claude-haiku-5-5"}` → `Model == "claude-haiku-5-5"`; no meta → null; non-string → null.
- **Acceptance:** Red with `CS1061`.

### Task 4.1.i — Carry it [Haiku]

- **Goal:** Make 4.1.t green per Spec §6.
- **Read first:** same; `AgentEvent.cs` `UsageUpdated`.
- **Risk:** `data`.
- **Deliverable:** Append `string? Model = null` to `UsageUpdated`; read it in the mapper.
- **Acceptance:** 4.1.t green.

### Task 4.2.t — The context fill remembers the model [Sonnet]

- **Goal:** Pin Spec §6: the latest non-null model reaches `ContextFillReading`.
- **Read first:** `src/Huddle.App/Services/ContextFillReading.cs`; `src/Huddle.App/Services/ContextFill.cs` (whole, 140 lines); `RoomSession.cs` `CountUsage` (search `private void CountUsage`); `tests/Huddle.Tests.Rooms/Services/ContextFillTests.cs`.
- **Risk:** `logic`.
- **Deliverable:** Runner test: two usage updates, the first with `Model = "m1"`, the second with null → reading's `Model == "m1"`. ContextFill test: a reading with a model round-trips.
- **Acceptance:** Red with `CS1061`.

### Task 4.2.i — Pass it through [Sonnet]

- **Goal:** Make 4.2.t green per Spec §6.
- **Read first:** same as 4.2.t.
- **Risk:** `logic`.
- **Deliverable:** Append `string? Model = null` to `ContextFillReading`; `RoomSession` keeps the last non-null model per session and passes it where it builds the reading.
- **Acceptance:** 4.2.t green; `ContextFillTests` green.

### Task 4.3.t — The tooltip names the model [Sonnet]

- **Goal:** Pin Spec §6's copy in `ContextFillText.Tooltip`.
- **Read first:** `src/Huddle.App/Services/ContextFillText.cs` (36 lines); its tests (grep `ContextFillText.Tooltip` in `tests/`).
- **Risk:** `logic`.
- **Deliverable:** `Tooltip_WithModel_AppendsAnsweredBy`: expected exactly the existing tooltip text plus ` Answered by claude-haiku-5-5.`; `Tooltip_NoModel_Unchanged`; `Label_WithModel_Unchanged`.
- **Acceptance:** Red on the first.

### Task 4.3.i — Write it [Sonnet]

- **Goal:** Make 4.3.t green per Spec §6.
- **Read first:** same as 4.3.t.
- **Risk:** `logic`.
- **Deliverable:** In `Tooltip`, append `string.Create(CultureInfo.InvariantCulture, $" Answered by {reading.Model}.")` when `Model` is not null or blank.
- **Acceptance:** 4.3.t green; `MessageListTests` and the `MembersPanel` tests green.

### Task 4.4 — Copy and wireframes [Haiku]

- **Goal:** Hold Spec §6's copy to `language.md` and record it where the UI is described.
- **Read first:** `docs/engineering/language.md` (grep "model"); `docs/engineering/wireframes.md` (grep "context fill" or `ContextFill`).
- **Risk:** `data`.
- **Deliverable:** Adjust the copy if `language.md` says otherwise (with 4.3.t's expected text in the same change); add the model to the context-fill tooltip's description in `wireframes.md`.
- **Acceptance:** `Check-All.ps1` clean.

### Task 4.5 — Live: usage names the model [Sonnet] **Paid**

- **Goal:** Verify Spec §6 against the real adapter.
- **Read first:** `tests/Huddle.Acp.Tests/E2E/RealAdapterNoticeTests.cs`.
- **Risk:** `boundary`.
- **Deliverable:** `RealAdapterModelTests.Haiku_Usage_NamesTheResolvedModel`: session on `haiku`, one "Reply PONG" prompt; assert some `UsageUpdated.Model` starts with `claude-haiku` (`// contains-ok:`-style comment not needed for `StartsWith` on a model id, but add it if `Check-All` asks).
- **Acceptance:** Passes with `TEAM_E2E=1`.

---

## D5 — Deleting the old session on a fresh start (Spec §7)

### Task 5.1.t — The host reads `CanDelete` [Sonnet]

- **Goal:** Pin Spec §7: `AgentHostInfo.CanDelete` follows `agentCapabilities.sessionCapabilities.delete`.
- **Read first:** `src/Huddle.Acp/Abstractions/AgentHostInfo.cs`; `DotAcpAgentHost.cs` lines 100–115 (where `CanResume` is read); `tests/Huddle.Acp.Tests/DotAcp/DotAcpAgentHostResumeTests.cs` (the test that sets `OnInitialize` to advertise resume).
- **Risk:** `boundary`.
- **Deliverable:** `Start_AdvertisesDelete_CanDeleteTrue` and `Start_NoDelete_CanDeleteFalse`, copying the resume tests' `OnInitialize` shape with `"delete": {}`.
- **Acceptance:** Red with `CS1061`.

### Task 5.1.i — Read it [Sonnet]

- **Goal:** Make 5.1.t green per Spec §7.
- **Read first:** same as 5.1.t.
- **Risk:** `boundary`.
- **Deliverable:** Add `bool CanDelete` to `AgentHostInfo` (trailing, default false if it is a record with positional defaults; otherwise update every construction, grep `new AgentHostInfo(`). Read `response.AgentCapabilities?.SessionCapabilities?.Delete is not null`.
- **Acceptance:** 5.1.t green; `Huddle.Acp.Tests` green.

### Task 5.2.t — `DeleteAsync` sends `session/delete` [Sonnet]

- **Goal:** Pin Spec §7's session behaviour.
- **Read first:** `src/Huddle.Acp/Abstractions/IAgentSession.cs`; `DotAcpAgentSession.cs` (constructor and `CancelAsync`); `tests/Huddle.Acp.Tests/Fakes/FakeAcpAgent.cs` (its `On*` hooks, lines 50–75, and the method dispatch switch); Conventions row **Linked fakes**.
- **Risk:** `boundary`.
- **Deliverable:** Add `internal Func<JsonObject, Task<JsonObject>> OnSessionDelete { get; set; }` to `FakeAcpAgent`, default returning `{}`, routed for method `session/delete`. Tests: `DeleteAsync_CanDelete_SendsSessionDeleteWithTheSessionId`; `DeleteAsync_CannotDelete_SendsNothing`; `DeleteAsync_NotFound_Succeeds` (`OnSessionDelete` throws `FakeRpcError(-32002, "not found")`).
- **Acceptance:** Red with `CS1061` (`DeleteAsync`); the linked-fake change builds in `mock-acp` too.

### Task 5.2.i — Implement `DeleteAsync` [Sonnet]

- **Goal:** Make 5.2.t green per Spec §7.
- **Read first:** same as 5.2.t.
- **Risk:** `boundary`.
- **Deliverable:** `IAgentSession`: `Task DeleteAsync(CancellationToken cancellationToken) => Task.CompletedTask;` with docs. `DotAcpAgentSession`: when the host `CanDelete`, `await connection.DeleteAsync(new DeleteSessionRequest { SessionId = ... }, ct)`; swallow `RemoteInvocationException` with `ErrorCode.ResourceNotFound`, log other failures through a `[LoggerMessage]` and rethrow.
- **Acceptance:** 5.2.t green; `Huddle.Acp.Tests` and the `MockAdapter/` folders of `Huddle.Tests.Agents` and `Huddle.Tests.Personas` green.

### Task 5.3.t — A fresh start deletes the old session, best effort [Sonnet]

- **Goal:** Pin Spec §7's `RoomSession` rule.
- **Read first:** `RoomSession.cs` `RequestFreshStartAsync` and `ApplyFreshStartAsync` (lines 430–565); `tests/Huddle.Tests.Agents/Acp/Sessions/StartAfreshTests.cs` (one test); `FakeAgentSession.cs` (to add a `DeleteCalls` counter).
- **Risk:** `boundary`.
- **Deliverable:** Add `public int DeleteCalls` and `public Exception? DeleteFailure` to `FakeAgentSession` (`DeleteAsync` increments, then throws `DeleteFailure` if set). Tests: `FreshStart_DeletesTheOldSession_AfterClosingIt` (old session's `DeleteCalls == 1`, and it was disposed first); `FreshStart_DeleteThrows_FreshStartStillCompletes_AndLogsWarning`; `Restart_DoesNotDelete` (the Restart path from rules.md).
- **Acceptance:** Red on the first two.

### Task 5.3.i — Call it [Sonnet]

- **Goal:** Make 5.3.t green per Spec §7.
- **Read first:** same as 5.3.t.
- **Risk:** `boundary`.
- **Deliverable:** In the fresh-start teardown, after the old session is closed, `try { await old.DeleteAsync(this.runToken) } catch (Exception ex) when (ex is not OperationCanceledException) { log Warning "Could not delete the old session of {PersonaName} in {RoomId}." }`. Never before close, never awaited by the new session's open.
- **Acceptance:** 5.3.t green; `StartAfreshTests` and the Room Session tests green.

### Task 5.4 — Live: delete really deletes [Sonnet] **Paid**

- **Goal:** Verify Spec §7 against the real adapter.
- **Read first:** `RealAdapterNoticeTests.cs`; `tests/Huddle.Acp.Tests/DotAcp/DotAcpAgentHostResumeTests.cs` (how a resume by id is requested).
- **Risk:** `boundary`.
- **Deliverable:** `RealAdapterDeleteTests.DeletedSession_CannotBeResumed`: start a session, one cheap prompt, `DeleteAsync`, then resume by its id → `AgentSessionNotFoundException`. Assert `host.Info.CanDelete` first.
- **Acceptance:** Passes with `TEAM_E2E=1`.

### Task 5.5 — Long-running Rooms docs [Haiku]

- **Goal:** Record Spec §7 as the answer to LR Q-4.
- **Read first:** `docs/Huddle.LongRunningRooms-Specifications.md` lines 485–495 and 845–855.
- **Risk:** `data`.
- **Deliverable:** Line 489's paragraph: "Since 2026-11 a fresh start deletes the old session (AdapterUpgrades §7)." Q-4 row: answered, link the Spec.
- **Acceptance:** `Check-All.ps1` clean.

---

## D6 — Compaction updates (Spec §8)

### Task 6.1 — Spike: record the compaction wire [Sonnet] **Paid**

- **Goal:** Replace Spec §8's **Assumed** token keys with observed ones.
- **Read first:** `tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/context-compaction.js` and `context-compaction-meta.js` (whole; both small); `RealAdapterNoticeTests.cs`.
- **Risk:** `boundary`.
- **Deliverable:** A throwaway E2E (not committed) with `TraceWire: true` that advertises `session.compaction` (temporarily add `["compaction"] = new Dictionary<string, object>()` to a test-only subclass of `ClientSessionCapabilities`), sends two short prompts then `/compact` on `haiku`, and prints every line whose `sessionUpdate` starts with `compaction`. Write the exact `compaction_update` shapes (statuses seen, `_meta` keys for pre/post tokens and duration) into Spec §8, replacing the **Assumed** sentence.
- **Acceptance:** Spec §8 holds the observed shapes with the date; no product code changed.

### Task 6.2.t — Advertise compaction behind an option [Sonnet]

- **Goal:** Pin Spec §8's capability and option.
- **Read first:** `src/Huddle.Acp/DotAcp/NoticeSessionCapabilities.cs`; `DotAcpHostOptions.cs`; `DotAcpAgentHost.cs` (search `NoticeSessionCapabilities`); `tests/Huddle.Acp.Tests/DotAcp/DotAcpAgentHostNoticeWireTests.cs` (the three capability tests).
- **Risk:** `boundary`.
- **Deliverable:** Tests: compaction only → `"session":{"compaction":{}}`; both → `{"notices":{},"compaction":{}}`; neither → no `session`; default options leave it off.
- **Acceptance:** Red with `CS1061` (`AdvertiseCompaction`).

### Task 6.2.i — `HuddleSessionCapabilities` [Sonnet]

- **Goal:** Make 6.2.t green per Spec §8.
- **Read first:** same as 6.2.t.
- **Risk:** `boundary`.
- **Deliverable:** Rename the class and file to `HuddleSessionCapabilities` with nullable `Notices` and `Compaction` dictionaries, `[JsonProperty(..., NullValueHandling = NullValueHandling.Ignore)]`, set from the two options. Add `bool AdvertiseCompaction = false` to `DotAcpHostOptions` and `AcpOptions.AdvertiseCompaction` (default **false**) wired in `DotAcpAgentHostFactory`. Update the 6.2 notice wire tests that name the old class.
- **Acceptance:** 6.2.t green; notice wire tests green.

### Task 6.3.t — Rename the two compaction kinds [Sonnet]

- **Goal:** Pin Spec §8's stream rename.
- **Read first:** `src/Huddle.Acp/DotAcp/NdjsonMethodRewriteStream.cs` `TryRewriteNotice` and `Emit`; `tests/Huddle.Acp.Tests/DotAcp/NdjsonMethodRewriteStreamTests.cs` (the `Notice_*` tests and `RewriteAsync` helper).
- **Risk:** `boundary`.
- **Deliverable:** With `rewriteCompaction: true`: `compaction_update` → `_session/compaction`, `compaction_summary_chunk` → `_session/compaction_chunk`, each still counted; off → bytes unchanged; a text chunk containing `"compaction_update"` untouched.
- **Acceptance:** Red with `CS1739` (no `rewriteCompaction` parameter).

### Task 6.3.i — Implement the rename [Sonnet]

- **Goal:** Make 6.3.t green per Spec §8.
- **Read first:** same as 6.3.t.
- **Risk:** `boundary`.
- **Deliverable:** Generalise `TryRewriteNotice` into `TryRewriteUpdateKind(body, kind, method)` and call it for the enabled kinds; add the optional constructor parameter `bool rewriteCompaction = false`; pass `options.AdvertiseCompaction` from `DotAcpAgentHost`.
- **Acceptance:** 6.3.t green; every `NdjsonMethodRewriteStreamTests` test green.

### Task 6.4.t — Publish `AgentCompaction` [Sonnet]

- **Goal:** Pin Spec §8's event.
- **Read first:** `src/Huddle.Acp/DotAcp/DotAcpClientAdapter.cs` `ExtNotificationAsync` and `PublishNotice`; `tests/Huddle.Acp.Tests/DotAcp/DotAcpClientAdapterNoticeTests.cs` (whole); Spec §8 as updated by 6.1.
- **Risk:** `boundary`.
- **Deliverable:** Tests in a new `DotAcpClientAdapterCompactionTests`: an update publishes `AgentCompaction` with id, status (`in_progress`/`completed`/`failed`/`cancelled`/unknown → `Unknown`), error and the token figures from the keys 6.1 recorded; a summary chunk publishes nothing; both are marked dispatched; unknown session dropped and dispatched.
- **Acceptance:** Red with `CS0246`.

### Task 6.4.i — Implement it [Sonnet]

- **Goal:** Make 6.4.t green per Spec §8.
- **Read first:** same as 6.4.t; `src/Huddle.Acp/Abstractions/AgentEvent.cs`, `Enums.cs`.
- **Risk:** `boundary`.
- **Deliverable:** `public enum CompactionStatus { InProgress, Completed, Failed, Cancelled, Unknown }`; `public sealed record AgentCompaction(string SessionId, string CompactionId, CompactionStatus Status, string? Error, long? PreTokens, long? PostTokens) : AgentEvent(SessionId);`; handlers for `session/compaction` and `session/compaction_chunk`, each with `ledger.NoteDispatched` in a `finally`.
- **Acceptance:** 6.4.t green; `Huddle.Acp.Tests` green.

### Task 6.5.t — `/compact`'s outcome reads the compaction [Sonnet]

- **Goal:** Pin Spec §8's Command Turn rule against Commands §8.3.
- **Read first:** `docs/Huddle.Commands-Specifications.md` §6.6 and §8.3 (lines 354–400, 503–518); `RoomSession.cs` (grep `preTokens` for where the outcome reads the tool call); `tests/Huddle.Tests.Agents/Acp/PersonaRunnerTests.cs` `CommandRig` tests (grep `compact`).
- **Risk:** `logic`.
- **Deliverable:** `Compact_WithAgentCompaction_OutcomeUsesItsFigures` (no tool call, an `AgentCompaction(Completed, 40000, 9000)`) → the §8.3 text with `40000 → 9000`; `Compact_ToolCallOnly_StillWorks` (today's path).
- **Acceptance:** Red on the first.

### Task 6.5.i — Implement it [Sonnet]

- **Goal:** Make 6.5.t green per Spec §8.
- **Read first:** same as 6.5.t.
- **Risk:** `logic`.
- **Deliverable:** On `AgentCompaction` with figures during a Command Turn, record them on the active Turn exactly where the tool call's `rawOutput` figures are recorded today; the outcome text code is unchanged.
- **Acceptance:** 6.5.t green; Command tests green.

### Task 6.6.t — An automatic compaction is a baseline, not spend [Sonnet]

- **Goal:** Pin Spec §8's ordinary-Turn rule.
- **Read first:** `RoomSession.cs` `CountUsage` and every `usageBaselinePending` reference (grep); traps.md "A compaction is not spend"; `PersonaRunnerTests` `AdapterNotice_*` tests.
- **Risk:** `boundary`.
- **Deliverable:** In an ordinary Turn: usage 50000, then `AgentCompaction(Completed)`, then usage 12000, then 15000 → Budget counts no spend for the drop and 3000 for the rise; a Notice row `Context compacted` (status `Notice`, id `huddle-notice-N`) is written.
- **Acceptance:** Red.

### Task 6.6.i — Implement it [Sonnet]

- **Goal:** Make 6.6.t green per Spec §8.
- **Read first:** same as 6.6.t; `RoomSession.cs` `ShowNoticeAsync`.
- **Risk:** `boundary`.
- **Deliverable:** On `AgentCompaction` with `Completed` outside a Command Turn, set `usageBaselinePending = true` and call `ShowNoticeAsync(new AgentNotice(sessionId, NoticeSeverity.Info, "Context compacted", null), ct)`.
- **Acceptance:** 6.6.t green; usage and Budget tests green.

### Task 6.7 — Live: `/compact` with compaction advertised [Sonnet] **Paid**

- **Goal:** Verify Spec §8 end to end against the real adapter.
- **Read first:** `RealAdapterNoticeTests.cs`; the 6.1 spike notes in Spec §8.
- **Risk:** `boundary`.
- **Deliverable:** `RealAdapterCompactionTests.Compact_WithCompactionAdvertised_PublishesAgentCompaction`: two prompts then `/compact` on `haiku` with `AdvertiseCompaction: true`; assert one `AgentCompaction` with `Completed` and `PostTokens <= PreTokens`, and no "Compact conversation" `ToolCallStarted`.
- **Acceptance:** Passes with `TEAM_E2E=1`.

### Task 6.8 — Turn it on [Sonnet]

- **Goal:** Ship Spec §8 per §10 (default true at this task).
- **Read first:** `src/Huddle.App/Acp/AcpOptions.cs` (`AdvertiseSessionNotices`); `tests/Huddle.Tests.Agents/Conformance/PersonaHostTests.cs` (the capability tests); traps.md "A compaction is not spend"; Commands spec §6.7.
- **Risk:** `boundary`.
- **Deliverable:** Default `AdvertiseCompaction` to `true`. `PersonaHostTests`: the shipped default's `session` equals `{"notices":{},"compaction":{}}`; add an "off" test. Engineering Guide row for `Acp:AdvertiseCompaction`. traps.md: extend "A compaction is not spend" with the automatic case. Commands spec §6.6: figures now come from the compaction update when advertised.
- **Acceptance:** Full suite green; `Check-All.ps1` clean.

---

## D7 — Close

### Task 7.1 — Index and limits [Haiku]

- **Goal:** Make the delivery findable per `CLAUDE.md` "Orientation".
- **Read first:** `docs/Index.md` (the AdapterUpgrades rows); `docs/engineering/known-limits.md` (header).
- **Risk:** `data`.
- **Deliverable:** Set the Index row's status to "Built {date}" and list the manual test pages touched. Add a known-limits entry only for anything a task reported as not done.
- **Acceptance:** `Check-All.ps1` clean (`Check-DocIndex` passes).

### Task 7.2 — Final verification [Haiku]

- **Goal:** Prove Spec §12 on the finished branch.
- **Read first:** this plan's Conventions table.
- **Risk:** `data`.
- **Deliverable:** Run the build, the full suite, `Check-All.ps1`, then (with the Human's approval) `TEAM_E2E=1` for `Agency.Huddle.Acp.Tests.E2E`. Report counts and any failure verbatim; fix nothing.
- **Acceptance:** A report with four results.

### Task 7.3 — Does Haiku 5.5 offer Effort? (OQ-4) [Haiku]

- **Goal:** Answer Spec §11 OQ-4 and correct the user guide.
- **Read first:** `docs/Huddle.UserGuide.md` "Running an Agent on Haiku"; the SDK type `ModelInfo` in `tools/acp/node_modules/@anthropic-ai/claude-agent-sdk/sdk.d.ts` (grep `supportedEffortLevels`).
- **Risk:** `data`.
- **Deliverable:** A throwaway Node script (scratch folder, not committed) that calls `query({...}).supportedModels()` with a prompt that never yields (it spends no tokens) and prints `haiku`'s effort fields. Correct the "**Effort** offers only **Agent default**" row if Haiku 5.5 has levels.
- **Acceptance:** OQ-4 answered in the Spec with the date.

---

## Retrospective log

| After task | Date | Changes made to this plan |
| --- | --- | --- |
| 15 | 2026-11-03 | Run at task 19. The facts file's Core now states the failure seams (`FakeAgentSession.EnqueueFailure`, `FakePersonaHost.FailNextOpenWith`, `FakeAgentHostFactory.FailNextCreateWith`, `FakeAcpAgent.On<Method>`), the `RoomSession.cs` member anchors for tasks 4.2, 5.3 and D6, the stub-marker rule, the `_meta` worked example (`SessionUpdateMapper.ReadClaudeCodeToolName`), `-NewNames` taking missing identifiers, the `au-` red label prefix and `Select-Object -Last 12` on every run. Prompts give Spec line ranges. One agent per `.t`/`.i` pair. Corrections from the Opus review of D1 and D5 settled 1.4's fallback (always the path line), the document reader's shape, `LibraryFileService.ReadDocumentAsync`, `SupportsDeleteSession`, and where 5.3's delete runs. Cost: the manager's own context was 80% of the tokens. A restricted `au-dev` agent type was added for the next session; it cannot load mid-session. |
| 30 | 2026-11-03 | Run at task 32. The facts Core now names the shared-helper namespaces, has an exact `-NewNames` rule (every missing identifier from the first compile, case-sensitive, named arguments included), allows one stub type per file and no project-only builds, gives the idle-eviction anchors, and records that the `CountUsage` baseline already exists (`usageBaselinePending`), so D6 reuses it. It also gives the D6 file anchors. Every prompt lists its frozen paths. A live run found that a top-level primitive in dotacp `_meta` arrives as a plain `string`; the Conventions `_meta` row was corrected and a unit test pins it. Cost: the restricted `au-dev` agent cut the first-call context from 70K to 21K, about 65% less cache read per call. The manager is still the largest cost. |
| 45 | — | — |
