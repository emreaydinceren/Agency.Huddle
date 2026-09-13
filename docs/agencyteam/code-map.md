# Code map

Every file in `src/` that carries a responsibility worth knowing, and what that
responsibility is. Read it when you need to find where something lives; read
[Runtime architecture](architecture.md) when you need to know how it works.

Several entries below point at [Rules](rules.md) or [Traps](traps.md). Those
pointers are not optional reading if you are about to edit that file. Back to
the hub: [AgencyTeam.md](../AgencyTeam.md).

## Where things live

`src/Huddle.App` — the chat surface:

| Path | Responsibility |
| --- | --- |
| `Services/ChatService.cs` | The only thing that posts a Message. Per-Room semaphore. `/invite` lives here. |
| `Services/RoomEvents.cs` | Singleton pub/sub. A handler that throws is logged and skipped, never propagated. |
| `Services/MentionParser.cs` | Finds Mentions. Resolves `@name` against the Room's Members, longest handle first — **not** by pattern. Candidates are every Member's Name plus every Persona Alias whose owner is in the Room; a Name beats an equal-length Alias. See [Rules](rules.md). |
| `Services/MentionAlias.cs`, `IMentionAliasSource.cs` | Supplies the Aliases in force, so `ChatService` and the invite tools need no dependency on `PersonaStore`. Implemented by `PersonaStore`, registered to the same singleton instance. |
| `Services/MarkdownRenderer.cs` | Markdig. **Never call `UseAdvancedExtensions()`**. |
| `Data/SqliteTeamDirectory.cs` | Humans, Agents, Rooms, Members. `ITeamDirectory` is the interface. |
| `Data/FileChatStore.cs` | One JSONL Transcript per Room under `App_Data/rooms/`. |
| `Data/PersonaModelStore.cs` | One row per Persona: its chosen Model. A separate table in the same `team.db`, **not** a column on `users` — see [Rules](rules.md). Synchronous, because its only caller is. |
| `Data/PersonaEffortStore.cs` | One row per Persona: its chosen Effort. A separate table again, for the same reason `PersonaModelStore` is — see [Rules](rules.md). Synchronous, same reason. |
| `Pipes/PipeServer.cs` | Accepts pipe connections. Hosted service. |
| `Pipes/AgentGateway.cs` | Bridges `RoomEvents` to connected Agents. Fills `Members` on every `messagePosted`. |
| `Components/Pages/Chat.razor` | The Room view. |
| `Components/Pages/Teammates.razor` | The Persona library UI, at `/teammates`. Teammate tiles grouped by Team with a Team filter, the rejected-file list above them; owns the open card's state. |
| `Components/Pages/TeammateGrouping.cs` | The grouping and filtering behind that page, as a pure function — extracted because `HtmlRenderer` cannot simulate choosing a `<select>` option, so inline it would have been untestable. |
| `Components/Shared/TeammateCard.razor` | One Teammate's details, opened over the page. Viewing, editing and creating are three modes of this one card. |
| `Components/Shared/InviteTeammate.razor` | **Add teammate** on the Room header. Offers only Agents that are not already Members; calls the same `InviteAsync` the `/invite` command and the App Tool do. |
| `Demo/DemoAgentHost.cs` | The echo agents. Its connect-retry loop was the model for `PersonaRunner`. |

`src/Huddle.App/Acp` — the join:

| Path | Responsibility |
| --- | --- |
| `ReplyGate.cs` | The reply rule, entire. `memberCount <= 2` or `mentioned`. A pure function, so the rule is provable without a pipe or an agent. |
| `Persona.cs`, `PersonaStore.cs` | The Persona library. One `.md` per Persona, anywhere under `{DataDir}/{Acp:TeamsDir}`. Holds an immutable `PersonaIndex`, swapped as one reference and rebuilt before `PersonasChanged` is raised. A recursive, debounced (500 ms) `FileSystemWatcher` drives it; see [Traps](traps.md) for the four ways that watcher fails silently. |
| `PersonaIndex.cs`, `PersonaEntry.cs`, `RejectedPersonaFile.cs` | The pure parse/validate/collision engine, built from `(path, text)` pairs with no I/O — so the write path validates a candidate edit through the very same code the read path uses. |
| `PersonaIdentity.cs` | The four structural frontmatter fields: `Name`, `Title`, `Alias` (all required) and `Teams` (optional). |
| `PersonaFrontmatter.cs`, `PersonaFrontmatterField.cs` | Parses a Persona's leading YAML frontmatter into ordered fields, reads the structural identity out of it, composes the job description `list_agents` shows (holding `name` back, since the bullet above already prints it), and composes the frontmatter `Add` writes. Ported from a sibling repo, not referenced across it — see [Decision record](decisions.md). |
| `PersonaSupervisor.cs` | The only hosted service here. Returns immediately when `Acp:Enabled` is false. One runner per Persona; diffs on change to start, stop or restart. |
| `PersonaRunner.cs` | **The join.** Pipe client on one side, ACP session on the other. Not a `BackgroundService` — see [Traps](traps.md). |
| `IAgentHostFactory.cs` | The test seam. A fake here is why the suite spends zero tokens. |
| `DotAcpAgentHostFactory.cs` | The real one: work dir, bearer token, one `AppToolServer` per Persona, then the agent process. |
| `SystemPromptComposer.cs` | A canned orientation naming `mcp__team__get_help`, then Persona text, then a fixed block naming every tool **with its `mcp__team__` prefix**. |
| `AdapterLocator.cs` | Finds `node_modules/.../claude-agent-acp/dist/index.js`. Returns null rather than throwing — a missing install must never break `dotnet run`. |
| `IModelCatalog.cs`, `ModelCatalogProbe.cs` | The model list offered by the picker, **and** a per-model effort list. ACP has no `models/list`, so this spawns a throwaway adapter, does `initialize` → `session/new` **with the requested model selected**, reads both catalogs off that one session and disposes. **No prompt turn, so no tokens.** Returns an empty list rather than throwing when nothing is installed; `WithoutAdapterDefault` drops the adapter's `"default"` sentinel from the effort list here, at the app layer. |
| `Tools/GetHelpTool.cs` | How this application works, then the whole tool catalog. Built from the other tools, so a tool added to the factory documents itself. |
| `Tools/ListAgentsTool.cs` | Who exists, who is online, and each one's job description composed by `PersonaFrontmatter`. |
| `Tools/CreateRoomTool.cs` | Takes `agents[]` and no name — a Room is named after its Agents. |
| `Tools/InviteAgentTool.cs` | Adds an Agent to a Room that already exists. Takes the Room's id, which a Turn's `[Room: …]` label carries. |
| `Tools/PostMessageTool.cs` | Speak into a Room other than the current one. Without this an agent-created Room stays silent. |

`src/Huddle.Acp` — the ACP client:

| Path | Responsibility |
| --- | --- |
| `Abstractions/` | `IAgentHost`, `IAgentSession`, `AgentEvent`, `IAppTool`. Start here. |
| `Abstractions/AgentEffortOption.cs` | One selectable effort level, as advertised through a `thought_level` category entry. A separate record from `AgentModelOption` so a model id can never be passed where an effort id belongs. |
| `DotAcp/DotAcpAgentSession.cs` | One live session. `PromptAsync` throws if a turn is in flight, so serialising is mandatory. |
| `DotAcp/SessionUpdateMapper.cs` | Wire updates to `AgentEvent`. Map synchronously before any await. |
| `DotAcp/SessionConfigSelects.cs` | The three protocol quirks shared by every select-shaped `configOptions` entry, regardless of category — struct-equality category match, flat-list-or-named-groups flattening, and reading the write-back id off the option itself. `ModelConfigOptions` and `EffortConfigOptions` are both thin façades over this — see [Traps](traps.md). |
| `DotAcp/ModelConfigOptions.cs` | Reads the `model`-category entry out of a `session/new` response and resolves a model id back into a `session/set_config_option` call. The shared protocol quirks live in `SessionConfigSelects` now; this class only fixes the category and projects into `AgentModelOption`. |
| `DotAcp/EffortConfigOptions.cs` | `ModelConfigOptions`'s sibling over the `thought_level` category. Materially different class doc: here, an absent entry is the NORMAL case ("this model offers no effort choice"), not "unknown" — see [Traps](traps.md). |
| `Tools/AppToolServer.cs` | Loopback Kestrel serving MCP. **It builds its own DI container**, so `IAppTool` instances must be created by Huddle.App's provider and passed in. |
