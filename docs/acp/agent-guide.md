# ACP and this codebase: a guide for AI agents

**Read this before doing any work on `src/Team.Acp`, `src/Team.Console` or
`tests/Team.Acp.Tests`.** It exists so you do not spend a session rediscovering
things that are already known. Everything marked **Verified** was established
empirically on this machine, usually by reflecting the real assemblies or by
running against the real Claude Code adapter. Everything marked **Assumed** is
belief, not evidence, and is fair game to re-check.

Written 2026-09-09, after the proof of concept was completed and passing.
Adapter version 0.75.1, `dotacp` 2026.7.19, .NET 10.0.400, Node 24.

Updated 2026-09-10: per-session system prompts (§3.5), the Newtonsoft wire
serialiser (§4.3), and concurrent hosts (§8, where the one-agent-at-a-time gap is
now closed). Also 2026-09-10: which tools actually run where (§2.2) and how to
give the agent a tool it does not already have (§3.6).

---

## 1. Orientation, in sixty seconds

This repository holds **two independently owned projects sharing one build
root.** Getting this wrong wastes time and breaks someone else's build.

| | ACP proof of concept | Team chat surface |
|---|---|---|
| Solution | `Team.slnx` | `Team.sln` |
| Source | `src/Team.Acp`, `src/Team.Console` | `src/Team.App`, `src/Team.Contracts` |
| Tests | `tests/Team.Acp.Tests` | `tests/Team.Tests` |
| Docs | `docs/acp/**`, `docs/adapter-smoke.md`, `README.md` | `docs/AgencyTeam.md`, `docs/agencyteam/**`, `docs/Team-Specifications.md`, `docs/adr/**` |

**Shared, owned by neither:** `Directory.Build.props`, `Directory.Packages.props`,
`global.json`, `.gitignore`. Changing one of these affects both projects. Do not
edit them without checking with whoever owns the other side.

Always build and test with the solution file, never a bare directory:

```
dotnet build E:\Repos\Team\Team.slnx
dotnet test  E:\Repos\Team\Team.slnx
```

Otherwise the other project's state colours your results. If a failure appears
to originate in `Team.App`, `Team.Contracts` or `Team.Tests`, it is not yours.
Report it; do not fix it.

Expect foreign files to change under you mid-task. That is normal here.

---

## 2. What ACP is

The Agent Client Protocol (<https://agentclientprotocol.com>) is to coding
agents roughly what the Language Server Protocol is to language servers: a
standard way for a client (an editor, or in our case a console app) to drive an
agent process.

**Transport.** Newline-delimited JSON-RPC 2.0 over the child process's stdin and
stdout. One JSON object per line. Property names are camelCase; discriminator
*values* are snake_case. **Verified.**

The client spawns the agent as a child process and speaks to it over the pipes.
There is no socket, no port, no daemon.

### 2.1 Methods, client to agent

| Method | Kind | Notes |
|---|---|---|
| `initialize` | request | Capability negotiation. Must be first. |
| `session/new` | request | `cwd` **must be absolute**. Returns a session id. |
| `session/prompt` | request | Returns only when the turn ends. |
| `session/cancel` | notification | Fire and forget. |

### 2.2 Methods, agent to client

| Method | Kind | Notes |
|---|---|---|
| `session/update` | notification | The streaming channel. See below. |
| `session/request_permission` | request | The agent asks before running a tool. |
| `fs/read_text_file` | request | The agent asks *you* to read a file. |
| `fs/write_text_file` | request | The agent asks *you* to write one. |
| `terminal/*` | request | Specified by ACP, **never called by this adapter**. |
| `elicitation/create` | request | Backs the built-in `AskUserQuestion` tool. |
| `elicitation/complete` | notification | Closes an elicitation. |

That table is the complete list of calls the adapter makes into the client.
**Verified** 2026-09-10 by enumerating every `ctx.request` / `ctx.notify` in
`dist/acp-agent.js` (adapter 0.75.1) — the `ClientConnection` class around line
542 has exactly six members and no more.

**Do not read `fs/*` as "the agent routes file access through your process".**
An earlier version of this guide said that, and it appears to be wrong for this
adapter. The SDK query is built with `tools: { type: "preset", preset:
"claude_code" }` and **no filesystem adapter**, so `Read`, `Write`, `Bash`,
`WebSearch` and the rest execute inside the adapter's own Node process against
the real disk and the real network. The adapter does define a bridge
(`readTextFile` → `this.client.readTextFile`, around line 5104) but **nothing in
`dist/` calls it**. Corroborated behaviourally: a live agent performed a web
lookup, and ACP defines no client callback for web access at all, so the
built-in toolset demonstrably runs agent-side.

The consequence is that **advertising a capability does not give the agent a
tool** — at most it offers to *serve* a tool the harness already has. To add a
tool the harness does not have, see §3.6.

**Acted on 2026-09-10:** `IClientFileSystem`, `LocalClientFileSystem` and their
tests were deleted, and `ClientCapabilities.Fs` now reports `ReadTextFile =
false, WriteTextFile = false`. `DotAcpClientAdapter.ReadTextFileAsync` and
`WriteTextFileAsync` throw `NotSupportedException`, exactly as the terminal
methods do. If this inference is ever wrong, the failure is loud and immediate —
the first `Read` of any real session raises a JSON-RPC error rather than
silently misbehaving. To restore it, re-implement against §2.2's table; nothing
else in the codebase depended on it.

For an editor client the `fs/*` direction still matters: it exists so the agent
sees unsaved buffer contents rather than stale disk. For a console with no
buffers it changes nothing even if it does fire.

### 2.3 Session update discriminators

`update.sessionUpdate` carries one of these. **Verified** by reflection against
the real assemblies:

`agent_message_chunk`, `agent_thought_chunk`, `user_message_chunk`,
`tool_call`, `tool_call_update`, `plan`, `usage_update`,
`current_mode_update`, `available_commands_update`, `session_info_update`,
`config_option_update`.

Content blocks are discriminated by `type`: `text`, `image`, `audio`,
`resource_link`, `resource`. **Verified.**

Enum wire values, all **verified**:

- Stop reason: `end_turn`, `max_tokens`, `max_turn_requests`, `refusal`, `cancelled`
- Tool kind: `read`, `edit`, `delete`, `move`, `search`, `execute`, `think`, `fetch`, `switch_mode`, `other`
- Tool call status: `pending`, `in_progress`, `completed`, `failed`
- Permission option kind: `allow_once`, `allow_always`, `reject_once`, `reject_always`
- Plan entry status: `pending`, `in_progress`, `completed`; priority: `high`, `medium`, `low`

### 2.4 The ordering rule that will bite you

Updates must reach your consumer in wire order. The JSON-RPC library may begin
dispatching the next inbound message the moment your handler yields at its first
`await`. So **map and publish synchronously, before any `await`.** In this
codebase `DotAcpClientAdapter.SessionUpdateAsync` is deliberately not `async`
for exactly this reason, and a test fires a hundred chunks without awaiting
between them to guard it. Do not "tidy" that method into an async one.

---

## 3. The Claude Code adapter: hard-won findings

The agent process is the npm package
`@agentclientprotocol/claude-agent-acp` (bin name `claude-agent-acp`), which
wraps the Claude Agent SDK. Requires Node 22 or later.

**Install it locally, not globally.** A global npm install on Windows produces a
`.cmd` shim, and `Process.Start` with `UseShellExecute=false` cannot execute a
shim. We install under `tools/acp/` and launch `node <path>/dist/index.js`
directly. **Verified** — this was a design decision made before the first line
of code and it was correct.

### 3.1 Authentication

**It uses your existing `claude` CLI login. No API key is required.** **Verified**
against a logged-in machine: `session/new` returned a real session and the
adapter volunteered `Claude Max` as the resolved plan. Evidence in
`docs/adapter-smoke.md`.

**The trap:** when already authenticated, `initialize` returns an **empty**
`authMethods` array. The obvious reading is backwards. A non-empty list does not
mean "you must log in". The real signal for needing authentication is a JSON-RPC
error **`-32000`** returned from `session/new`. **Verified.** This is reportedly
the single finding most likely to cost someone a debugging session.

`ANTHROPIC_API_KEY`, if set in the environment, overrides the CLI login.

### 3.2 It sends notifications you did not ask for

Immediately after `initialize`, the adapter emits `_auth/status_update`
notifications carrying the account plan and **email address**. **Verified.**

Two consequences. A client that throws on unrecognised notification methods will
fault on **every real session**, so tolerate unknown notifications. And do not
log those payloads above debug level, because they contain personal data.

### 3.3 Never close stdin early

If you close the adapter's stdin as soon as you have written your request, the
process is torn down before it flushes asynchronous replies. You get the
`initialize` response and then silence. **Verified** the hard way: a naive shell
pipe loses the `session/new` reply entirely. Keep the streams open for the
lifetime of the connection.

### 3.4 Was blamed on the adapter; it was our serialiser

Until 2026-10-01 this section said that at the moment `session/request_permission`
fires, the tool call's `rawInput` arrives with **empty arrays** for its fields, e.g.
`{"file_path":[],"content":[]}`, and put that down to the adapter. **That was wrong.**
The adapter sends the real values (a wire trace of `claude-agent-acp` 0.75.1 shows
`"rawInput": {"file_path": "...", "content": "gamma"}` on the request). dotacp reads
`rawInput` with Newtonsoft, so it arrives as a `JObject`, and `SessionUpdateMapper.SerializeRaw`
then wrote it with `System.Text.Json`, which walks a `JToken` as a sequence of its children
and emits `[]` for every value. `SerializeRaw` now writes a `JToken` with Newtonsoft.

One consequence mattered: the app's `~/.claude` write guard reads `file_path` from this
string, never found a string value, and approved every write. See the manual-test
tracker's WORKMODE-08 row.

### 3.5 You can give a session a system prompt, through `_meta`

The ACP spec has no `systemPrompt` on `session/new`; the params are `cwd`,
`mcpServers` and `additionalDirectories`. But the spec carries a `_meta` escape
hatch, and the adapter reads a system prompt out of it. **Verified** by reading
`dist/acp-agent.js` (adapter 0.75.1, around line 5838):

```js
let systemPrompt = { type: "preset", preset: "claude_code" };
if (params._meta?.systemPrompt) {
    const customPrompt = params._meta.systemPrompt;
    if (typeof customPrompt === "string") {
        systemPrompt = customPrompt;
    } else if (typeof customPrompt === "object" && customPrompt !== null && !Array.isArray(customPrompt)) {
        systemPrompt = { ...customPrompt, type: "preset", preset: "claude_code" };
    }
}
```

**The JSON type of `_meta.systemPrompt` selects the behaviour**, and the two
behaviours are very different:

| Wire shape | Effect |
|---|---|
| a JSON **string** | **Replaces** the Claude Code preset outright |
| a JSON **object**, e.g. `{"append":"..."}` | Keeps the preset and appends. `type` and `preset` are spread *after* your keys, so you cannot escape the preset this way |
| absent | The default `{type:"preset",preset:"claude_code"}` |

Replacement is the sharp edge: it discards the harness instructions the built-in
tools rely on, so a replaced prompt that still expects `Read` or `Bash` to behave
well is asking for trouble. Append is the right default for a persona that still
does work; replacement suits a persona that is purely conversational.

Do not send `type` or `preset` in the object yourself. The adapter overwrites
both, so including them states a control you do not have.

**In this codebase.** `AgentSessionOptions` takes an optional third argument:

```csharp
new AgentSessionOptions(cwd, permissionHandler,
    new SystemPromptOptions("You are the Chief of Staff.", SystemPromptMode.Append));
```

`SystemPromptMode.Append` is the default. `DotAcpAgentHost.StartSessionAsync` is
the only place that knows the wire shape — it maps `Append` to
`{"append": text}` and `Replace` to a bare string, and leaves `Meta` entirely
unassigned when no prompt is given, so the default path is byte-for-byte what it
always was. That last part is guarded by a test; keep it that way, because an
empty `_meta` dictionary is a behaviour change you would not notice.

`_meta` is **adapter-specific, not protocol**. Another ACP agent will ignore it
silently — no error, just a persona that never took. Treat the feature as
best-effort and do not build a guarantee on it.

Prompts are fixed at `session/new`, not per turn. Changing persona means a new
session, which is what `/new` already does.

---

### 3.6 Giving the agent a tool it does not already have

The built-in tool names belong to the harness, and the client-implemented set
(§2.2) is closed. There is no "register a tool" call in ACP. So a tool **we**
name has exactly one delivery route: an MCP server listed in `session/new`.

The adapter advertises `mcpCapabilities: { http: true, sse: true }` and maps the
`mcpServers` array like this (around line 5812):

```js
if ("type" in server && (server.type === "http" || server.type === "sse")) { ... }
else if (!("type" in server)) { ...stdio... }
// no branch for "acp"
```

Three things follow, all **Verified** 2026-09-10.

- **`type: "acp"` is silently dropped.** The unstable spec defines an ACP
  transport that would tunnel MCP over the stdio channel we already have — no
  socket at all — and `dotacp.protocol.unstable` has the types, so it is
  temptingly reachable from C#. This adapter has no branch for it. No error; the
  tool simply never appears. Do not use it.
- **`type` must be on the wire or the entry is ignored.** `dotacp`'s
  `McpServerHttp.Type` is a **read-only** property already returning `"http"`,
  and Newtonsoft does serialise it — confirmed by serialising a real
  `NewSessionRequest`. Nothing to assign, but it is load-bearing, so
  `DotAcpAgentHostToolServerTests` asserts the literal `"type": "http"` on the
  wire. If that test ever goes red, the whole feature is off and nothing else
  will tell you.
- **MCP tools are permission-gated normally.** They reach the model as
  `mcp__<serverName>__<toolName>` and flow through `session/request_permission`
  like any built-in, so the existing `IPermissionHandler` covers them.

`AppToolServer` (`src/Team.Acp/Tools`) is the proof of concept: Kestrel on
`http://127.0.0.1:0`, one `POST /mcp` route speaking `initialize`, `tools/list`
and `tools/call`. **HTTP here is loopback IPC, not a web server.** The transport
exists only because the agent is a separate process; the tool body runs in ours,
which is the entire point — a stdio MCP server would put the tool back in a
process that cannot see application state. A request with no `id` is a
notification (`notifications/initialized`) and must get an empty 202, never a
result.

The fake tools (`src/Team.Console/Tools`) are `list_chatrooms` and
`create_chatroom`, backed by an in-memory `ChatRoomRegistry`. Nothing should be
built on top of them.

**Choosing a probe tool is harder than it looks.** The first attempt was
`record_note`, and it was a bad probe: "record a note" overlaps with the model's
own memory behaviour, so a model can satisfy the request *without* calling our
tool and the test proves nothing. Chatrooms have no built-in analogue in the
harness, which is the whole point — if the agent reports a chatroom, that
information can only have come from our process.

The registry is seeded with one hardcoded room, **`bananas`**. It is a canary: it
exists before any agent runs and is not guessable, so a model that names it must
have called `list_chatrooms`. `create_chatroom` returns the resulting room count
for the same reason — the count lives in our process state and cannot be
inferred. Tests assert against the `ChatRoomRegistry` instance itself rather than
the HTTP response text, because only the registry proves our code ran.

**Registering a tool is not the same as the model finding it.** This cost four rounds
of debugging on 2026-09-10, so it is worth stating plainly. On an account with several
MCP servers connected, the session runs in **deferred-tool mode**: tool names are listed
up front but schemas load on demand, and the model resolves a tool through a name lookup
rather than scanning the roster. A prompt naming the tool imprecisely — `list_chatrooms`
rather than `mcp__team__list_chatrooms` — misses, and the model then reports, correctly
and confusingly, that no such tool exists.

Worse, the model's keyword fallback returns *semantically adjacent* tools. Asked for
chat rooms it surfaced `mcp__claude_ai_ms365__teams_list_chats`, which looks plausible
and is a different product entirely. A negative result from that lookup is evidence
about the **search index**, not about the tool schema — do not read it as proof the tool
is unregistered, as this guide's author did, twice.

The fix is to name the tools in the session's system prompt (§3.5):

```
The Team application exposes these tools, which run inside the application process:
mcp__team__list_chatrooms, mcp__team__create_chatroom. When asked about chat rooms,
call them. Never answer from the codebase.
```

**Verified 2026-09-10:** the identical prompt against the identical running server failed
without that text and succeeded with it, returning the canary room `bananas`. The gated
E2E test appends the same thing, because otherwise whether it passes depends on the tool
roster of whoever runs it.

How to tell the two failures apart, since they have opposite fixes: `AppToolServer` logs
every method it receives at `Debug`. If `initialize` and `tools/list` arrive, the harness
connected and the tool is registered — any remaining problem is discovery or prompting.
If nothing arrives, it is a wiring fault. Run the console with
`Logging__LogLevel__Team=Debug` and `/exit` immediately: `initialize` and `session/new`
complete before any model call, so this diagnosis costs no tokens.

Note that `IAppTool.Description` is **model-facing** — it is transmitted in
`tools/list` and is the only thing telling the model what the tool is for. Do not
write "used to prove tool invocation" or similar in one: a model that is told a
tool is a diagnostic will sensibly decline to use it for a real request.

---

## 4. The `dotacp` C# library

Packages `dotacp.client` and `dotacp.protocol`, both `2026.7.19`, Apache-2.0,
single maintainer. It sits on `StreamJsonRpc`.

**Use the `dotacp.protocol` and `dotacp.client` namespaces. Never the
`.unstable` variants.** Both exist and the unstable ones will compile.

### 4.1 The StreamJsonRpc pin is load-bearing

`dotacp.client` declares `StreamJsonRpc >= 2.7.76`, which drags in
`MessagePack 2.2.85`, which has published CVEs. With `TreatWarningsAsErrors`
that is 36 build errors. **Fix, verified:** pin `StreamJsonRpc` to `2.25.29` in
`Directory.Packages.props` **and** add a direct `PackageReference` in
`Team.Acp.csproj`. A central version pin alone does **not** repin a
transitive-only package. `MessagePack` then resolves to 2.5.302 and the audit is
clean. `dotacp.client`, compiled against StreamJsonRpc 2.7, binds and runs
correctly against 2.25. **Verified** by loading and invoking it.

Do not "simplify" this by removing the direct reference.

### 4.2 API surface you will actually use

```csharp
Connection? Connection.RunClient(IAcpClient client, Stream agentStdin,
                                 Stream agentStdout, TraceSource? trace);
```

`connection.Completion` completes when the channel closes. `Connection` is
`IDisposable`. Outbound: `InitializeAsync`, `NewSessionAsync`, `PromptAsync`,
`CancelAsync`, and more we do not use.

`IAcpClient` is what you implement: `SessionUpdateAsync`,
`RequestPermissionAsync`, `ReadTextFileAsync`, `WriteTextFileAsync`, five
`*TerminalAsync` methods, `ExtMethodAsync`, `ExtNotificationAsync`,
`OnDisconnected`.

Throwing `NotSupportedException` from an `IAcpClient` method is the correct way
to decline a capability; the library converts it to a JSON-RPC error.

### 4.3 Traps in the type system

**Three near-identical tool-call types exist.** All carry the same fields, so
mixing them up compiles and then fails confusingly. **Verified:**

| Type | What it is |
|---|---|
| `ToolCall` | the `tool_call` session update |
| `SessionUpdateToolCallUpdate` | the `tool_call_update` session update |
| `ToolCallUpdate` | standalone; this is what `RequestPermissionRequest.ToolCall` is |

**Type aliases.** `SessionId`, `ToolCallId`, `PermissionOptionId`,
`SessionModeId`, `AuthMethodId` are structs with implicit conversions both ways
with `string`. Cast to `string` before putting them in your own types.

**`ProtocolMeta.Version` is a `ushort`.** `InitializeResponse.ProtocolVersion`
needs a double cast: `(int)(ushort)response.ProtocolVersion`. **Verified.**

**`RawInput` / `RawOutput` are `object`** holding a Newtonsoft `JToken` (a `JObject` for
an object) when read off the wire. A hand-built test value may be a `JsonElement`.
`SessionUpdateMapper.SerializeRaw` handles both. **Do not hand a `JToken` to
`System.Text.Json`:** it serialises as its children, so `{"file_path":"x"}` becomes
`{"file_path":[]}`.

**The wire serialiser is Newtonsoft, not `System.Text.Json`.** The protocol types
carry `[Newtonsoft.Json.JsonProperty]`; `NewSessionRequest.Meta`, for instance, is
a `Dictionary<string, object>` mapped to `_meta`. **Verified** by reflection. So
anything you put in an `object`-typed protocol field must be something Newtonsoft
will serialise sensibly — plain dictionaries, strings and primitives. Handing it a
`JsonElement` or a `JsonNode` there will not do what you expect. The tests read
the wire back with `System.Text.Json.Nodes`, which is a separate concern and fine.

**`ReadTextFileRequest.Line` / `.Limit` are `uint?`**, not `int?`.

**Errors** surface as `StreamJsonRpc.RemoteInvocationException` with an
`ErrorCode`; a dropped connection as `ConnectionLostException`.
`dotacp.protocol.ErrorCode.AuthenticationRequired` is `-32000`.

---

## 5. This codebase: the invariants

Layering, and the reason for it: **no `dotacp` or `StreamJsonRpc` type may
appear outside `Team.Acp.DotAcp`.** `Team.Acp.Abstractions` defines our own
event, permission and session types. That isolation is what makes the protocol
library swappable and what lets the console and any future platform depend on
`IAgentHost` / `IAgentSession` instead of a third-party package.

Invariants that tests actively guard. Breaking one of these will fail the suite,
but more importantly each exists because the natural implementation is wrong:

- **`SessionUpdateAsync` is not `async`.** See §2.4.
- **`CancelAsync` must not cancel the token passed to the underlying prompt
  call.** Cancel the *linked* per-prompt source instead. The agent has to be
  allowed to finish its turn and report `cancelled`; aborting the round-trip
  throws that answer away. Because the source is linked, cancelling it does not
  propagate to the caller's token.
- **The permission handler must never let an exception escape.** It runs
  mid-turn; throwing fails the whole turn instead of declining one tool.
- **`OnDisconnected` must not dereference its `Connection` argument.** It is
  legitimately called with nothing there.
- **Discard buffered input only when stdin is an interactive terminal.**
  Discarding protects against a stray keystroke approving a file write. But with
  piped input the OS delivers every line at once, so the legitimate answer is
  already queued and gets eaten, making the feature impossible to script or
  verify. Gate on `System.Console.IsInputRedirected`.
- **`session/new` sends no `_meta` at all when no system prompt is set.** Not an
  empty dictionary — unassigned. A test asserts the default wire is unchanged, so
  that adding the persona feature could not quietly alter every existing session.
- **`session/new` sends an empty `mcpServers` array when no tool server is
  configured**, for the same reason, and `DotAcpAgentHost` is the only file that
  may know the `McpServerHttp` wire shape. `IAppTool` and `ToolServerEndpoint`
  live in `Team.Acp.Abstractions` and know nothing about MCP or `_meta`; keep it
  that way, or the layering rule above is broken for a proof of concept.

---

## 6. Build and test configuration

These will cost you a build if you do not know them.

- **`TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are on.** Style
  violations and analyser findings are build failures. `.editorconfig` at
  `E:\Repos\.editorconfig` governs: 4 spaces, CRLF, file-scoped namespaces,
  Allman braces, braces always, `this.` qualification, no expression-bodied
  methods, `Program.Main` rather than top-level statements.
- **Logging in `src/**` must use `[LoggerMessage]` source-generated methods.**
  Direct `logger.LogWarning(...)` calls fail the build via CA1848.
- **`global.json` sets the Microsoft.Testing.Platform runner.** .NET 10 dropped
  VSTest-mode support for `xunit.v3`. Without that node, `dotnet test` fails
  outright.
- **Keep `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio`.** This was
  challenged and checked against xunit's own documentation, which recommends
  retaining both for backward compatibility. Removing them is not an improvement.
- **Analyser suppressions:** stylistic and performance findings (`CA1707`,
  `CA1861`) may be suppressed in the test project with a reason. Anything
  protecting test correctness or cancellation (`xUnit1051`, `xUnit1069`,
  `xUnit1030`, assertion misuse) must be **fixed, never suppressed**. Nothing in
  `src/**` may be suppressed. A `GlobalSuppressions.cs` was added once and
  deliberately removed; do not reintroduce it.
- **A public test member cannot expose an internal type.** xunit requires
  `[Fact]`/`[Theory]` and `[MemberData]` members to be public, and C# forbids
  such a member naming an internal type. `InternalsVisibleTo` does not relax
  this. Parameterise theories over `string` or a public enum and dispatch inside
  the body.
- **Timeouts belong on async tests only.** A `Timeout` on a synchronous test can
  never fire, because xunit cannot interrupt a synchronous body. Async tests keep
  the timeout and pass `TestContext.Current.CancellationToken`.
- **Pin the host content root** to `AppContext.BaseDirectory`. The bare
  `Host.CreateApplicationBuilder(args)` anchors to the current directory, so
  `appsettings.json` is missed whenever the app is launched from anywhere else.

---

## 7. Running and verifying

```
pwsh tools/acp/install.ps1                                   # once
dotnet run --project src/Team.Console -- --cwd C:\temp\sandbox
```

`--cwd` sets the directory the agent operates in; it is the blast radius, so
point it at a throwaway folder. `--auto-approve` skips the permission prompt.
`--trace-wire` logs raw protocol traffic. All logging goes to stderr, so
`2> team.log` captures diagnostics without corrupting the conversation.

`--tools` exposes the app's own `list_chatrooms` and `create_chatroom` tools to
the agent (§3.6) and prints the loopback URL it bound. To check it by hand, ask
`What chat rooms exist? List them.` — a reply naming `bananas` can only have come
through the tool. Note that `--tools` is stripped from `args`
before `ParseArguments` runs, because that method's return shape is pinned by
existing tests — so unlike every other flag it is not positionally validated.
Straighten that out if this graduates past a proof of concept.

To give the session a persona (see §3.5):

```
--system-prompt "You are the Chief of Staff."
--system-prompt-file C:\personas\coo.md
--system-prompt-replace
```

`--system-prompt` and `--system-prompt-file` are mutually exclusive, and
`--system-prompt-replace` on its own is a usage error rather than a no-op. Prefer
the file form for anything longer than a sentence. Replace mode drops the Claude
Code preset, so reach for it deliberately.

Commands in the app: `/exit`, `/new`, `/help`. Ctrl+C cancels the current turn
without ending the session.

```
dotnet test Team.slnx                    # fast, offline
$env:TEAM_E2E="1"; dotnet test Team.slnx # adds the real-agent tests
```

**`dotnet test` currently reports "Zero tests ran" on this repo** while the same
build runs fine directly. Observed 2026-09-10; cause not diagnosed. Until it is,
run the test executable:

```
dotnet build Team.slnx
./tests/Team.Acp.Tests/bin/Debug/net10.0/Team.Acp.Tests.exe
./tests/Team.Acp.Tests/bin/Debug/net10.0/Team.Acp.Tests.exe -filter "/*/*/ClassName/*"
```

The filter flag is a **single dash** and takes xunit's query-filter language
(`/assembly/namespace/class/method`); `--filter`, `--filter-method` and
`--filter-query` are all rejected. Baseline as of 2026-09-10: **211 total, 0
failed, 9 skipped.**

**The opt-in tests spend real model tokens on the user's subscription.** Do not
run them casually, do not run them in a loop, and never set `ANTHROPIC_API_KEY`
or run `claude login` on someone's machine without being asked.

---

## 7.5 Path to the real application

The proof of concept runs **one tool server per agent host**, on its own ephemeral loopback
port, accepting anonymous requests. That is fine for a console with one agent and wrong for
the Blazor app, where several personas share one process. What changes, and what does not:

**What does not change: there is no cross-process marshalling.** Because the tools are hosted
over HTTP *in our own process* rather than as a stdio MCP server, an `IAppTool` can take the
application's real services by constructor injection and call the same domain code the UI
calls. Nothing is serialised, and there is no second process to keep alive.

**What does change: a tool call arrives outside any circuit.** The request lands on a Kestrel
request thread with no Blazor circuit, no signed-in user, and no circuit-scoped services.
Three consequences:

- `StateHasChanged` from that thread does nothing. UI updates must go through a singleton the
  components subscribe to, with each component marshalling via `InvokeAsync(StateHasChanged)`
  onto its own circuit's dispatcher.
- There is no `AuthenticationStateProvider`. A tool call is anonymous unless you make it
  otherwise — see the token below.
- Several agents can call concurrently, on threadpool threads unrelated to any circuit.
  `ChatRoomRegistry` uses a `Lock` for exactly this reason; in the real application that
  becomes ordinary persistence and transactions.

**Identity: the per-session token.** ACP's `McpServerHttp` carries a `headers` array, and
`ToolServerEndpoint` now takes headers that `DotAcpAgentHost` maps onto it. `AppToolServer`
accepts an optional `authToken`; when set it publishes `Authorization: Bearer <token>` on the
endpoint and rejects any request without a matching header with **401, before parsing the
body**. The console mints a fresh token per run with `RandomNumberGenerator`. **Verified
2026-09-10** on the wire.

That token is what makes one shared endpoint possible: the Blazor app should expose **one**
MCP route on its existing Kestrel host and resolve the token into "which agent, which
persona, which user", rather than running a listener per agent instance. It also means
`AppToolServer` collapses into a minimal-API endpoint there, and the
`Microsoft.AspNetCore.App` framework reference stops being an oddity in a class library.

The feature is optional in the same way the system prompt is: with no token configured the
behaviour and the wire are byte-for-byte what they were, and tests pin that.

**Caveat:** `--trace-wire` dumps the raw `session/new` payload, so the bearer token appears in
those logs. The tool server itself never logs it. Treat wire traces as sensitive generally —
they already carry prompts and file contents.

---

## 8. What we do not know

Honest gaps, so you do not mistake absence of evidence for evidence of absence.

- **Ctrl+C in a live terminal is unproven end to end.** Session-level cancel is
  verified against real Claude, and the console's interrupt logic is unit-tested,
  but no automated test presses Ctrl+C at a real keyboard. `CreateNoWindow=true`
  is what detaches the adapter so the signal does not kill it; if that turns out
  to be insufficient, the fallback is `PosixSignalRegistration`.
- ~~**Only one agent, one session at a time** has been exercised.~~ **Closed
  2026-09-10.** Two concurrent hosts — two real `node` adapter processes, each
  with its own session and its own persona — were run against live Claude and
  kept their personas, their session ids and their event streams entirely
  separate; disposing one left the other answering normally. See
  `RealAdapter_TwoConcurrentHosts_KeepSeparatePersonasAndSessions`. Five
  fake-backed tests in `DotAcpConcurrentHostTests` cover the same shapes for
  free, including the case where both agents hand back the *same* session id.
  Caveat: the real run has been done **once**, at two hosts. Not a soak test, and
  nothing above two is known.
- ~~**No MCP servers** are passed to the agent.~~ **Closed 2026-09-10.** A
  session may now carry one app tool server (§3.6); with none configured
  `session/new` still sends an empty list, and a test pins that. A live model
  called `mcp__team__list_chatrooms` and returned the canary room `bananas`,
  which exists only in this process's memory — so the full loop is **Verified**
  end to end, by hand. The automated `RealAdapter_AppTool_IsCalledByLiveModel`
  covering the same path is written, `TEAM_E2E`-gated, and still **unrun**.
- **Our MCP server is correct by an independent check.** The reference
  `@modelcontextprotocol/sdk` client connects to it and lists both tools. When
  our server and the harness disagree, arbitrate with that client rather than
  reasoning about either — it costs no tokens and settled a question four rounds
  of inspection could not.
- **One tool server, one agent, no concurrency.** Nothing is known about several
  agents calling one shared endpoint at once, which is the shape §7.5 proposes.
  The per-session token is verified on the wire but has never had to
  *distinguish* two callers.
- **Terminal capability is declined** — and it would not matter if we
  implemented it. This adapter never calls `terminal/*` at all (§2.2), so `Bash`
  runs agent-side regardless.
- **The client filesystem was removed on inference, not measurement.** No call
  site exists for `client.readTextFile` in the adapter and built-ins
  demonstrably run agent-side (§2.2), so `IClientFileSystem` and its
  implementation were deleted. But the spy test that could have settled it
  empirically was deleted with them, so this remains reasoning rather than
  evidence. If a real session ever fails on `Read` with a JSON-RPC error, this
  is the first thing to suspect.
- **Only one app tool, one tool server** has been exercised. Nothing is known
  about several servers on one session, or a tool that is slow, streams, or
  blocks.
- **`loadSession` is advertised by the adapter** but we never call it.
- **Long conversations are untested.** The longest real turn we have run is a few
  hundred tokens.
- **`Replace` personas are confirmed against a live model; `Append` is not.** The
  `_meta` mapping in §3.5 is proven byte-for-byte against the fake agent for both
  modes. Beyond that, the two-host E2E run on 2026-09-10 had a real Claude adopt a
  `Replace` persona and answer in character, so that path is **Verified** end to
  end. `Append` has only ever been checked on the wire —
  `RealAdapter_ReplaceSystemPrompt_ForcesExactBananaReply` also covers `Replace`,
  and no test yet asks a live model to honour an appended persona. Given the
  adapter locks `type`/`preset` around your object, append is the lower-risk path
  of the two; it is simply not evidenced yet.
- **`_meta.claudeCode.options` is deliberately not wired up.** The adapter spreads
  that object straight into the Agent SDK options, *after* `systemPrompt`, so it
  can set `tools`, `disallowedTools`, `hooks`, `env`, `settings` and more —
  per-persona tool restriction would live there. It is a much larger surface with
  no tests behind it, so we pass nothing. Know that the door exists.
- **`settingSources` is hardcoded** by the adapter to `["user", "project",
  "local"]`, so the session's `cwd` contributes its `CLAUDE.md` and
  `.claude/settings.json` whether we intend it or not. That is a second, implicit
  way a persona can arrive; if a session behaves oddly, look at the `cwd` before
  blaming the system prompt.

---

## 9. Where to look next

| You want | Read |
|---|---|
| How to run it | `README.md` |
| Authentication evidence | `docs/adapter-smoke.md` |
| The chat project's language | `docs/agencyteam/language.md` |
| The chat project's design | `docs/Team-Specifications.md` |
| Protocol reference | <https://agentclientprotocol.com> |
| Adapter source | `tools/acp/node_modules/@agentclientprotocol/claude-agent-acp` |

If you add an architecture decision record for the ACP work, put it in
`docs/acp/adr/`. The numbering in `docs/adr/` belongs to the chat project.
