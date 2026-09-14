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
| `Services/ChatService.cs` | The only thing that posts a Message, so also the only thing that counts one: it owns each Room's Budget, refuses the post that would exceed it, and `ExtendBudgetAsync` grants one more and re-delivers. Per-Room semaphore — the Budget is read and written inside it. `/invite` lives here. |
| `Services/RoomEvents.cs` | Singleton pub/sub. A handler that throws is logged and skipped, never propagated. `DraftChanged` is the one event the Room view subscribes to and `AgentGateway` must not. |
| `Services/Drafts.cs`, `Draft.cs` | Every Turn's text while it is still arriving, keyed by the Message id it will post under — not by Room, because two Agents can stream into one Room at once. In memory, capped, and cleared when the Message lands or the Agent disconnects. `Draft` is public only because the Room view takes one as a `[Parameter]`. |
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
| `Components/Pages/Settings.razor` | The settings shell at `/settings`, with a hand-rolled tab rail — this repo has no tab, dialog or accordion primitive. Owns every editable Hook value; `HooksPanel` holds none. One tab so far. |
| `Components/Settings/HooksPanel.razor` | The twenty-two Hooks, grouped and editable. Takes exactly one non-string parameter, so it cannot hit the missing-`@` trap [Rules](rules.md) describes and needs no source-regex guard of its own. |
| `Components/Settings/HookFieldFactory.cs` | The grouping, the three-state flags and the textarea sizing, as pure functions — extracted for the same reason `TeammateGrouping` is: `HtmlRenderer` cannot dispatch a click, so logic inside a component is untestable here. |
| `Components/Settings/HookFieldState.cs` | One row's view-model, plus `HookEdit` and the group record. Public because a Razor `[Parameter]` may not be of an internal type; the catalog and the store stay internal behind it. |
| `Components/Settings/ResetAllControl.razor` | Restore every Hook to its default, with an inline confirm rather than a dialog. Stages the defaults like any other edit — Save is still the only thing that writes. |
| `Components/Settings/Appearance.razor` | The Appearance tab: the Theme dropdown, the path to `appearance.json`, and any overrides that were rejected. Renders `ThemeCatalog` rather than a list of its own. Saving forces a full page load — `<head>` belongs to the server and Blazor's render tree cannot reach it. Note the injected field is named `AppearanceStore`, not `Appearance`: a service named after its own component collides with the generated class (`CS0542`). |
| `Components/Settings/SettingsTab.cs` | Which pane the tab rail is showing. Public for the same reason `TeammateCardMode` is — see [Rules](rules.md). |
| `Themes/ThemeTokens.cs` | The 39 Token names `theme.css` declares, as a `HashSet` — the set an override key must belong to, checked in-process with no file access. Pinned against the stylesheet in both directions by `ThemeFileTests`. Roadmap item 7 needs exactly this key set for its mapping. |
| `Themes/ThemeCatalog.cs` | The fixed list of built-in Themes (`ThemeDescriptor(Id, Label, IsDark)`) and the Appearance dropdown's single source. Item 7 extends it by enumerating `{DataDir}/themes/*.css` — a directory to read, never a second list. |
| `Appearance/AppearanceStore.cs` | `HookStore`'s sibling at about a third of the size, over `{DataDir}/appearance.json`: volatile snapshot, one write lock, debounced `FileSystemWatcher`, malformed falls back wholesale, unknown keys kept and never deleted, an absent file normal and never created just to read from. Synchronous, because Razor renders cannot await. A `theme` naming no catalogue entry is a warning, never a failure. |
| `Appearance/AppearanceSettings.cs` | The resolved snapshot a render reads: the Theme id to link, the validated inline CSS, and one line per rejection for the tab to show. |
| `Appearance/ThemeOverrides.cs` | Pure. Validates the Human's Token-keyed overrides and builds the inline `:root { … }` body. **The allowlist here is the sole defence** — the CSS is emitted as a `MarkupString`, so nothing downstream escapes it. See [Rules](rules.md). |
| `Components/Shared/TeammateCard.razor` | One Teammate's details, opened over the page. Viewing, editing and creating are three modes of this one card. |
| `Components/Shared/InviteTeammate.razor` | **Add teammate** on the Room header. Offers only Agents that are not already Members; calls the same `InviteAsync` the `/invite` command and the App Tool do. |
| `Demo/DemoAgentHost.cs` | The echo agents. Its connect-retry loop was the model for `PersonaRunner`. |

`src/Huddle.App/wwwroot` — the stylesheets, in cascade order:

| Path | Responsibility |
| --- | --- |
| `theme.css` | **Layer 1, always loaded.** The 39 Tokens and nothing else: 35 colours as `light-dark(light, dark)`, 4 typography values as plain values, each colour carrying its intended VSCode mapping key as a trailing comment. Every colour and font in this application is declared here — a literal anywhere else fails the build. |
| `themes/huddle-light.css`, `themes/huddle-dark.css` | **Layer 2, linked only when a Theme is selected.** One `:root` block each, declaring `color-scheme` and nothing else, so all 39 Tokens fall through to `theme.css` and resolve to that mode's half of their `light-dark()`. That is the design, not a stub — see [Rules](rules.md) and [ADR-0009](../adr/0009-a-theme-is-a-stylesheet-layered-over-the-tokens.md). Served at their plain unfingerprinted route, which is what lets `App.razor` build the href by convention. |
| `app.css` | Every rule for the chat surface. No colour and no font-family literal: 123 `var()` references and nothing else on the right-hand side of a colour or font declaration. |
| `Components/Layout/MainLayout.razor.css` | Scoped CSS, and the one **written exemption** from tokenisation: `#blazor-error-ui` keeps `lightyellow` and `color-scheme: light only`, because it is the banner shown when the application has already failed. |
| `Components/Layout/ReconnectModal.razor.css` | Scoped CSS for the reconnect dialog, tokenised. Had never reached the browser at all until 2026-09-13 — see [Traps](traps.md). |

Layer 3 is not a file: `App.razor` renders the Human's validated overrides as an
inline `<style>` after both links. All three layers target plain `:root`, so source
order decides per Token, independently.

`src/Huddle.App/Acp` — the join:

| Path | Responsibility |
| --- | --- |
| `ReplyGate.cs` | The reply rule, entire. Budget first, then `memberCount <= 2` or `mentioned`, returning `Reply`, `CatchUp` or `BudgetExhausted`. Still a pure function over labels the server put on the Envelope, so the rule is provable without a pipe or an agent. |
| `Persona.cs`, `PersonaStore.cs` | The Persona library. One `.md` per Persona, anywhere under `{DataDir}/{Acp:TeamsDir}`. Holds an immutable `PersonaIndex`, swapped as one reference and rebuilt before `PersonasChanged` is raised. A recursive, debounced (500 ms) `FileSystemWatcher` drives it; see [Traps](traps.md) for the four ways that watcher fails silently. |
| `PersonaIndex.cs`, `PersonaEntry.cs`, `RejectedPersonaFile.cs` | The pure parse/validate/collision engine, built from `(path, text)` pairs with no I/O — so the write path validates a candidate edit through the very same code the read path uses. |
| `PersonaIdentity.cs` | The four structural frontmatter fields: `Name`, `Title`, `Alias` (all required) and `Teams` (optional). |
| `PersonaFrontmatter.cs`, `PersonaFrontmatterField.cs` | Parses a Persona's leading YAML frontmatter into ordered fields, reads the structural identity out of it, composes the job description `list_agents` shows (holding `name` back, since the bullet above already prints it), and composes the frontmatter `Add` writes. Ported from a sibling repo, not referenced across it — see [Decision record](decisions.md). |
| `PersonaSupervisor.cs` | The only hosted service here. Returns immediately when `Acp:Enabled` is false. One runner per Persona; diffs on change to start, stop or restart. |
| `PersonaRunner.cs` | **The join.** Pipe client on one side, ACP session on the other. Keeps no Budget count of its own — it passes the Envelope's labels to `ReplyGate` — but does hold the per-Persona token Budget, summed from the rises in `UsageUpdated.Used`. Not a `BackgroundService` — see [Traps](traps.md). |
| `PersonaHealth.cs` | What is known about each Persona's Agent right now, keyed by Persona name, with a reason and a since. Written by `PersonaSupervisor` (start failures) and by `PersonaRunner` over an event, so the runner keeps no privileged in-process dependency. |
| `PersonaStatusResolver.cs` | Combines pipe liveness with health into the one status four surfaces render. Health outranks connectivity, and that ordering is the rule — see [Rules](rules.md). Pure, like `ReplyGate`. |
| `IAgentHostFactory.cs` | The test seam. A fake here is why the suite spends zero tokens. |
| `DotAcpAgentHostFactory.cs` | The real one: work dir, bearer token, one `AppToolServer` per Persona, then the agent process. |
| `SystemPromptComposer.cs` | Four Hooks and the Persona's own text, joined in a fixed order. Holds no wording of its own. Receives the tool names already carrying their `mcp__team__` prefix — see [Rules](rules.md) for why it never builds one. |
| `AdapterLocator.cs` | Finds `node_modules/.../claude-agent-acp/dist/index.js`. Returns null rather than throwing — a missing install must never break `dotnet run`. |
| `IModelCatalog.cs`, `ModelCatalogProbe.cs` | The model list offered by the picker, **and** a per-model effort list. ACP has no `models/list`, so this spawns a throwaway adapter, does `initialize` → `session/new` **with the requested model selected**, reads both catalogs off that one session and disposes. **No prompt turn, so no tokens.** Returns an empty list rather than throwing when nothing is installed; `WithoutAdapterDefault` drops the adapter's `"default"` sentinel from the effort list here, at the app layer. |
| `Tools/GetHelpTool.cs` | How this application works, then the whole tool catalog. Built from the other tools, so a tool added to the factory documents itself. |
| `Tools/ListAgentsTool.cs` | Who exists, who is online, and each one's job description composed by `PersonaFrontmatter`. |
| `Tools/CreateRoomTool.cs` | Takes `agents[]` and no name — a Room is named after its Agents. |
| `Tools/InviteAgentTool.cs` | Adds an Agent to a Room that already exists. Takes the Room's id, which a Turn's `[Room: …]` label carries. |
| `Tools/PostMessageTool.cs` | Speak into a Room other than the current one. Without this an agent-created Room stays silent. |

`src/Huddle.App/Hooks` — the model-facing text:

| Path | Responsibility |
| --- | --- |
| `HookCatalog.cs` | Every Hook's default wording, its placeholders, and whether an edit reaches the next Turn or only the next session. The authority: `hooks.default.json` is generated from this, not the other way round. |
| `HookRenderer.cs` | `{{name}}` substitution. Single-pass by construction — matches are found against the original template and the output is built from literal slices, so a substituted value containing `{{b}}` is never re-expanded. |
| `HookValidator.cs`, `HookIssue.cs` | The two failures that are otherwise invisible: a prompt that stopped naming a tool, a Room label that lost its id. Reports and never refuses — see [Rules](rules.md). |
| `HookStore.cs`, `IHookSource.cs` | Resolves overrides over defaults per key. One frozen snapshot behind a volatile field, rebuilt before `HooksChanged` is raised and never mutated after publish. Debounced `FileSystemWatcher`, same recipe as `PersonaStore` — see [Traps](traps.md). Synchronous, because a tool's `Description` getter and a Razor render cannot await. |

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
