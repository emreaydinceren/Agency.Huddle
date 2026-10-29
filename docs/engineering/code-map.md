# Code map

Every file in `src/` that carries a responsibility worth knowing, and what that
responsibility is. Read it when you need to find where something lives; read
[Runtime architecture](architecture.md) when you need to know how it works.

Several entries below point at [Rules](rules.md) or [Traps](traps.md). Those
pointers are not optional reading if you are about to edit that file. Back to
the hub: [Huddle.EngineeringGuide.md](../Huddle.EngineeringGuide.md).

## Where things live

`src/Huddle.App` — the chat surface:

| Path | Responsibility |
| --- | --- |
| `Services/ChatService.cs` | `PostAsHuddleAsync` is the one way to post as Huddle (`KnownIds.Huddle`, ADR-0065). The only thing that posts a Message, so also the only thing that counts one: it owns each Room's Budget, refuses the post that would exceed it, and `ExtendBudgetAsync` grants one more and re-delivers. Per-Room semaphore — the Budget is read and written inside it. `/invite` lives here. `CreateRoomForAsync(ids, team)` creates a Room scoped to a Team, and `EnsureTeamOneToOneAsync(team, agent)` finds or creates that Team's one-to-one (ADR-0052); `EnsureRoomForAsync` stays root-only. |
| `Services/RoomNaming.cs` | What a Room is called when nobody has chosen a name for it: its Agent Members, joined by `", "`. Pure, like `ReplyGate` — and one function rather than three call sites, because `ChatService` needs the identical derivation when creating a Room, creating a group Room, and renaming after an Invitation. `InviteAsync` compares a Room's current name against this before re-deriving, which is the whole mechanism by which a Human-chosen name survives later Invitations. |
| `Services/RoomEvents.cs` | Singleton pub/sub. A handler that throws is logged and skipped, never propagated. `DraftChanged` is the one event the Room view subscribes to and `AgentGateway` must not. |
| `Services/Drafts.cs`, `Draft.cs` | Every Turn's text while it is still arriving, keyed by the Message id it will post under — not by Room, because two Agents can stream into one Room at once. In memory, capped, and cleared when the Message lands or the Agent disconnects. `Draft` is public only because the Room view takes one as a `[Parameter]`. |
| `Services/MentionParser.cs` | Finds Mentions. Resolves `@name` against the Room's Members, longest handle first — **not** by pattern. Candidates are every Member's Name plus every Persona Alias whose owner is in the Room; a Name beats an equal-length Alias. See [Rules](rules.md). |
| `Services/MentionAlias.cs`, `IMentionAliasSource.cs` | Supplies the Aliases in force, so `ChatService` and the invite tools need no dependency on `PersonaStore`. Implemented by `PersonaStore`, registered to the same singleton instance. |
| `Services/MarkdownRenderer.cs` | Markdig. **Never call `UseAdvancedExtensions()`**. |
| `Services/TurnActivity.cs` | Which Agent has a Turn running in which Room, fed by `RoomSession`; the Tasks UI reads it for the "AI reacting" badge (Spec §10.8). |
| `Services/ContextFill.cs`, `ContextFillReading.cs`, `ContextFillBand.cs`, `ContextFillBandKind.cs`, `ContextFillText.cs` | How full each Agent's context window is, per (Agent, Room); a `null` Room is the shared-mode entry, which `Get` returns for any Room marked `Shared`. `RoomSession.CountUsage` reports the latest `Used` and `Size` on every usage update (a gauge: it falls after a `/compact`, so it is never summed). A fresh session open forgets, Welcome and a stopped `PersonaRunner` clear. The Members panel's context meter reads it, and so does `MessageList`, on the meta line of each Agent's newest group; the bands warn at 75% and 90%. `ContextFillText` holds the label and tooltip both use. |
| `Data/SqliteTeamDirectory.cs` | Humans, Agents, Rooms, Members. `ITeamDirectory` is the interface. The `room_teams` table (`room_id`, `team` compared `NOCASE`, `team_room`) holds a Room's Scope: one row per scoped Room, no row for a root Room; a unique index allows one Team Room per Team (ADR-0052). |
| `Data/Models.cs` (`Room.Team`) | `Room.Team`: the Team a Room is scoped to, or `null` for a root Room. Set at creation and never changed. Filled by the reads that join `room_teams`. |
| `Data/FileChatStore.cs` | One JSONL Transcript per Room under `App_Data/rooms/`. |
| `Data/PersonaModelStore.cs` | One row per Persona: its chosen Model. A separate table in the same `team.db`, **not** a column on `users` — see [Rules](rules.md). Synchronous, because its only caller is. |
| `Data/PersonaEffortStore.cs` | One row per Persona: its chosen Effort. A separate table again, for the same reason `PersonaModelStore` is — see [Rules](rules.md). Synchronous, same reason. |
| `Data/PersonaWorkModeStore.cs` | One row per Persona: its chosen Work Mode, the id of a mode its Adapter advertises. A separate table (`persona_work_modes`) again, a mirror of `PersonaEffortStore` — see [Rules](rules.md). `Set` with null or blank deletes the row, because null means the Adapter's own mode. Synchronous, same reason. |
| `Pipes/PipeServer.cs` | Accepts pipe connections. Hosted service. |
| `Pipes/AgentGateway.cs` | Bridges `RoomEvents` to connected Agents. Fills `Members` on every `messagePosted`. |
| `Components/Pages/Chat.razor` | The Room view. |
| `Components/Pages/Agents.razor` | The Persona library UI, at `/agents`. Agent tiles grouped by Team with a Team filter, the rejected-file list above them; owns the open card's state. |
| `Components/Pages/AgentGrouping.cs` | The grouping and filtering behind that page, as a pure function — extracted because `HtmlRenderer` cannot simulate choosing a `<select>` option, so inline it would have been untestable. |
| `Components/Pages/Settings.razor` | The settings shell at `/settings`. A section menu (`MudNavMenu`) on the left and the active section on the right. Each row is a link to `/settings/{section}`, and the highlight is derived from the parsed `{Tab}` route parameter rather than NavLink's URL match, so `/settings/prompts` stays bookmarkable and `/settings` or an unknown segment still falls back to Profile. Owns every editable Prompt value; `PromptsPanel` holds none. |
| `Components/Pages/Tasks.razor` | The Tasks page (Spec §13.2): holds the effective View — the saved View with the toolbar's session overrides applied — and requeries on `TasksReloaded`, `TaskChanged` and `ViewsChanged`. |
| `Components/Pages/TeamPage.razor` | The Team page and the Project page, one component (Spec §6.6): Members, Files and Tasks tabs driven by route parameters. Routes are `/teams/<Team>[/{Tab}]` and `/teams/<Team>/projects/<Project>[/{Tab}]`. |
| `Components/Tasks/TaskColors.cs` | The Spec §13.10 mapping from a Task's state, priority or presence to a MudBlazor `Color` (and the priority icon). |
| `Components/Tasks/TaskDetailMode.cs` | Which of `TaskDetail`'s two layouts (Spec §13.6) is shown. |
| `Components/Tasks/ConflictChoice.cs` | Which side of a conflicting field the Human picked when resolving a `TaskResult.Conflict` (Spec §13.7). |
| `Components/Tasks/NewTaskDefaults.cs` | The Team and Project a new Task's edit page should start in (Spec §13.3). |
| `Components/Tasks/TaskToolbar.razor` | The page's toolbar (Spec §13.3): the header row (counts, Group by, List | Kanban, New task) and the Filter-menu row with a `TaskFilterChip` per filter that is set. Every change is saved to the View as it is made. |
| `Components/Tasks/TaskViewNav.razor` | The sidebar's list of Views (Spec §13.1), one `MudNavGroup` titled "Tasks" between Agents and Settings. |
| `Sidebar/SidebarPanelCatalog.cs` | The Right sidebar's panel picker: for a Subject, the registered panels that apply and whose feature switch is on, in Order. Pure over the registrations and the bound options. |
| `Sidebar/SidebarPanel.cs` | One registered panel: id, title, icon, Order, the Subject test, its component and the optional feature switch. |
| `Components/Sidebar/` | `SidebarPanelFrame`, `PageSubjectSource`, `PageCommandHandler` and the v1 panels: `MembersPanel`, `RoomTasksPanel`, `RoomDocumentsPanel`, `OutlinePanel`, `LinkedDocumentsPanel`, `RecentEditsPanel`. |
| `Components/Tasks/TaskListView.razor` | The List (Spec §13.3a): a read-only `MudDataGrid<TaskItem>` whose columns follow `View.Fields` and whose grouping matches `TaskQuery`'s own ordering rule. |
| `Components/Tasks/TaskBoard.razor` | The Board (Spec §13.4): one `MudDropContainer` with one `MudDropZone` per lane/column, laid out from `BoardLayout.Build`. |
| `Components/Tasks/TaskCard.razor` | The Board's card (Spec §13.4): a `<div>` root so the ⋮ menu's activator and the Awake link sit outside the clickable open-detail control. |
| `Components/Tasks/TaskDetail.razor` | Task detail (Spec §13.6), the editor hosted by the edit page: every field, the Save/Revert cycle, the conflict UI, Close/Reopen/Make a copy, and the Change log. |
| `Components/Tasks/TaskTabs.cs` | How a Task is opened for editing: always in the edit page's own Tab (`OpenNew`, `OpenCopy`, `OpenEdit`), and the draft a copy starts from. |
| `Components/Tasks/FieldsDialog.razor` | The **Fields…** dialog (Spec §13.5): reorder, switch off, remove and add the View's fields. |
| `Components/Tasks/TaskFilterChip.razor` | One "Label: Value" filter or Group by chip of the toolbar, with a menu and a clear button; also the Members tab's team type filter in `BuildTeam`, in its `NoMenu` form (no caret, no menu). |
| `Components/Tasks/ViewActions.cs` | Rename (through `NameDialog`), delete confirm and the default name of a new View. |
| `Components/Tasks/DuplicatePickerDialog.razor` | Asks which Task a moved Task duplicates (Spec §13.4 Dropping step 2), a strict `MudAutocomplete` over the Task index. |
| `Components/Tasks/ReasonDialog.razor` | Asks for an optional reason when a Task moves to Cancelled or Rejected (Spec §13.4 Dropping step 3). |
| `Components/Tasks/AiReactingChip.razor` | The page header's "AI reacting" chip (Spec §13.8, §10.8): shown while a Watched Task's last wake's Room has a Turn running. |
| `Components/Tasks/WakeToasts.razor` | Turns `TaskActivity` wake events into Snackbar toasts while the Tasks page is open (Spec §13.8). |
| `Components/Tasks/SprintLabels.cs` | Renders Sprint labels for Tasks: the Sprint name if known, or a warning if the Sprint id is missing from the catalog. |
| `Components/Settings/PromptsPanel.razor` | The twenty-two Prompts, grouped and editable. Takes exactly one non-string parameter, so it cannot hit the missing-`@` trap [Rules](rules.md) describes and needs no source-regex guard of its own. |
| `Components/Settings/PromptFieldFactory.cs` | The grouping, the three-state flags and the textarea sizing, as pure functions — extracted for the same reason `AgentGrouping` is: `HtmlRenderer` cannot dispatch a click, so logic inside a component is untestable here. |
| `Components/Settings/PromptFieldState.cs` | One row's view-model, plus `PromptEdit` and the group record. Public because a Razor `[Parameter]` may not be of an internal type; the catalog and the store stay internal behind it. |
| `Components/Settings/ResetAllControl.razor` | Restore every Prompt to its default, with an inline confirm rather than a dialog. Stages the defaults like any other edit — Save is still the only thing that writes. |
| `Components/Settings/Appearance.razor` | The Appearance tab: **two** sections since 2026-09-22 — a grouped Theme picker, and the Human's own Avatar, which lives here because the Human has a Name but no Persona file and therefore no Agent card to edit one on. Unlike the card, it has no Save button, so it saves on every change; that difference is deliberate, because there is no Cancel here to protect. It arms a flag before each save: `AvatarStore.Save` raises `AvatarsChanged` **synchronously**, and re-seeding from that echo would flip the Human's just-made choice back — a genuine bug the tests caught. Plus the credit line naming Visual Studio Code and the path to `appearance.json`. The light/dark preference that sat beside it went on 2026-09-21 ([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). A `MudList` with `MudListSubheader` headings, not a `MudSelect`: MudBlazor 9 ships no `MudSelectItemGroup`, and a list shows the whole catalog at once. Renders `ThemeCatalog.Grouped` rather than a list of its own, so Themes arrive without this file changing. Saving applies immediately — `MudThemeProvider` lives in the render tree, so there is no page reload and no `<head>` to rewrite. Note the injected field is named `AppearanceStore`, not `Appearance`: a service named after its own component collides with the generated class (`CS0542`). |
| `Components/Settings/SettingsTab.cs` | Which section the Settings menu is showing. Public for the same reason `AgentProfileCardMode` is — see [Rules](rules.md). |
| `Components/Teams/TeamsNav.razor` | The sidebar's list of Teams and their Projects (Spec §6.5): the Teams are the root rows of one `MudNavMenu`, with no "Teams" group over them. Under an open Team, a **Rooms** folder (its Team Room, then its other scoped Rooms, rows from `RoomNavRow.razor`) and a **Library** folder (its Projects), each a `MudNavGroup`. The Team's `⋯` menu has **New chat** (ADR-0052), shown whatever `ProjectsAllowed` says. Subscribes to `ITeamCatalog.Changed` and `RoomEvents.RoomsChanged`. |
| `Components/Shared/RoomNavRow.razor` | One Room row in the sidebar: the `NavTabLink` to `/rooms/{id}` and its Archive and Delete menu, shared by `RoomList` and `TeamsNav` so the two cannot drift (ADR-0052). |
| `Components/Teams/NewTeamDialog.razor` | The name dialog for creating a new Team or Project (Spec §6.5), behind the TeamsNav's "New team" and "New project" buttons. Copies `Library/AddFolderDialog.razor`'s shape. |
| `Components/Teams/NewTeamDialogMode.cs` | Enum: whether `NewTeamDialog` is naming a Team or a Project. |
| `Components/Teams/TeamTabToolbar.razor` | The action button and search field every Team page tab shares (Spec §6.6): owned state for search text, passed back debounced and trimmed-to-null. |
| `Components/Teams/TeamDetails.razor` | The Details tab of a Team page, always last: `BuildTeam` with only `Team` set, i.e. Build a team's type and details sections for the Team (type and name fixed) with its own Save changes, which writes "What it's for" and the Library folder to `_team.md`. |
| `Components/Teams/TeamRoster.razor` | The Members tab of a Team page: `BuildTeam` with `MembersOnly` (Build a team's Hire section for the Team, with its own Save changes), plus each member's status dot (pipe liveness, then health, through `PersonaStatusResolver`) and a **Message** that calls `ChatService.EnsureTeamOneToOneAsync` and navigates, so it opens the Team's own one-to-one, not the root one (ADR-0052). |
| `Components/Teams/TeamFilesTab.razor` | The Files tab of a Team or Project page (Spec §6.8): a `LibraryExplorer` scoped to the Team's or Project's folder, with a shared toolbar for "New note" and "Search files". |
| `Components/Teams/TeamTasksTab.razor` | The Tasks tab of a Team or Project page (Spec §6.9): shows an in-memory Task View grouped into swimlanes by Project for Teams, or ungrouped for Projects. |
| *(`Components/Teams/TeamsNav.razor.css` — scoped stylesheet)* | Styling for the sidebar Teams list. |
| *(`Themes/ThemeTokens.cs` — deleted 2026-09-14)* | The 39 Token names are gone with `theme.css`. MudBlazor's `Palette` properties are the key set roadmap item 7 maps onto now. See [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md). |
| `Themes/ThemeCatalog.cs` | An assembly point and nothing else: `BuiltIn`, a flat list of eight `ThemeDescriptor`s built from the skin files in each file's `order`, `Grouped`, the projection the picker renders, `Find`, and `Resolve`, which merges a Human's customization over a skin (cached per Theme and customization). Lumen Light is deliberately first — three call sites read `BuiltIn[0]` to mean "the default Theme", which is why grouping is a projection and never a re-sort. |
| `Themes/ThemeGrouping.cs` | One picker group and its Themes; the grouping itself is computed once in `ThemeCatalog.Grouped`. The picker draws no headings, so the record has no display text. |
| `Themes/ThemeDescriptor.cs` | `(Id, Label, MudTheme, Mode, Group)` plus the `Skin` it was built from (`ThemeDescriptor.From(skin)`). `Mode` names the one palette slot the Theme fills — the other keeps MudBlazor's defaults and is never rendered. `Group` is the picker list; today it equals `Mode`, but it is a separate field. |
| `Themes/ThemeMode.cs` | `Light` / `Dark`. The Theme's own palette, and what `MainLayout` derives `IsDarkMode` from — so selecting a Theme selects the mode ([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). |
| `Themes/ThemeGroup.cs` | `Light` / `Dark`. Which picker list shows the Theme. Kept separate from `ThemeMode` so a future group can differ from the palette slot a Theme fills; `HighContrast` was removed with the VS Code ports. |
| `Themes/ThemeDefaults.cs` | The shared UI font stack, and `CreateLight(light)` / `CreateDark(dark)` — one palette each. There is deliberately no two-palette builder: that is what made a borrowed palette expressible. A palette carries colour but no font, so the stack is written once rather than once per Theme. |
| `Themes/Skins/*.json`, `skin.schema.json` | The eight embedded skins (`lumen`, `fluent`, `nordic`, `cobalt-soft`, each `.light` and `.dark`) and their schema. A skin holds palette, tokens, traits, faces, radius, typography and shadows ([ADR-0058](../adr/0058-a-theme-is-a-palette-a-skin-and-traits.md)); how to write one is in [theme-authoring.md](theme-authoring.md). |
| `Themes/Skins/SkinLoader.cs`, `Skin.cs`, `SkinCustomization.cs` | Loads and validates the embedded skins (`LoadAll`), resolves `include`, and validates (`Sanitize`) and merges (`Customize`) a Human's customization. |
| `Themes/Skins/SkinTokens.cs`, `SkinExpression.cs`, `SkinCss.cs` | The token registry (59 tokens with light and dark defaults), the expression grammar (`palette.x`, `mud()`, `ref()`, `mix()`, `alpha()`), and the `<style id="hu-skin">` text `MainLayout` writes. |
| `Themes/Skins/SkinContrastPairs.cs` | The text and focus pairs `SkinContrastTests` and the Theme lab check. |
| `Themes/Skins/Traits/*.cs` | One enum per trait (`MessageLayout`, `SendStyle`, `StatusStyle`, `LabelCase`, `ComposerStyle`, `NavStyle`, `SurfaceStyle`, `MarkStyle`, `ReadingStyle`); a value is rendered as `data-hu-<trait>` on `.theme-marker`. |
| `Components/Settings/ThemeLab.razor` | `/settings/theme-lab`, Development only: live-edits palette and tokens for the circuit, shows contrast badges, Inspect and Export JSON. |
| `Appearance/AppearanceStore.cs` | `PromptStore`'s sibling at about a third of the size, over `{DataDir}/appearance.json`: volatile snapshot, one write lock, debounced `FileSystemWatcher`, malformed falls back wholesale, unknown keys kept and never deleted, an absent file normal and never created just to read from. Synchronous, because Razor renders cannot await. A `theme` naming no catalogue entry is a warning, never a failure. |
| `Appearance/AppearanceSettings.cs` | The resolved snapshot a render reads: the Theme ids and the valid `customizations` by Theme id. The inline CSS and the rejection list went with the override layer; the light/dark preference went with [ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md). |
| *(`Appearance/DarkModePreference.cs` — deleted 2026-09-21)* | `System` / `Light` / `Dark`, and the one place this feature reached for JavaScript. A Theme carries its own palette now, so there is nothing for a second control to choose. See [ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md). |
| *(`Appearance/ThemeOverrides.cs` — deleted 2026-09-14)* | Validated the Human's Token-keyed overrides into an inline `:root { … }` body. The override layer is gone, and with it the `MarkupString` emission its allowlist was the sole defence for. See [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md). |
| `Components/Shared/AgentProfileCard.razor` | One Agent's details, opened as a **`MudDialog`** through `IDialogService` — focus trap, Esc, scroll lock and focus restore come from MudBlazor rather than being owed. Its parameters are frozen at open (`DialogHelperComponent` ignores later `SetParametersAsync`), so it owns its own interactive state and subscribes to health itself. |
| `Components/Shared/InviteAgent.razor` | **Add agent** on the Room header. Offers only Agents that are not already Members; calls the same `InviteAsync` the `/invite` command and the App Tool do. |
| `Components/Shared/RoomList.razor` | The sidebar's list of Rooms, and the **only** place the archived filter is applied — archiving is a display decision, so no storage or delivery code knows the feature exists ([ADR-0018](../adr/0018-a-room-can-be-archived-or-deleted.md)). Each row carries a `MudMenu` offering Archive and Delete, reachable both by right-click and by a keyboard-focusable `⋮` button; Delete confirms by swapping the row, never by stacking a dialog. Lists only root Rooms (`Team is null`), plus unarchived Rooms whose Team is no longer in the catalog, marked with a muted `(<Team>)`; each row is a `RoomNavRow.razor`. |
| `Components/Shared/ArchivedChats.razor` | The **Archived chats** entry, directly above Settings in the Activity bar's bottom toolbar. Carries no state — it only opens the dialog. |
| `Components/Shared/ArchivedChatsDialog.razor` | Lists archived Rooms with an Unarchive button each, plus the same inline Delete confirm. A `MudDialog` through `IDialogService`, following `AgentProfileCard.razor`; it reads its own Rooms on open, so it takes no parameters and there is no `DialogParameters` builder to keep in step. |
| `Avatars/Avatar.cs` | What an Agent shows for itself: `(Label, Image, Background)`, all optional. Rendering is **Image → Label → initials**, so "initials" is the *absence* of an override rather than a stored kind — the same absent-is-normal shape `prompts.json` and `appearance.json` use, and the reason no discriminator is ever written. `Background` is orthogonal to that three-way choice. Also owns `TrimLabel`, which counts **Unicode text elements**, not `string.Length` — one ZWJ emoji is one character however many code units it takes. |
| `Avatars/AvatarStore.cs` | `AppearanceStore`'s sibling over `{DataDir}/avatars.json`, and built from it line for line: volatile snapshot, one write lock, debounced `FileSystemWatcher`, malformed falls back wholesale, unknown keys kept, an absent file normal and never created just to read from, synchronous because Razor renders cannot await. Keyed by Agent **Name**, resolved `OrdinalIgnoreCase` while preserving the key's on-disk casing. Also owns the image files: `WriteImage` names them by a generated id, never by a Name. A `background` that `MudColor` cannot parse is corrected to null with a warning — `MudColor`'s constructor *throws*, and a render that throws on Blazor Server takes the circuit with it. |
| `Avatars/AvatarImage.cs` | Magic-byte sniffing and the 512 000-byte cap (Blazor's own `OpenReadStream` default, not an invented number). PNG, JPEG, WebP; never SVG, never the extension, never `ContentType`. |
| `Avatars/AvatarLibraryOptions.cs` | The `Team:AvatarLibrary` options object, reached through `TeamOptions.AvatarLibrary`. Its one key, `Dir`, is null for no library. Nested like `Library`, `Teams` and `Tasks` ([ADR-0044](../adr/0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md), D-9). |
| `Avatars/AvatarLibraryLocator.cs` | `Dir` is the configured folder, else `BundledDir` (the `AvatarLibrary` folder beside the app); a configured `Dir` is **never** replaced by it, which is how the shipped library is switched off. `IsConfigured`: that folder has a `master.json`, one `File.Exists` and no parse. It decides whether **Choose from library** shows and whether the `/avatar-library` mount is added in `Program.cs`, so deciding to show the button never loads the index. |
| `AvatarLibrary/` | The library that ships with the app: `master.json`, its `master.schema.md` and the 324 pictures, 256 × 256 PNGs, 35 MB in all, brought in unchanged on 2026-10-10 from the folder they were made in. `Huddle.App.csproj` copies the folder beside the app on build and publish (a `Content` item, so it is real files that can be served, never embedded). `BundledAvatarLibraryTests` pins the count, the sizes and that no picture is orphaned. Not a test fixture: tests that change library files work on a copy. `.gitattributes` marks the PNGs binary. |
| `Avatars/SkinGroups.cs` | The one mapping from `skin_tone` to a group: `fair` and `light` to Light, `tan` and `brown` to Tan, `dark` to Dark. It lives in code, not in `master.json`, and an unrecognised or missing tone belongs to no group. |
| `Avatars/LibraryAvatar.cs` | The slim record of the five fields Huddle reads from `master.json`: file, profession, skin tone, gender presentation and tags. Nothing else from the index is kept. |
| `Avatars/AvatarLibraryQuery.cs` | The four inputs to a search: the skin group or All, the text, the selected professions and the selected tags. Professions combine by OR, tags by AND, and the four controls by AND. |
| `Avatars/AvatarCatalog.cs` | The loaded index: the records, the two chip pools (professions alphabetical, tags most frequent first) and the pure `Search(query)`, which returns results in `master.json` order. Held in one private field of the dialog and dropped when it closes (R-1). |
| `Avatars/AvatarLibraryLoader.cs` (`IAvatarLibraryLoader`) | A stateless singleton. `LoadAsync` parses `master.json`, capped at 4 MB, into an `AvatarCatalog` and skips and counts bad records. `ReadImageAsync` returns the bytes of one picked file, checked by its bare name and by magic bytes. It holds no catalogue. |
| `Themes/ContrastColour.cs` | WCAG contrast over `MudColor`, and the one place "is this foreground legible on that background" is answered. `ReadableForeground` returns pure black or pure white, which its remarks **prove** is sufficient — the two intervals overlap, so no sRGB colour defeats both. Replaced the private copies that used to live in `ThemeCatalogTests`; a second copy under `src/` would have broken the same one-function-owns-it rule `StatusDot` and `ReplyGate` rest on. |
| `Components/Shared/AgentAvatar.razor` | The **only** place an Avatar becomes pixels, and the reason `Monogram` no longer lives on `AgentProfileCard` for `Agents.razor` to reach across for. Same argument as `StatusDot`, one domain value later. Renders **exactly one root element** — a Razor component adds no node of its own, and three existing suites locate `.mud-avatar` by position, so a wrapper `<div>` would silently break all three. It also takes `PendingImage` and `PendingImageExtension`, an image chosen but not written yet, which it shows as a `data:` URL so the editor and the card can preview a choice that is still only in memory. `app.css` scales any image under `.mud-avatar` to its circle (`.mud-avatar img`); MudBlazor sizes only its own `MudImage`. |
| `Components/Shared/AvatarChoice.cs` | The editor's three-way radio value. Editor-only and **never persisted**: it is derived from the record on open by the same Image→Label→initials precedence, which is why no discriminator reaches disk. |
| `Avatars/AvatarPalette.cs` | The one palette of avatar background colours (`Colours`, built from MudBlazor's tint/shade, analogous and gradient generators). It is the swatch set of the avatar editor **and** what `Pick(Random)` draws a new Agent's random background from, so a random colour is always one the editor can show as selected. `Pick` takes the `Random` so a test seeds it. |
| `Agents/AgentNamePool.cs`, `Agents/AgentNames.json` | The 121 first names a new Agent's Name box starts with (embedded resource, ASCII only so each passes `NameRules`). `Pick(Random, taken, current)` skips names already taken and the one showing, and falls back to `Name 2`, `Name 3` when the pool is used up. `AgentProfileCard` calls it on opening Create mode and from the Name box's reset adornment (`New random name`). |
| `Components/Shared/AvatarEditorDialog.razor` | Where an Agent's avatar is edited: the three-way choice, the label, an upload, **Choose from library** and the Background palette (static swatches, hidden under **An image**, which covers it). A `MudDialog` opened by pressing the avatar on `AgentProfileCard` in **every** mode. It keeps a working copy in local fields, reads its parameters once, and **writes nothing**: it returns an `AvatarEditorResult` on **Apply** or **Save** and nothing on Cancel, Esc or close. `Deferred` says whether the card holds the result until its own Save (Create, Edit: the button reads Apply) or writes it at once (View: Save). |
| `Components/Shared/AvatarEditorResult.cs` | The dialog result of `AvatarEditorDialog`: the `Avatar` to show plus the bytes and extension of an image not yet written. The bytes travel only under the **An image** choice, so switching to a label after an upload files no orphan image. Internal: it is a dialog result's `object`, never a `[Parameter]`. |
| `Components/Shared/AvatarLibraryDialog.razor` | The modal `MudDialog` picker for an avatar library, opened with `IDialogService` from `AvatarEditorDialog` and from `Appearance.razor`. It owns the catalogue as its one private field, shows the first 48 results with **Show more**, and returns `DialogResult.Ok(fileName)` on a pick. Thumbnails are buttons with an `aria-label` built from the raw skin tone and no visible text ([ADR-0044](../adr/0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md), D-4). |
| `Components/Shared/StatusDot.razor` | The status dot, and the **only** place `PersonaState` maps to a colour. It was copy-pasted in four components before 2026-09-14; [Rules](rules.md) argues one pure function should own the status *ordering*, and this is that argument applied to its *rendering*. |
| `Demo/DemoAgentHost.cs` | The echo agents. Its connect-retry loop was the model for `PersonaRunner`. |

`src/Huddle.App/wwwroot` — the stylesheets, in cascade order:

| Path | Responsibility |
| --- | --- |
| `_content/MudBlazor/MudBlazor.min.css` | **Linked first, deliberately.** It carries bare-element rules including `body{...}`; `app.css`'s `html, body` is the same specificity, so loading the library first lets the app's own rules win the tie. A library reset belongs first in the cascade. |
| `app-vars.css` | The one app-owned custom property, `--font-mono`, because MudBlazor ships no monospace equivalent. Declared once, nowhere else. |
| `app.css` | Every rule the MudBlazor components do not own — the chat transcript, the status dot, page grids. No colour and no font literal: every `var()` names a `--mud-*` variable, a registered `--hu-*` skin token, an `--ink-*` alias or `--font-mono`, enforced by `AppCss_UsesOnlyMudBlazorOrSkinTokens`, `AppCss_ReadsOnlyKnownTokens` and, for scoped stylesheets, `ScopedCss_UsesOnlyMudBlazorVariables`. Down from 1011 lines to 603 across the migration. `.message-row` is a two-column grid since 2026-09-22 — avatar gutter, then meta and body — which replaced the `padding-left: 2ch` that `.message-body` and `.tool-activity` had each been declaring separately. One gutter, declared once. |
| `Components/Layout/MainLayout.razor.css` | Scoped CSS, and the one **written exemption** from tokenisation: `#blazor-error-ui` keeps `lightyellow` and `color-scheme: light only`, because it is the banner shown when the application has already failed. |
| `Components/Layout/ReconnectModal.razor.css` | Scoped CSS for the reconnect dialog, tokenised. Had never reached the browser at all until 2026-09-13 — see [Traps](traps.md). |

Layer 3 is not a file: `App.razor` renders the Human's validated overrides as an
inline `<style>` after both links. All three layers target plain `:root`, so source
order decides per Token, independently.

`src/Huddle.App/Acp` — the join:

| Path | Responsibility |
| --- | --- |
| `ReplyGate.cs` | The reply rule, entire. Budget first, then `memberCount <= 2` or `mentioned`, returning `Reply`, `CatchUp` or `BudgetExhausted`. Still a pure function over labels the server put on the Envelope, so the rule is provable without a pipe or an agent. |
| `RoomReplyResolver.cs` | What one delivery amounted to for the Room **as a whole**, where `ReplyGate` only ever speaks for one Agent: `Expected`, `ContextOnly`, `Paused` or `NoRecipients`. Owns the fan-out and **no part of the rule** - every decision inside it comes from `ReplyGate.Decide`, so the Budget-before-Mention ordering stays unreorderable. An unreachable Agent is filtered out of the recipients rather than folded into the decision; the caller derives reachability through `PersonaStatusResolver`, never from `IsOnline` alone. See [ADR-0012](../adr/0012-a-room-says-why-it-stayed-quiet.md) and [Rules](rules.md). |
| `Persona.cs`, `PersonaStore.cs` | The Persona library. One `.md` per Persona, in its own folder `{DataDir}/{Acp:AgentsDir}/<Name>/`, found by a one-level scan (`Acp:TeamsDir` retired, ADR-0031). Holds an immutable `PersonaIndex`, swapped as one reference and rebuilt before `PersonasChanged` is raised. A recursive, debounced (500 ms) `FileSystemWatcher` drives it; see [Traps](traps.md) for the four ways that watcher fails silently. |
| `PersonaIndex.cs`, `PersonaEntry.cs`, `RejectedPersonaFile.cs` | The pure parse/validate/collision engine, built from `(path, text)` pairs with no I/O — so the write path validates a candidate edit through the very same code the read path uses. |
| `PersonaIdentity.cs` | The four structural frontmatter fields: `Name`, `Title`, `Alias` (all required) and `Teams` (optional). |
| `PersonaFrontmatter.cs`, `PersonaFrontmatterField.cs` | Parses a Persona's leading YAML frontmatter into ordered fields, reads the structural identity out of it, composes the job description `list_agents` shows (holding `name` back, since the bullet above already prints it), and composes the frontmatter `Add` writes. Ported from a sibling repo, not referenced across it — see [Decision record](decisions.md). It now has a **second** write path: `WriteScalarField` rewrites one key's value line in place, preserving every other field and the file's line endings. `Compose` could not be reused for this — it emits only the four identity keys, so recomposing a real Persona through it drops exactly the fields `ComposeJobDescription` sends to the model, and `Parse` is not round-trip safe either (it flattens block scalars and joins lists with `"; "`). A block scalar or block list is refused rather than flattened. |
| `PersonaSupervisor.cs` | The only hosted service here. Returns immediately when `Acp:Enabled` is false. One runner per Persona; diffs on change to start, stop or restart. |
| `PersonaRunner.cs` | **The join, since Room Sessions holding less of it.** Pipe client on one side; on the other, since RS §6.1, a `RoomSessionPool` it owns rather than one `IAgentSession` directly. Keeps no Budget count of its own — it passes the Envelope's labels to `ReplyGate` — but does hold the per-Persona token Budget, summed from the rises in `UsageUpdated.Used` across every Room Session, and the one per-Persona consecutive-failure streak (RS §6.8, D-11). Implements `IRoomSessionOwner`: what a `RoomSession` calls back into this runner through, for everything that is still Persona-wide — writing an Envelope, the Budget, the failure streak, status, and answering a `ReadTranscript` request the pool needs for a fresh session's Catch-up. The per-Turn **idle** watchdog (`Acp:TurnIdleTimeoutSeconds`), `BuildPrompt`, the event reader and the work queue all moved into `RoomSession` (RS §6.1); see [Rules](rules.md) for the two traps that watchdog's cancellation ordering exists to avoid, unchanged by the move. Not a `BackgroundService` — see [Traps](traps.md). File Changes (FC §6.8, §6.15) moved with the Turn machinery into `RoomSession`: `FileChangeTracker.CollectAsync` at Turn start, `RecordTouchedPaths` off `ToolCallStarted`/`ToolCallUpdated` for `ToolKind.Edit`/`Delete`/`Move`, `FileChangeTracker.CommitAsync` at Turn end. |
| `RoomFollows.cs` | Which Agents are Following which Rooms — roadmap item 8's whole state, keyed by Room id because the hot read is "who follows this Room". A Singleton rather than the per-runner `HashSet` ADR-0005 specified: `IAgentHostFactory.CreateAsync` returns only `(IAgentHost, IAgentSession)`, so sharing a field with the tools would mean widening a declared test seam. Never crosses the wire, which is why item 8 cost no `ProtocolVersion` bump. `FollowersOf` hands back a snapshot, never the live set. Self-heal is explicit here — `PersonaRunner` calls `ClearAgent` after its handshake — because a Singleton outlives the runner that a per-instance field would have died with. See [ADR-0012](../adr/0012-a-room-says-why-it-stayed-quiet.md)'s amendment for what that visibility changed. |
| `PersonaHealth.cs` | What is known about each Persona's Agent right now, keyed by Persona name, with a reason and a since. Written by `PersonaSupervisor` (start failures) and by `PersonaRunner` over an event, so the runner keeps no privileged in-process dependency. |
| `PersonaRenameCascade.cs` | Carries a Persona rename into the chat surface. Subscribes to `PersonaStore.PersonaRenamed`, renames the Agent's Team Directory row **in place, keeping its id** — so Rooms, memberships and Transcripts follow, because every one of them references the id and never the Name — then re-derives Rooms still carrying an auto-name, publishes `RoomsChanged`, and moves the Work Dir. Two halves, two urgencies: the row rename is synchronous and inline because it races `PersonaSupervisor`'s restart; everything after it is detached because it races nothing. Its own `IHostedService`, and unlike `PersonaSupervisor` it runs with `Acp:Enabled` false. Also moves the renamed Persona's `FileStateStore` entry (including the moved file's own keys, FC §6.12) on rename, and removes it on `PersonaStore.PersonaRemoved`, following the same pattern as the Avatar cascade. See [ADR-0011](../adr/0011-a-rename-moves-the-agent-not-its-history.md). |
| `PersonaStatusResolver.cs` | Combines pipe liveness with health into the one status four surfaces render. Health outranks connectivity, and that ordering is the rule — see [Rules](rules.md). Pure, like `ReplyGate`. |
| `IAgentHostFactory.cs` | The test seam, split by RS §6.3: `StartAsync(persona, agentId, ct)` starts one Persona's host and opens no session — `IPersonaHost.OpenAsync`/`ResumeAsync` do that, once per Room. A fake here is why the suite spends zero tokens; `FakeAgentHostFactory` returns its scripted `Session` from a host's first `OpenAsync`, records later opens in order in a `Sessions` list, and scripts `ResumeAsync` per test. |
| `IPersonaHost.cs` | What `IAgentHostFactory.StartAsync` returns: one Persona's running Adapter process and App Tool server, behind `OpenAsync`/`ResumeAsync` and a `CanResume` flag read from `sessionCapabilities.resume`. Everything per Persona — Work Dir, bearer token, App Tools, tool server, Adapter process — lives behind this interface; only opening or resuming one Room's session is per call. Carries `Profile`, the resolved Adapter Profile, so a `RoomSession` can read `SessionPerRoom`/`IsolateUserSettings` without a second lookup (finding P-8). |
| `DotAcpAgentHostFactory.cs` | The real `IAgentHostFactory`: work dir, bearer token, one `AppToolServer` per Persona, then the agent process — and returns a `DotAcpPersonaHost` rather than opening a session itself. Resolves the profile first, derives the tool-name prefix from it, and takes its `IAgentProcessLauncher` by injection so a test can drive the real host over an in-proc stream. Also creates `{WorkDir}/memory/` next to the Work Dir on every session, whether or not the memory block ends up in the prompt (FC §6.15); offers `watch_folder`/`unwatch_folder` and builds the Memory index only when `FileChanges.Enabled` **and** the resolved profile's `ReadsFiles` is `true`; and, when `profile.IsolateUserSettings`, passes `meta: { claudeCode: { options: { settingSources: [project, local], settings: { autoMemoryEnabled: false } } } }` into `AgentSessionOptions` (RS §6.10, ACP A-4) — **unverified live**, see [Known limits](known-limits.md). |
| `DotAcpPersonaHost.cs` | The real `IPersonaHost`: one Persona's `DotAcpAgentHost` and `AppToolServer`, started once by `DotAcpAgentHostFactory.StartAsync`. Owns everything `SystemPromptComposer.Compose` and `AgentSessionOptions` need to open or resume a Room's session, so that work runs again, unchanged, on every call rather than only once. Disposing it disposes the `AppToolServer` it was built with — the factory owns the tool server's lifetime, not the inner host alone. |
| `WorkModePolicy.cs` | The hidden list (`Team:Acp:HiddenModes`, default `bypassPermissions`, `auto` and `plan`), `Filter` for the picker and `EffectiveMode`, the enforcement `DotAcpPersonaHost.BuildOptions` uses, so a hand-edited row for a hidden mode is dropped with a warning and never sent. See [Rules](rules.md). |
| `PlanModePermissionHandler.cs` | Wraps the permission handler only when the effective mode is `plan` and refuses the Adapter's `ToolKind.SwitchMode` request to leave it (RejectOnce, else RejectAlways, else Cancelled). Applied by `DotAcpPersonaHost.GuardPermissions`. |
| `SystemPromptComposer.cs` | Four Prompts and the Persona's own text, joined in a fixed order, with a Skills block and then a Memory block appended when either applies (FC §6.15) — each addition is a separate overload that reproduces the previous one byte-for-byte when its new parameter is empty or `null`. Holds no wording of its own. Receives the tool names **already carrying whatever prefix their Adapter takes** — `mcp__team__` for `claude-agent-acp`, nothing at all for an Adapter whose client surfaces MCP names unmodified — see [Rules](rules.md) for why it never builds one and [ADR-0014](../adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md) for why the prefix follows the Adapter. `PromptGoldenTests` pins both forms. |
| `AdapterLocator.cs` | Finds `node_modules/.../claude-agent-acp/dist/index.js`. Returns null rather than throwing — a missing install must never break `dotnet run`. It knows how to find **the Node adapter and nothing else**, which is why `AgentProcessOptionsFactory` consults it only for a profile that takes the `mcp__` prefix. |
| `AdapterProfile.cs`, `AdapterProfileOptions.cs` | One configured Adapter: `Id`, `DisplayName`, `Command`, `Args`, `AdapterPath`, whether its App Tools carry the `mcp__team__` prefix, and `EnvironmentOverrides` — the environment its process is launched with, which is how an Adapter that ships no `appsettings.json` of its own gets configured at all. The record is **public** because `AgentProfileCard` binds it to a `[Parameter]`; the mutable options class exists because `ConfigurationBinder` cannot reliably bind a positional record. Neither `Args` nor `EnvironmentOverrides` has an initialiser, for two *different* binder reasons — see [Rules](rules.md) and the Adapters spec §7.4. `AdapterCatalog` copies the dictionary rather than aliasing it, because a profile outlives the options object and is handed to a Razor parameter. Also carries `ReadsFiles` (default `true`; `false` on `agency-acp`, which has no file tools — turns off the Watched Folder list, `watch_folder`/`unwatch_folder` and frontmatter `watches`, FC §6.11) and `IsolateUserSettings` (default `false`; `true` on the synthesised legacy profile — RS §6.10, finding P-11). |
| `AdapterCatalog.cs` | Every Adapter this installation can launch, frozen at startup from `Team:Acp:Adapters`. When that list is absent it synthesises **exactly one** profile from the legacy `Command`/`AdapterPath`/`Args` keys, which is what makes a stock install byte-identical. A blank `Command` or a duplicate `Id` throws at **startup**, never at first Turn. |
| `AdapterProfileResolver.cs` | A Persona's `adapter:` string to a profile. **Never fails**: absent, blank or unknown all resolve to the default, an unknown id returning a warning alongside it. Pure and synchronous — it must not touch the filesystem, which is what lets `DotAcpAgentHostFactory` and `PersonaSupervisor` both call it for free. See [ADR-0013](../adr/0013-an-adapter-is-a-property-of-the-persona.md). |
| `IAdapterProbeRunner.cs`, `AdapterProcessProbeRunner.cs`, `AdapterProbeOutcome.cs` | The process spawn `ModelCatalogProbe` delegates to, extracted so the cache, the gate and the timeout can be tested without one — [Rules](rules.md) forbids any test reaching the real probe. A test seam, not a public extension point, exactly as `IModelCatalog` says of itself. |
| `IModelCatalog.cs`, `ModelCatalogProbe.cs` | The model list offered by the picker, **and** a per-model effort list and Work Mode list (`GetWorkModesAsync`, cached unfiltered in one `SessionCatalog` under the effort key, so one spawn fills both), all **per Adapter**. ACP has no `models/list`, so this spawns a throwaway adapter, does `initialize` → `session/new` **with the requested model selected**, reads both catalogs off that one session and disposes. **No prompt turn, so no tokens.** Returns an empty list rather than throwing when nothing is installed; `WithoutAdapterDefault` drops the adapter's `"default"` sentinel from the effort list here, at the app layer. Both caches key on the **resolved** profile id — never the requested string — so `null`, `"claude"` and an unconfigured id that falls back to the default all share one entry and one spawn. The effort key joins profile and model with a **unit separator**, because both can contain ordinary punctuation. The `SemaphoreSlim` gate stays single and shared **across** Adapters: a slow probe delaying another is the correct trade against spawning two processes. The spawn goes through `AgentProcessOptionsFactory` like any other, so the probe inherits the profile's `EnvironmentOverrides` — which matters, because `initialize` + `session/new` is exactly where an unconfigured Adapter hard-fails, so the probe is the first thing that environment unblocks. |
| `Tools/FollowRoomTool.cs`, `Tools/UnfollowRoomTool.cs` | Ask to be woken by every Message in a Room, and stop. Take the Room's id, which a Turn's `[Room: …]` label carries — never "the current Room", because an Agent that just created a Room is still answering in the one it was asked from. `FollowRoomTool` refuses a Room the caller is not a Member of: delivery only ever reaches Members, so following one would have reported success and then delivered nothing. |
| `Tools/LogChangeTool.cs` | `log_change`: an Agent's one-line report of a change to a Library Markdown document, written to its History under the Name the tool was bound to (DocHistory spec §6.3). Nothing is verified. Offered when `DocHistory.Enabled` **and** the resolved profile's `ReadsFiles` is `true`, appended before `GetHelpTool` is built. The `DocHistory/` folder holds `DocHistoryStopHook` (the Stop hook that reminds an Agent to log, installed into a Persona's Work Dir when the tool is offered), the store (`{DataDir}/doc-history/{rootId}/{path}.jsonl`), the recorder the Library's save, create, rename and move report to, and the pure view functions. |
| `Tools/WatchFolderTool.cs`, `Tools/UnwatchFolderTool.cs` | Start and stop listing a folder's file changes (FC §6.9). Both delegate every result text to `FileChangeTracker.Subscribe`/`Unsubscribe` verbatim, following `FollowRoomTool`'s shape: an `agentName` bound at construction, one string argument, never throws for an expected failure. Offered only when `FileChanges.Enabled` and the resolved Adapter Profile's `ReadsFiles` is `true` — see `DotAcpAgentHostFactory.cs` and [Rules](rules.md). |
| `Tools/GetHelpTool.cs` | How this application works, then the whole tool catalog. Built from the other tools, so a tool added to the factory documents itself. |
| `Tools/ListAgentsTool.cs` | Who exists, who is online, and each one's job description composed by `PersonaFrontmatter`. |
| `Tools/CreateRoomTool.cs` | Takes `agents[]` and no name — a Room is named after its Agents. |
| `Tools/InviteAgentTool.cs` | Adds an Agent to a Room that already exists. Takes the Room's id, which a Turn's `[Room: …]` label carries. |
| `Tools/PostMessageTool.cs` | Speak into a Room other than the current one. Without this an agent-created Room stays silent. After a successful post it calls `OwnPosts.Record` (RS §6.7), so the target Room's next Turn carries it as *You, from another Room: …* — the only way that Room Session learns of a post its own Turn did not make. |
| `Tools/TaskToolText.cs` | Shared text and argument-parsing helpers every Task App Tool reuses (Spec §11.1): rendering a Task as one line, resolving an id or actor, phrasing the notify clause, and turning a `TaskService` exception into refusal text. |
| `Tools/CreateTaskTool.cs` | Creates a Task (Spec §11.2): validates arguments in the spec's order, then hands the rest to `TaskService.Create` and reports who will be notified. |
| `Tools/SearchTool.cs` | Global Search for Agents (Search spec §18, ADR-0066): calls the five providers directly, Rooms limited to the caller's Member and Team Rooms unless `scope: "all"`, output capped at 12,000 characters. |
| `Tools/GetTaskTool.cs` | Reads one Task's full text (Spec §11.3), including its Change log on request; the one read-only Task tool, so it takes no caller identity. |
| `Tools/ListTasksTool.cs` | Lists Tasks matching a filter, sorted the same way Views are (Spec §11.4), through the same pure `TaskQuery` helpers the Tasks page uses. |
| `Tools/UpdateTaskTool.cs` | Changes one or more fields of an existing Task (Spec §11.5) through `TaskService.Update`, always with `baseVersion: null` since a tool call tracks no draft version. |
| `Tools/CloseTaskTool.cs` | Closes a Task into its Team's (or Project's) `_closed` folder (Spec §11.6); refused when already Closed. Mirrors `ReopenTaskTool`. |
| `Tools/ReopenTaskTool.cs` | Reopens a Task out of `_closed` (Spec §11.6); refused when already Active. Mirrors `CloseTaskTool`. |

`src/Huddle.App/Acp/Sessions` — a Room Session for each (Persona, Room), delivering
[roadmap item 18](roadmap.md#18-one-session-per-room--delivered-code-2026-09-23) and
[ADR-0024](../adr/0024-an-agent-holds-one-session-per-room.md):

| Path | Responsibility |
| --- | --- |
| `RoomSession.cs` | One Room's queue, session, event reader and Turns (RS §6.1) — what the whole runner used to be, before Room Sessions. `Enqueue`, `StopAsync(roomId, mark, ct)`, `CloseAsync` (sends `session/close`, keeps the stored id so a later Turn can resume it), and the opening sequence: try `RoomSessionStore.Get`, resume when the Adapter can and the stored Adapter/Model/Effort/Work Mode still match, else open fresh with Transcript Catch-up. Holds the per-Room `lastUsed` (RS D-10: a shared one would read one session's context fill against another's), the idle watchdog, and this Room Session's own consecutive-failure count (D26, finding P-14) — two failures in a row close and forget it, distinct from the one Persona-wide streak `PersonaRunner` still keeps. Largest file in `Acp/Sessions` by a wide margin: it is every Turn-shaped concern `PersonaRunner` used to own, now scoped to one Room. |
| `RoomSessionPool.cs` | Owns every `RoomSession` a Persona has work in (RS §6.2): lazy per-Room opens except the Room with the Human at start, idle eviction on the injected `TimeProvider` (a 30-second sweep), the `MaxLiveSessions` LRU cap, Stop routing, and the `TurnGate` every Room Session's Turns run through. In shared mode (`SessionPerRoom: false`, finding P-9) it holds exactly one `RoomSession` serving every Room, opened once at start and never evicted or swept (finding P-20) — the same class serves both modes rather than a second implementation. Implements `ITurnScheduler`, the seam `RoomSession` calls back into it through. |
| `TurnGate.cs` | A ticketed admission gate for Turns across every Room Session a Persona owns (RS §6.2, finding **P-4**): admits the lowest-numbered offered ticket first whenever a slot is free, so cross-Room arrival order is preserved exactly — a `SemaphoreSlim` admits by release order instead, which can run a later-arriving Room's Turn first. Tracks tickets explicitly in a `SortedDictionary` under one `Lock`; `Offer` registers and immediately pumps, `Complete`/`Withdraw` of an admitted ticket free its slot and pump again. See [Rules](rules.md). |
| `RoomSessionStore.cs` | Owns `{DataDir}/room-sessions/`, one JSON file per Agent keyed by the Agent's Name, like `file-state/`: `Get`, `Put`, `Forget(name, roomId)`, `ForgetAll(name)` (Restart, a Persona edit), `Prune(name, liveRoomIds)` (at Welcome, against Rooms the Agent is still a Member of), `Rename`, `Remove`. Atomic write under one `Lock`, missing or corrupt read as `null` plus a Warning rather than a crash — costs a fresh session with Catch-up, nothing worse (RS §6.6). |
| `OwnPosts.cs` | An Agent never receives its own Message (`AgentGateway.DeliverAsync`), so a `post_message` into another Room is in that Room's Transcript but not in that Room's session unless something carries it there (RS §6.7, U12). `PostMessageTool` calls `Record` after a successful post; the pool drains `Take` into the target Room's next Turn as `turn.ownPostLine` lines, capped per Room, dropped on that session's first-ever Turn (the Transcript range already holds those posts) and refused while the target Room's own session holds the lock making the post. `ClearAgent` at Welcome, like `RoomFollows.ClearAgent`. |
| `RoomLabels.cs` | Pure, stateless (P0-3, RS D-20): `Distinguish(roomId, roomName, knownNames)` appends `" #"` plus the last six characters of the clashing Room's id when another of the Agent's known Rooms shares its name, case-insensitively — so "Nova" and "nova" still collide, and two same-named Rooms read apart in a Turn's label. `PersonaRunner` owns the mutable table of known names; this only decides what one label should say given it. |
| `WorkItem.cs` | The unit `PersonaRunner`'s read loop hands a `RoomSession.Enqueue`: a built prompt with its Catch-up already taken, so the session's consumer never reads the Transcript or the buffer itself. |
| `IRoomSessionOwner.cs` | The seam `RoomSession` calls back into `PersonaRunner` through, for what stays Persona-wide: writing an Envelope, the token Budget, the one consecutive-failure streak, status changes, and answering a `ReadTranscript` request for a fresh session's Catch-up. `PersonaRunner` implements it explicitly, so nothing outside `Acp/Sessions` can call these members by accident. |
| `ITurnScheduler.cs` | The seam `RoomSession` calls back into `RoomSessionPool` through, for admission: `Offer`/`WaitAsync`/`Withdraw`/`Complete` at `TurnGate`, and `MakeRoomToOpenAsync` — evicting an idle Room Session, if needed, so a requester can open inside its own admitted slot without deadlocking against the live cap (finding P-5). Also holds `ImmediateTurnScheduler`, every member a no-op that completes at once: shared mode (`SessionPerRoom: false`) has exactly one Room Session, so nothing is ever admitted ahead of or behind it. |

`src/Huddle.App/FileChanges` — Watched Folders, File Changes and Memory (FC, delivering
[roadmap item 11](roadmap.md#11-telling-an-agent-which-watched-files-changed--delivered-code-2026-09-23) and
[ADR-0023](../adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md)):

| Path | Responsibility |
| --- | --- |
| `FileState.cs` | The records: `FileChangeKind`, `FileEntry` (size + modified time), `FolderSnapshot` (a folder's files keyed by relative path, `OrdinalIgnoreCase` on Windows/macOS and `Ordinal` on Linux), `FileWriter` (this Agent's own last write to one path: the Room id and the `FileEntry` it left), `RoomBaseline`, `FileState` (the whole persisted shape: `Subscribed`, `Rooms`, `Writers`), `FileChange` (one reported line, with the optional `ByYouRoomName` FC §6.15 adds), `FileChangesReport` (a Turn's capped block, `NotListed` and `Unchecked`). |
| `FileChangesOptions.cs` | Bound from `Team:FileChanges` (FC §6.14): `Enabled` (default `true`; `false` passes a `null` tracker to every runner and offers neither tool), `Ignore` (default `.git`, `node_modules`, `bin`, `obj` — read through `EffectiveIgnore`, never the raw property, because `ConfigurationBinder` appends to a pre-populated collection instead of replacing it), `MaxFilesPerFolder` (5000), `MaxListed` (50), `MaxMemoryEntries` (100). |
| `FolderScanner.cs` | Walks one folder into a `FolderSnapshot`, pruning `Ignore` directory names and never following reparse points, reporting `TooLarge` past `MaxFilesPerFolder` rather than truncating. |
| `WatchedFolderResolver.cs` | Turns one `watches`/`watch_folder` entry — an Agent Name or a path — into a full folder path inside `DataDir`, or a Degraded warning when it resolves to neither. |
| `FileStateDiff.cs` | Pure comparison of two `FolderSnapshot`s into added/changed/deleted `FileChange`s, sorted, size-only and time-only changes both counting as `Changed`. |
| `FileStateStore.cs` | Reads and writes `{DataDir}/file-state/<Name>.json`, atomically (no `.tmp` left behind), missing or corrupt treated as `FileState.Empty`. `Rename` moves the file and rewrites every Room's keys and every entry's Writers; `Remove` deletes it — both called from `PersonaRenameCascade`. |
| `FileChangeTracker.cs` | The tracker `PersonaRunner` holds: `CollectAsync` at Turn start (scans every Watched Folder — the Agent's own Work Dir always, plus `watches` frontmatter and `Subscribe`d folders — diffs against that Room's saved baseline, and marks a diff *by you, in Room 'X'* when the current `FileEntry` matches this Agent's own last recorded write there); `CommitAsync` at Turn end (the new baseline is the start scan plus only the paths this Turn's own `Edit`/`Delete`/`Move` tool calls touched, never a full rescan); `Subscribe`/`Unsubscribe` for `watch_folder`/`unwatch_folder`, both idempotent ("already watching") and persisted through `FileStateStore`. |
| `TouchedPaths.cs` | Pulls rooted file paths out of a tool call's raw input JSON — a `file_path` value, nested or arrayed — ignoring relative strings, non-path strings, `null` and malformed JSON. Adapter-agnostic by design (FC D-3b): parsing `Bash` commands is deliberately out of scope, because a wrong attribution must only ever cost an extra line, never claim another Agent's change. |
| `MemoryIndex.cs` | `MemoryEntry` (a summary and a full path) and `MemorySnapshot` (the folder path, its entries, `NotListed`) — `DotAcpAgentHostFactory` wraps `Build`'s return into the latter. `Build(memoryDir, maxEntries)` reads only `*.md` directly inside `memory/` (not subfolders), takes each file's first non-blank line as its summary (leading `#` and spaces stripped, cut at 200 characters, falling back to the file name if blank or unreadable), orders by file name ordinal so the system prompt stays byte-stable while memory is unchanged, and caps at `maxEntries`, counting the rest in `NotListed`. Pure apart from the reads; a missing folder gives an empty index. |

`src/Huddle.App/Prompts` — the model-facing text:

| Path | Responsibility |
| --- | --- |
| `PromptCatalog.cs` | Every Prompt's default wording, its placeholders, and whether an edit reaches the next Turn or only the next session. The authority: `prompts.default.json` is generated from this, not the other way round. |
| `PromptRenderer.cs` | `{{name}}` substitution. Single-pass by construction — matches are found against the original template and the output is built from literal slices, so a substituted value containing `{{b}}` is never re-expanded. |
| `PromptValidator.cs`, `PromptIssue.cs` | The two failures that are otherwise invisible: a prompt that stopped naming a tool, a Room label that lost its id. Reports and never refuses — see [Rules](rules.md). |
| `PromptStore.cs`, `IPromptSource.cs` | Resolves overrides over defaults per key. One frozen snapshot behind a volatile field, rebuilt before `PromptsChanged` is raised and never mutated after publish. Debounced `FileSystemWatcher`, same recipe as `PersonaStore` — see [Traps](traps.md). Synchronous, because a tool's `Description` getter and a Razor render cannot await. |

`src/Huddle.App/Tasks` — Tasks, the work item the Human and AI Agents share (Spec §7-§13):

| Path | Responsibility |
| --- | --- |
| `TaskId.cs` | A Task's unique identifier: `PREFIX-0042`, parsed and formatted case-insensitively. |
| `TaskItem.cs` | The Task record itself, plus `TaskActor`, `TaskActorKind`, `TaskLocation` and `ChangeLogEntry`. |
| `TaskState.cs` | `TaskState` and `TaskPriority`, their wire names, declaration order and `TryParse`/`ToWire` extensions. |
| `TaskLayout.cs` | Maps a file path to a `TaskLocation` under the Team/Project/`_closed` folder rules, and back to a file path. |
| `TaskFileFormat.cs` | Parses, composes and version-hashes a Task file (Spec §7); the only place that reads or writes a Task file's text. |
| `TaskStore.cs` | Owns every file under `{DataDir}/{Tasks.Dir}`: the in-memory index, rejected files, Team/Project listing, writes and moves, and the outside-edit watcher (Spec §8). |
| `RejectedTaskFile.cs` | A file under the Tasks scan root that did not become an indexed `TaskItem` (Spec §8.1-§8.2). |
| `TeamFolder.cs` | A Team folder found under the Tasks scan root, with its Project sub-folders (Spec §8.2). |
| `TaskIdAllocator.cs` | Assigns Task ids: a per-Team prefix and a never-reused per-Team number (Spec §8.6), stored in `team.db`. |
| `OutsideEdit.cs` | A Task's state before and after an edit made outside Huddle, noticed by `TaskStore`'s filesystem watcher (Spec §8.4). |
| `TaskDraft.cs` | A new Task, as offered to `TaskService.Create` (Spec §9.1). |
| `TaskPatch.cs` | Distinguishes "leave this field alone" from "set this field, possibly to null" for a Task update (Spec §9.1). |
| `TaskResult.cs` | The outcome of a `TaskService` mutation (Spec §9.1): expected failures are values, never exceptions. |
| `TaskDiff.cs` | A field-by-field diff between two versions of a `TaskItem` (Spec §7.5) and the Change log summary vocabulary (Spec §7.4). |
| `TaskDiffTypes.cs` | `FieldChange` and `TaskField`, the fields a Task diff can name. |
| `TaskEvents.cs` | One successful change to a Task (Spec §9.5): before, after, what changed, who changed it, and its Change log entry. |
| `TaskService.cs` | The only way to change a Task (Spec §9): validates, builds the Change log entry, writes through `TaskStore`, and raises `TaskEvents.TaskChanged`. |
| `TaskActors.cs` | Builds the `TaskActor` for the Human, the one actor `PersonaStore` knows nothing about. |
| `TaskTriggerService.cs` | What saving a Task change would do to its assignee (Spec §10.1): who it would notify, their presence, and which guard stops it. `ChooseRoomAsync` still prefers a usable origin Room; its fallback Room (the Human, the creator and the assignee) is found and created scoped to the Task's Team (ADR-0052). |
| `TaskActivity.cs` | `WakeOutcome`, `WakeRecord` and `WakeBudget`, plus the in-memory per-Task record of Agent-made wake attempts (Spec §10.6-§10.7). |
| `AssigneeOption.cs` | One candidate in `TaskDetail`'s Assignee autocomplete (Spec §13.6): a known Persona's Name, title and presence. |
| `TaskPresence.cs` | Resolves the Tasks UI's presence badge (Awake/Asleep/Offline) from an Agent's online and busy state (Spec §10.8). |
| `TaskReference.cs` | Resolves a candidate Task id found in rendered Markdown to the Task it names, so `MarkdownRenderer` can link it (Spec §13.13.2). |
| `TasksOptions.cs` | Configuration for the Tasks feature: `Enabled`, `Dir`, `WakeEnabled`, `WakeCoalesceSeconds`, `AgentWakeBudget`. |
| `SprintId.cs` | A Sprint's unique identifier: `S42`, parsed and formatted case-insensitively. |
| `Sprint.cs` | The Sprint record: its `Id` and human-readable `Name`. |
| `SprintFileFormat.cs` | Parses and composes the `sprints.md` file (Spec §6); the only place that reads or writes Sprint lines. |
| `ISprintCatalog.cs` | The live list of Sprints, rebuilt when the file changes or a Task changes its Sprint. |
| `SprintStore.cs` | Owns `{DataDir}/sprints.md`: the in-memory index, file writes on Sprint create and removal, and the watcher that detects outside edits (Spec §8). |

`src/Huddle.App/Tasks/Views` — saved arrangements of Tasks (Spec §12):

| Path | Responsibility |
| --- | --- |
| `TaskView.cs` | A saved arrangement of Tasks: `TaskView`, `TaskFilter`, `ViewKind`, `ViewScope`, `TaskGroupField`, `SortKey`, `BoardColumn` and the Views-document shape. |
| `ViewFieldKeys.cs` | The field keys a View's Fields and sort keys can name (Spec §12.2). |
| `ViewJson.cs` | JSON options and converters for `views.json`, including the wire-name converters for `TaskState` and `TaskPriority`. |
| `ViewStore.cs` | Joins the two built-in Views with the Human-editable `views.json`, resolving the current set of Views every render reads. |
| `ViewValidator.cs` | Validates a `TaskView` against Spec §12.4; pure, never throws for an invalid View. |
| `BoardLayout.cs` | Builds a `BoardModel` (lanes and columns) from sorted Tasks and a `TaskView`, for Spec §12.6. |
| `TaskQuery.cs` | Pure querying over Tasks: filtering, sorting, grouping and the `#` picker's search (Spec §12.5, §13.13.4). |

`src/Huddle.Acp` — the ACP client:

| Path | Responsibility |
| --- | --- |
| `Abstractions/` | `IAgentHost`, `IAgentSession`, `AgentEvent`, `IAppTool`. Start here. |
| `Abstractions/AgentEffortOption.cs` | One selectable effort level, as advertised through a `thought_level` category entry. A separate record from `AgentModelOption` so a model id can never be passed where an effort id belongs. |
| `Abstractions/AgentModeOption.cs` | One selectable Work Mode, as advertised through a `mode` category entry. A separate record so a model or effort id can never be passed where a mode id belongs. The ACP layer says `Mode`; the app says `WorkMode`. |
| `DotAcp/DotAcpAgentSession.cs` | One live session. `PromptAsync` throws if a turn is in flight, so serialising is mandatory. |
| `DotAcp/SessionUpdateMapper.cs` | Wire updates to `AgentEvent`. Map synchronously before any await. |
| `DotAcp/SessionConfigSelects.cs` | The three protocol quirks shared by every select-shaped `configOptions` entry, regardless of category — struct-equality category match, flat-list-or-named-groups flattening, and reading the write-back id off the option itself. `ModelConfigOptions` and `EffortConfigOptions` are both thin façades over this — see [Traps](traps.md). |
| `DotAcp/ModelConfigOptions.cs` | Reads the `model`-category entry out of a `session/new` response and resolves a model id back into a `session/set_config_option` call. The shared protocol quirks live in `SessionConfigSelects` now; this class only fixes the category and projects into `AgentModelOption`. |
| `DotAcp/EffortConfigOptions.cs` | `ModelConfigOptions`'s sibling over the `thought_level` category. Materially different class doc: here, an absent entry is the NORMAL case ("this model offers no effort choice"), not "unknown" — see [Traps](traps.md). |
| `DotAcp/ModeConfigOptions.cs` | The `mode`-category sibling: `Read`, `TryResolve` and `CurrentValue` over `SessionConfigSelects`. Unlike effort, it keeps `default` (Manual is a real mode). `DotAcpAgentHost` applies Model, then Effort, then Mode, and reads each response back to catch a clamp. |
| `Tools/AppToolServer.cs` | Loopback Kestrel serving MCP. **It builds its own DI container**, so `IAppTool` instances must be created by Huddle.App's provider and passed in. Its 401 carries a **bare** `WWW-Authenticate: Bearer` — no `resource_metadata`, deliberately, because that parameter is what keys MCP's OAuth discovery path and neither side has designed one. |

`src/Huddle.App/Teams` — the Teams catalog and membership operations:

| Path | Responsibility |
| --- | --- |
| `Teams/ITeamCatalog.cs` | The live list of Teams as the pages see it, rebuilt whenever a Persona label or a Team folder changes. |
| `Teams/TeamCatalog.cs` | Builds and holds one immutable snapshot of Teams, rebuilt on `PersonaStore.PersonasChanged` and `TaskStore.IndexChanged` events. Swaps the snapshot in before raising `Changed`. No lock: a reader always sees a whole snapshot. |
| `Teams/TeamSummary.cs` | One Team as the pages see it: Name (the display spelling), HasMembers, and the read-only list of Projects. Names compare ignoring case. |
| `Teams/TeamPageFeatures.cs` | Which optional features the installation has on; decides which tabs exist on Team pages. |
| `Teams/TeamPageTab.cs` | A tab of a Team or Project page: Members, Files or Tasks. |
| `Teams/TeamPageTabs.cs` | Determines which tabs appear based on enabled features, and resolves URL segments to tabs. |
| `Teams/ITeamMembership.cs` | Adds a Persona to a Team or removes it, by rewriting only the `teams` field of the Persona's definition. |
| `Teams/TeamMembership.cs` | Implementation: reads the Persona, edits its YAML, saves it through `PersonaStore.Update`, which keeps the Model and Effort and raises `PersonasChanged`; it also passes `persona.WorkMode`, because `Update` has no default for it and a membership edit would otherwise wipe it. Serialises the whole read-modify-write. |
| `Teams/MembershipOutcome.cs` | Enum: what a membership change did (Added, Removed or Unchanged). |
| `Teams/MembershipResult.cs` | The outcome, plus the store's message when the save was rejected. |
| `Teams/TeamLabels.cs` | Pure operations on a list of team labels, comparing case-insensitively by team name: add, remove, contains, unique-with-spelling. |
| `Teams/TeamNames.cs` | Validation rules for Team and Project names, and the reserved `memory` folder name. `UploadsFolder` (`Uploads`) is reserved beside it: visible in the Library, never a Project (ADR-0052). |
| `Teams/TeamRoomReconciler.cs` | Hosted singleton on `TeamCatalog.Changed` and `RoomsChanged`: creates each Team's Team Room (flagged `team_room`) once it has a member, invites members who are missing from it, and archives scoped Rooms whose Team has left the catalog. Does nothing while the catalog is empty. Runs are serialised. An Archived Team Room is never recreated or refilled (ADR-0052). |
| `Teams/TeamMemoryGroup.cs` | One Team's Memory snapshot, holding the Team-wide Memory and each Project's Memory separately (Spec §6.2, §7.2). |
| `Teams/TeamMemorySnapshot.cs` | A Persona's Team Memory snapshot, holding each Team's Memory grouped by Project (Spec §7.2). |
| `TeamTypes/TeamTypeCatalog.cs` | Loads, validates and serves team types from the embedded `TeamTypes/Builtin/` folder and `{DataDir}/TeamTypes/`; a user folder with the same id replaces the built-in one. A type that fails validation is listed in `Problems`, never thrown. ADR-0038. |
| `TeamTypes/TeamTypeManifest.cs` | The records a `team-type.json` reads into (roles that name a hiring-pool job, coverage, processes, workspace), and `ModelTier` (`Strongest`, `Balanced`, `Fast`). |
| `TeamTypes/TeamTypeValidator.cs` | Checks a manifest and reports each problem with a code (`TeamTypeProblem`). |
| `TeamTypes/CandidateTemplate.cs` | One Candidate: an Agent template read from a type's `candidates/*.md`. |
| `TeamTypes/UserCandidateWriter.cs` | Writes a custom role the Human chose to keep ("Save for next time") into `{DataDir}/TeamTypes/`. |
| `TeamTypes/Builtin/` | The embedded built-in types: `development/` and `household/` (a manifest, `candidates/` and `processes/` each). No Skill packs ship any more: they come from `SKILLS.md`. Picked up by the csproj glob, so a new built-in file needs no csproj change. |
| `Teams/HireService.cs` | Writes one hire as an Agent of a Team, enforces the hire limits, copies its Skills through `ISkillCopier` (`EnsureSkillsAsync`) and starts it; `StartTeamAsync` starts the hired members in order. |
| `Teams/ISkillAvailability.cs` | What the hire path asks of the registry: a Pack's contents, a Skill's state, and the setup item for one that is not ready. `SkillRegistry` implements it; tests use `FakeSkillAvailability`. |
| `Teams/SkillSetupCheck.cs` | Turns a card's Packs and Skills into the **Needs setup** items (`For`); a Pack's own problem folds into one item. Pure. |
| `Teams/PackMembership.cs` | Adds or removes one Pack on an Agent's `skills`, `_packs` and `consult_when`, keeping Skills another Pack or the Role still gives (`PackContents`). Pure. |
| `Teams/RoleHirer.cs` | Turns a type's roles into hire cards for `HireService`. `IHireContext` says which adapters answered and which connectors are connected. |
| `Teams/HireContext.cs` | The production `IHireContext`: every Adapter profile counts as answered, and models come from `Team:Teams:Tiers`. |
| `Teams/CardResolver.cs` | Resolves one role's defaults into a hire card: `Ready` when all resolve, `NeedsSetup` otherwise. |
| `Teams/TierModelPicker.cs` | Picks a card's model from `Team:Teams:Tiers:<adapterId>:<tier>`; an adapter with no entry for a tier uses its default model. |
| `Teams/FileConnectorStatus.cs` | Reads the connected connectors from `connectors.json` in the data folder on every call; a missing or malformed file means none. |
| `Teams/MemberWarnings.cs` | The setup warnings for one member: a shared vendor, too many Skills, and Skills sharing a catalog sub-category. |
| `Teams/TeamDuplicator.cs` | Copies a Team's members into a new Team for Duplicate, and reports each member it did not copy, with the reason. |
| `Teams/ProcessDocument.cs` | Parses and writes `Process.md`: the `gates`, `wip`, `checkCommand`, `retro` and `digest` keys, and the four `GateId`s. |
| `Teams/ProcessProvisioner.cs` | Copies a process template into the Team folder as `Process.md`. It never overwrites one; `ProvisionResult` says how the call ended. |
| `Teams/ProcessGateService.cs` | Judges a proposed Task status change against the enabled gates (`CheckTaskChange`, `CheckMerge`); the first refusal wins. `TaskService` calls it. ADR-0039. |
| `Teams/ProcessMergeGate.cs` | Backs `IMergeGate` with the Team's process, so checks-before-merge applies to `git merge` and `git push` from its workspace. |
| `Teams/CadenceService.cs` | Hosted service: each minute, wakes a Team's manager for a digest and its retro role for a retro, on time or after N Done Tasks. Its last firings are kept in `team.db`. A digest or retro posts in the Team Room with a Mention of the recipient, or in the recipient's root one-to-one if the Team Room is Archived or lacks them, through `IOnboardingPoster.PostToTeamAsync` (ADR-0052). |
| `Teams/RecordsBundle.cs` | The evidence bundle for a retro: `RecordsBundleBuilder` builds it from the Tasks changed since a cutoff, and `RenderText` renders it. |
| `Teams/RetroFiles.cs` | Reads and writes a Team's `Retro register.md` and its reports in `_retros/`. |
| `Teams/RetroProposalService.cs` | Shows a retro's proposed changes to the Human and applies the ones the Human accepts. |
| `Teams/RetroVerifier.cs` | The register rows the next retro must check against later evidence (RT-7), and which are due. |
| `Skills/SkillProvenance.cs` | The `.source.json` record written beside a copied Skill (Source, origin, `copiedFrom`, commit, folder hash, time), read back for provenance and local-edit detection. |
| `Teams/TeamOnboarding.cs`, `Teams/ChatOnboardingPoster.cs` | Posts the manager's onboarding prompt into the manager's direct Room as the Human, once: the Team card then records `onboarded: true`. `IOnboardingPoster` (in `TeamOnboarding.cs`) also has `PostToTeamAsync`, the Team-aware post that cadence uses; onboarding keeps `PostAsync`, which stays root-only (ADR-0052). |
| `Tasks/RequestFields.cs` | The Request keys of an ordinary Task file, and the `RequestKind` (bug, feature, feedback, question) and `TriageState` enums. ADR-0040. |
| `Tasks/RequestLifecycle.cs` | The Request state machine: `Apply` decides each transition from the current state and an event, and is pure. |
| `Acp/Tools/FileRequestTool.cs` (`file_request`) | Files a Request into another Team's backlog. Offered only to a Persona whose Skill lists the tool. |
| `Acp/Tools/TriageRequestTool.cs` (`triage_request`) | Lets the receiving manager accept, decline or ask for more on a Request. |
| `Acp/Tools/ConfirmRequestTool.cs` (`confirm_request`) | Lets the requester-side QA confirm a Request is done, which closes it. |
| `Acp/Tools/ListTeamsTool.cs` (`list_teams`) | Lists every Team's card and how many Requests are open in each backlog. |
| `Acp/Tools/AddEvidenceTool.cs` (`add_evidence`) | Adds an Evidence line to a Task, the input the gates read. |
| `Acp/Tools/ReadRecordsTool.cs` (`read_records`) | Gives the retro role a Team's evidence bundle, or one Task's change log and Evidence. |
| `Acp/Tools/ProposeRetroChangesTool.cs` (`propose_retro_changes`) | Lets a member holding the `retrospective` Skill propose up to three exact-text changes to the Human. |
| `Acp/Tools/MarkRetroRowTool.cs` (`mark_retro_row`) | Updates one `Retro register.md` row's status. |
| `Components/Pages/TeamSetup.razor` | `/teams/new` and `/teams/{Team}/setup`: the New Team from a type form, then the setup screen with the hiring pool, the team check bar and Start Team. |
| `Components/Shared/ChipCloud.razor` | A cloud of toggle chips (selected first, optional cap with "Show all"), the avatar picker's cloud as a component. |
| `HiringPool/HiringPoolFile.cs` | The records `hiringpool.json` reads into: `PoolTeamType`, `PoolJob`, `HiringPoolFile`. |
| `HiringPool/HiringPoolCatalog.cs` | Loads the embedded pool and the user pool (merged by id), serves jobs, team type names and personas; never throws for bad content. |
| `Components/Teams/ProcessStep.razor` | The Process step on a manager's Agent card: Use this process, or Start blank. |
| `Components/Teams/ProcessTab.razor` | The Process tab of a Team page: `Process.md`, with Edit. |
| `Components/Teams/DuplicateTeamDialog.razor` | The Duplicate dialog: the new Team's title and an optional workspace folder. |
| `Components/Shared/SkillProvenanceLine.razor` | The provenance line shown wherever a Skill appears, with a "modified locally" chip. |
| `Components/Shared/RetroProposalCard.razor` | A Room's pending retro changes, with Accept and Reject. |
| `Teams/TeamMemoryIndex.cs` | Builds one Persona's Team Memory snapshot: the Team-wide and per-Project Memory of every Team the Persona carries a label for, capped across all of them (Spec §6.4, §8.4). |

`src/Huddle.App/SkillSources` — the Skill registry, the copier and the files an AI tool reads
([the Skill Packs design](../Huddle.SkillPacks-Specifications.md) §6.5 to §6.13). Huddle never downloads:
an AI tool writes `SKILLS.md` and its files, and this folder reads, watches and copies.

| Path | Responsibility |
| --- | --- |
| `SkillRegistry.cs` | The singleton every Skills screen and the hire path read: one immutable `RegistrySnapshot` (resolved rows, states, findings, holder counts), replaced whole on each rebuild and announced by `Changed`. Implements `ISkillAvailability`. `RebuildAsync(RebuildScope)` re-reads; `MarkSeenAsync` clears a badge. |
| `RegistrySnapshot.cs` | The snapshot, `RegistryChangedEventArgs`, `RebuildScope` and the readiness records. |
| `RegistryWatcher.cs`, `WatchRoots.cs` | The hosted watcher: runs the move first, then watches the Skill Sources folder and each pointer root (at most `MaxWatchers`, the rest polled) and asks for a rebuild, coalesced. `WatchRoots` is the pure rule for what to watch. **It must not throw at process start**, even with an empty `DataDir`. |
| `CachingFileReader.cs` | The `IFileReader` the registry reads through: file text cached by write time and length, folder hashes by newest write time and file count. |
| `SkillCopier.cs`, `SkillCopyFolders.cs` | `ISkillCopier`: copies one Skill from its pointer into `Skills/<name>/` through `.staging`, swaps the old copy into `.trash`, writes `.source.json`, and re-checks the folder (an App Tool in `tools:` fails it). |
| `PackUpdater.cs` | Applies an accepted update: copies the changed Skills, then rewrites the holders' membership. |
| `InstallFileWriter.cs`, `RoleLister.cs` | Writes `InstallSkillPack.md` and `install-context.json` at start-up and on change (once a second at most); `RoleLister` is the union of Roles its appendix lists. |
| `SkillPromptBuilder.cs`, `PathFormats.cs` | The six prompts the Human copies into an AI tool, built from the `skillPrompt.*` Prompts; the three path forms (`PathFormatChoice`) the Copy path menu offers. |
| `SkillSourcesSettings.cs` | `skill-sources.json`: the folder override and the `seen` keys; resolves the Skill Sources folder. |
| `SkillsMdWriter.cs` | The only code that edits `SKILLS.md`: removes a row's lines, through a temporary file, retrying once. |
| `SkillSourcesRemover.cs` | Remove and hide: a Pack (with what to do for its holders), a catalog, a whole Source; refuses anything outside the Skill Sources folder. |
| `HiddenStore.cs`, `SourceLog.cs` | `hidden.json`, and the append-only `log.jsonl` of each Source. |
| `Move/SkillSourcesMove.cs`, `Move/LegacyReaders.cs`, `Move/MoveTypes.cs` | The one-time move from the old `SkillPacks/` and the embedded `Skills/SkillsCatalog.json` into `SKILLS.md`, six catalogs and the Sources `local/skills` and `local/my-packs`. Runs only while `SKILLS.md` does not exist; writes `skill-sources-move.json`. Changes no Agent. |

`src/Huddle.App/Skills/Ui` — pure text and view models for the Skills screens, so the components stay
thin and the wording is tested without rendering:

| Path | Responsibility |
| --- | --- |
| `SkillGuide.cs` | Every intro, button sentence, hover text and in-app help page of Settings › Skills (`For`, `HelpFor`). The User Guide subsections are compared with it word for word. |
| `SkillStateText.cs`, `SkillProblemText.cs`, `SkillProblemCount.cs` | A state's chip text, colour and sentence; a finding's sentence; the count behind a tab badge. |
| `SkillRow.cs`, `PackView.cs`, `SkillOriginText.cs`, `SkillNotes.cs`, `ReadAgoText.cs` | One Skills tab row; one Pack card; where a Skill came from; the Source's notes link; "read 2 minutes ago". |
| `PackUpdatePlanner.cs` | Plans an update card from the snapshot and the holders: which Skill files changed and which membership changes each holder accepts. |
| `RemovalText.cs` | The confirmation and result sentences for Remove. |
| `PromptClipboard.cs` | Copies a prompt and says so in a snackbar, with the fallback when the clipboard is refused. |

`src/Huddle.App/Components/Settings/Skills` — Settings › Skills, `G4` to `G8` in
[wireframes](wireframes.md):

| Path | Responsibility |
| --- | --- |
| `SkillsTab.razor`, `PacksTab.razor`, `SourcesTab.razor` | The three tabs, each with its intro and **Learn more**. The Skills grid virtualises only above 500 rows. |
| `PackBar.razor`, `PackCard.razor`, `PackEmptyState.razor`, `PackDetailsDialog.razor` | The Packs tab's filter bar, one card, the empty state and the details dialog. |
| `SourceRow.razor`, `SkillSourcesFolderDialog.razor` | One Source on the Sources tab; changing the Skill Sources folder. |
| `AddWithAiDialog.razor`, `CopyPathMenu.razor` | **Add with your AI tool**: the prompt, where `InstallSkillPack.md` is, and the path forms. |
| `UpdateDialog.razor`, `RemovePackDialog.razor` | The update card (`G7`) and Remove Pack, with the holders' choice. |
| `SkillStateChips.razor` | The state chips with their hover text. |
| `SkillsHelpDialog.razor`, `SkillsLearnMore.razor` | The in-app help (`G8`) and the **Learn more** link that opens it. |
| `SkillDialogs.cs`, `SkillFlows.cs` | Opening the dialogs, and the multi-step flows (Remove a Pack, Hide with Undo). |

`Components/Layout/SkillArrivalWatcher.razor` shows the "ready" snackbar when a new Pack or Skill
appears, and `Components/Layout/MoveReportAnnouncer.razor` reports the move once.

The Library — the explorer, viewer and editor, Spec §6. Paths are relative to `src/Huddle.App`:

| Path | Responsibility |
| --- | --- |
| `Library/LibraryOptions.cs` | Configuration bound from `Team:Library`. |
| `Library/LibraryRoot.cs`, `Library/LibraryPath.cs` | Records for a Library Root and a path resolved inside one. |
| `Library/LibraryRootStore.cs` | The Library Roots: `teams`, `agents` and the pinned roots, saved from Settings. |
| `Library/LibraryPathResolver.cs` | The only code that turns input into a path; every path operation goes through it. |
| `Library/LibraryFileService.cs`, `Library/AtomicFile.cs` | List, read, write, create, rename, move and recycle; every write is atomic. |
| `Library/TextFileCodec.cs` | Detects and restores encoding, BOM and line ending. |
| `Library/WikiLinkParser.cs`, `WikiLinkResolver.cs`, `WikiLinkRewriter.cs`, `WikiLinkIndex.cs` | Parse, resolve (case-insensitive), rewrite on rename, and the per-root Backlinks index. |
| `Library/LibraryReferenceResolver.cs`, `Library/LibraryPathPatterns.cs` | Turn a Library path in a chat Message or note into a link. |
| `Library/LibraryDocumentCollector.cs` | The Library documents a Turn's prompt block hands an Agent. |
| `Library/UploadKind.cs`, `Library/UploadKinds.cs` | The kinds of file an upload can be (the enum), and the classifier that reads a file's magic bytes, never its name. SVG and executables are refused here (ChatUploads spec §6.3, ADR-0053). |
| `Library/UploadNames.cs` | Cleans a browser's file name for an upload and numbers a taken name (`notes (2).txt`); a pasted screenshot gets the `pasted-image-…` name, from `TimeProvider` (spec §6.3). It only builds names: choosing the unique name on disk and removing a partial file are `LibraryFileService`'s job. |
| `Library/LibraryUpload.cs` | The record `LibraryFileService.CreateFromStreamAsync` returns: the saved path, its kind and its size (spec §6.2). |
| `Uploads/ChatUploadService.cs` | Saves one attached file for a Room: picks the Room's folder from its Scope, streams the bytes under the size cap and removes a refused partial file (spec §6.4). |
| `Uploads/TaskUploadFolders.cs`, `Uploads/TaskUploadService.cs` | A Task's upload folder, `uploads:/Tasks/<ID>`, and the service that saves one attached file there through the chat upload's byte writer (TaskAttachments spec §6.1, §6.2). |
| `Tasks/ITaskBacklinkSource.cs`, `TaskBacklinkSource.cs`, `TaskBacklink.cs` | The open Tasks that link to a Library document, for the Backlinks panel: an attached path line or a `[[wikilink]]` in the description. `WikiLinkIndex` skips `_tasks/`, so it cannot see them. |
| `Tasks/TaskAttachmentText.cs` | Appends a file's path as a code-span line to a Task description, once, with a blank line only after prose (TaskAttachments spec §6.4). |
| `Components/Library/LibraryFilePickerDialog.razor` | Picks one Library file by name search or by browsing; a folder only opens (TaskAttachments spec §6.3). |
| `Uploads/PendingUploads.cs` | The composer's chips: one `PendingUpload` per attachment, with its state (uploading, done, refused) (spec §6.4). |
| `Uploads/RoomUploadFolders.cs` (`IRoomUploadFolders`) | A Room's upload folders, resolved through the Library's path resolver. Used by Room delete to recycle them (spec §6.9). |
| `Library/TeamFolderCatalog.cs`, `Library/TeamFolderProvisioner.cs` | List and create Team and Project folders. |
| `Library/ITeamFolders.cs` | Creates the folders behind a Team and its Projects on demand for the Team pages (Spec §6.5, §6.6). Every refusal is a `LibraryResult<T>` with a user-facing reason; nothing is ever renamed or deleted. |
| `Library/LibrarySearchResult.cs` | Search results from a Library query, including the hits and whether more results were truncated. |
| `Library/IRecycleBin.cs`, `Library/WindowsRecycleBin.cs` | Delete to the Recycle Bin; `NotAvailableRecycleBin.cs` off Windows. |
| `Library/LayoutGuard.cs` | Validates that `Teams:Dir` and `Acp:AgentsDir` do not overlap. |
| `Library/LibraryLocation.cs` | The public record a host passes to say which folder the explorer starts from. |
| `Library/LibraryLinkQuery.cs` | Pure text handling for a `?library=` link and the `/library` page address it opens. |
| `Components/Library/LibraryExplorer.razor` | The reusable control: tree + document, scoped by its `Scopes` parameter. |
| `Components/Library/LibraryTree.razor`, `LibraryDocument.razor`, `LibraryEditor.razor`, `BacklinksPanel.razor`, `LibraryFileOps.razor` | The explorer's parts; file operations open `RenameDialog`, `MoveDialog` and `NamePromptDialog`. |
| `Components/Library/LibraryNavLink.razor` | The sidebar's Library link. |
| `Components/Library/LibraryLinkRouter.razor` | Sends a `?library=` link to the Library page; renders nothing. |
| `Components/Pages/Library.razor` | The `/library` page: the only Library surface, one Tab. |
| `Components/Settings/LibraryPanel.razor` | Pinned roots editor. |
| `Library/LibraryFilesEndpoint.cs` | HTTP endpoint serving `/library-files/…` for images inside notes. |
| `Acp/AgentPaths.cs` | Definition file, Agent folder and Work Dir for a Name. |
| `Acp/AgentLayoutMigration.cs` | The one-time move from `Teams/` + `work/` to `Agents/`. |
| `wwwroot/library-editor.js` | CodeMirror 6 interop module: the editor, `Mod-s`/`Mod-b`/`Mod-i`/`Mod-e`, the `[[` completion source and the Markdown toolbar's listeners. |
| `wwwroot/markdown-commands.js` | The toolbar commands and the `[[` pattern as pure text transforms (no CodeMirror, no DOM), so Node tests can run them. |
| `Components/Library/MarkdownToolbar.razor` | The Markdown formatting row above the editor (Edit and Split); markup only, wired by `library-editor.js`. |
| `tools/codemirror/` | Pinned CodeMirror packages and the esbuild script that builds `wwwroot/lib/codemirror/codemirror.bundle.js` (ADR-0028). |

References — the `@` and `#` picker in the Composer, the reference snapshot and the Referenced items header, `docs/Huddle.AtTagging-Specifications.md`. Paths are relative to `src/Huddle.App`:

| Path | Responsibility |
| --- | --- |
| `References/ReferencesOptions.cs` | The `References:` configuration keys, read once: `ExpandInPrompt` and `MaxPromptChars`. |
| `References/ReferenceLimits.cs` | The constants: 10 references per Message, 60-character labels, 120 per header value, the token length. |
| `References/ReferenceText.cs` | The pure text functions: Room and Team addresses, labels, escaping and the text a pick writes. |
| `References/RoomLinkText.cs` | The Copy link text for a Room, built by `ReferenceText` so it matches the picker. |
| `References/ReferenceQueryParser.cs` | Reads one picker token into its filter, phrase and whether it may search. Pure. |
| `References/ReferencePickerModel.cs` | Turns the picker's state into groups, rows, the footer and the live line, with no renderer. |
| `References/ReferenceSearch.cs` | Runs one picker search under the Reference purpose and drops late results. |
| `References/ReferenceCandidate.cs` | A reference the scanner found in a Message's text, before it is checked. |
| `References/ReferenceScanner.cs` | Finds the references in a Message's text through the chat Markdown pipeline. |
| `References/ResolvedReference.cs` | A reference after the resolver looked it up: a Task, Room, Team or Library file, or a gone reference. |
| `References/ReferenceResolver.cs` | Looks up each candidate now against the stores. |
| `References/IReferenceSnapshotter.cs`, `ReferenceSnapshotter.cs` | Builds a Message's snapshot at post time. A failure never blocks a post. |
| `References/ReferencedItemsFormatter.cs` | Writes the Referenced items header, quoting every untrusted value. |
| `References/ReferenceExpander.cs` | Expands a Turn's trigger Message into the header text. |
| `Components/References/ReferencePicker.razor` | The picker list drawn over the Composer. |
| `Components/References/MessageReferences.razor` | The reference row under a Message that has a snapshot. |

Global Search — the search box in the Top Bar, its dropdown and the providers behind it, `docs/Huddle.GlobalSearch-Specifications.md`. Paths are relative to `src/Huddle.App`:

| Path | Responsibility |
| --- | --- |
| `Search/SearchQuery.cs` | The parsed query: its filter, the trimmed text and whether it is long enough to search; an optional list of the only Rooms the Rooms provider may open. |
| `Search/SearchQueryParser.cs` | Reads a leading prefix (`team:`, `agent:`, `lib:`, `task:`, `room:`) from the text. Pure. |
| `Search/SearchFilter.cs` | The filters: All, Teams, Agents, Library, Tasks and Rooms. |
| `Search/SearchScope.cs` | The Active or Archived pass of one search. |
| `Search/SearchOptions.cs` | The `Search:` configuration keys, read once. |
| `Search/SearchLimits.cs` | The constants: 5 cards per group under All, 50 for one filter. |
| `Search/SearchContentPolicy.cs` | Which Library entries have their contents read: the extension list and the 1 MB limit. |
| `Search/SearchHit.cs` | A hit as data: the base record and one subtype per kind of hit. |
| `Search/SearchHitLinks.cs` | The address each kind of hit opens. |
| `Search/SearchMatch.cs` | What a hit matched on, and where, for the bold text. |
| `Search/SearchSnippet.cs` | The one-line snippet around a content match. |
| `Search/SearchOpenTeam.cs` | The Team a hit belongs to, when it has one. |
| `Search/MatchTier.cs` | The provider's coarse judgement of how well a hit matches. |
| `Search/SearchResult.cs` | One provider's result: its hits, and whether it was truncated or failed. |
| `Search/ISearchProvider.cs` | The contract every provider implements. |
| `Search/SearchResultRanker.cs` | Orders hits by tier, then by name. Pure. |
| `Search/SearchOrchestrator.cs` | Runs the providers for the Active pass, then the Archived pass, and reports each group as it arrives. |
| `Search/SearchGroupState.cs` | One group while a search runs: searching, with its result, or failed. |
| `Search/SearchDropdownModel.cs` | The dropdown's view of the groups, their headings and the result count. |
| `Search/TeamSearchProvider.cs` | Searches Teams. |
| `Search/AgentSearchProvider.cs` | Searches Agents. |
| `Search/LibrarySearchProvider.cs` | Searches the Library by name, then by contents. |
| `Search/TaskSearchProvider.cs` | Searches Tasks, Closed Tasks included on the Archived pass. |
| `Search/RoomSearchProvider.cs` | Searches Rooms by name and Message text, Archived Rooms on the Archived pass. |
| `Components/Search/SearchBar.razor` | The box, the chips, the dropdown and the keyboard for them. |
| `Components/Search/SearchCard.razor` | Picks the card for a hit by its kind. |
| `Components/Search/TeamSearchCard.razor` | The card for a Team hit. |
| `Components/Search/AgentSearchCard.razor` | The card for an Agent hit. |
| `Components/Search/LibrarySearchCard.razor` | The card for a Library hit, with its snippet. |
| `Components/Search/TaskSearchCard.razor` | The card for a Task hit. |
| `Components/Search/RoomSearchCard.razor` | The card for a Room hit, one per Room. |
| `Components/Shared/AgentProfileCardOpener.cs` | Opens the Agent card the same way from search and from the Agents page. |

Needs You — the sidebar card of Rooms waiting on the Human, `docs/Huddle.NeedsYou-Specifications.md`. Paths are relative to `src/Huddle.App`:

| Path | Responsibility |
| --- | --- |
| `NeedsYou/NeedsYouReason.cs`, `NeedsYouEntry.cs` | The four reasons (`Mention`, `Question`, `Form`, `Budget`) as flags, and the record the tracker reports per Room. |
| `NeedsYou/NeedsYouRule.cs` | The pure rule for an unanswered Mention, shared by the live tracker and the startup scan. |
| `NeedsYou/NeedsYouTracker.cs` | Singleton, in memory, keyed by Room id. Observes `RoomEvents` and the Question and Form stores; owns the Mention and Budget reasons, mirrors the other two, and holds Dismiss. |
| `NeedsYou/NeedsYouStartupScan.cs` | Hosted service that reads the last 50 Messages of every Room once at start and seeds the tracker's Mention reason. |
| `Components/NeedsYou/NeedsYouList.razor` | The rows, with no chrome and no idea where it is placed; reports its count through `CountChanged`. |
| `Components/NeedsYou/NeedsYouCard.razor` | The left sidebar's card around the list; hidden while the count is 0. Placed in `MainLayout.razor` above `RoomList`. |
| `Data/IChatStore.cs`, `FileChatStore.cs` | `ReadTailAsync`: the last N Messages, read from the end of the Transcript file. |
Feed — one stored history of what Agents and Huddle did, `docs/Huddle.Feed-Specifications.md`, ADR-0063. Paths are relative to `src/Huddle.App`:

| Path | Responsibility |
| --- | --- |
| `Feed/IFeedPublisher.cs`, `FeedMessage.cs`, `FeedActor.cs` | What a producer calls (`Post`, `Resolve`) and the item it describes. Both only enqueue and never throw. |
| `Feed/FeedSourceType.cs`, `FeedSourceTypes.cs`, `FeedPayload.cs` and the `*Payload.cs` records, `FeedPayloads.cs`, `FeedEnvelope.cs` | The source types with their wire names, the one payload record per type, and the JSON envelope in the `payload` column. A row this build cannot read comes back with a null payload. |
| `Feed/FeedCategory.cs`, `FeedCategories.cs` | The Category control's seven values (Helpers is the seventh) and the one Category each source type belongs to. |
| `Feed/FeedQueue.cs`, `NullFeedPublisher.cs`, `FeedCommand.cs` and its records | The bounded channel behind `IFeedPublisher`, and the commands the writer applies (post, resolve, mark seen, dismiss, mark Tab seen). |
| `Feed/FeedWriter.cs` | The only code that writes `feed_events`: batches in one transaction, fills a Room's name and Team in, raises `FeedEvents` after commit, runs the clean-up on a timer. |
| `Feed/FeedStore.cs`, `FeedItem.cs`, `FeedQuery.cs`, `FeedCounts.cs`, `FeedEvents.cs`, `FeedOptions.cs` | The table in `team.db`, its keyset-paged reads, the hub the UI listens to, and the `Feed:*` options. |
| `Tasks/TaskFeedRules.cs`, `Tasks/TaskFeedStartup.cs`, `Tasks/IRequestConfirmers.cs`, `Tasks/RequestConfirmers.cs` | The pure rule that turns a Task change into items and resolutions, the startup pass that resolves what ended while Huddle was stopped, and the shared "can an Agent confirm this Team's Request" check. |
| `NeedsYou/NeedsYouTracker.cs`, `NeedsYou/NeedsYouStartupScan.cs`, `Services/ChatService.cs`, `Teams/TeamRoomReconciler.cs`, `DocHistory/DocHistoryStore.cs` | The producers; each posts after its own write. `ChatService` takes an optional `FeedActor` on its Room-lifecycle methods (internal overloads). |
| `Components/Feed/FeedSummary.cs`, `FeedTime.cs`, `FeedGrouping.cs` and their records | The interface copy, ages, day headings and the grouping of neighbouring items; pure. |

Helpers — background agents that make one stateless judgement and let Huddle apply the Verdict, `docs/Huddle.Helpers-Specifications.md`. Paths are relative to `src/Huddle.App`:

| Path | Responsibility |
| --- | --- |
| `Helpers/IHelperAgent.cs`, `IHelperAgent{TInput,TVerdict}.cs`, `HelperId.cs`, `HelperDefaults.cs`, `HelperApplied.cs`, `HelperOutcome.cs`, `HelperRegistry.cs` | The contract: a Helper has an id, a display name, prompt keys and code defaults; it fingerprints and renders its input, validates a typed Verdict and applies it through a domain service. `HelperRegistry` refuses a duplicate id at start-up. |
| `Helpers/HelperRunner.cs`, `IHelperProcessRunner.cs`, `HelperSessionRequest.cs`, `HelperTurn.cs`, `HelperSessionMeta.cs` | One call: resolve the Adapter, take the call from the budget, run a fresh adapter process under the Helper's timeout, take the text from the first `{` to the last `}` and parse it. Every failure is an outcome, never an exception. `HelperSessionMeta` is the `_meta` that makes a Claude session toolless (no built-in tools, no claude.ai connectors, no MCP servers beyond the empty set); **without it a call costs about 29 times more and the model can see the account's connectors** — see `acp-session-config.md`. |
| `Acp/AdapterProcessHelperRunner.cs`, `Acp/ThrowawayAdapterSession.cs`, `Helpers/HelperPermissionHandler.cs` | The production `IHelperProcessRunner`: replaced system prompt, no tool server, a permission handler that refuses and counts every request, and a Model the session does not list is refused rather than silently run on the Adapter's default. `ThrowawayAdapterSession` is the launch-and-dispose skeleton it shares with `AdapterProcessProbeRunner`. [Rules](rules.md) forbids any test reaching the real runner. |
| `Helpers/HelperDispatcher.cs`, `HelperInvoker.cs`, `IHelperInvoker.cs`, `HelperRunReport.cs`, `HelperRunKind.cs` | The queue: waits per key (a save restarts the wait, never past six times it), one run per key, a concurrency limit (`Team:HelperConcurrency`), the enabled and `Team:Acp:Enabled` gates, a per-Helper cache of `Ok` Verdicts by content, and the result stored on the card. `HelperInvoker<TInput,TVerdict>` is the typed half, so the dispatcher never learns a Helper's types. |
| `Helpers/HelperBudget.cs`, `HelperUsage.cs`, `Data/HelperUsageStore.cs` | The daily call cap on the Human's local day: a call counts when it is reserved, before the process starts; a paused Helper counts skipped inputs; Continue grants calls for today only. |
| `Helpers/HelperSettingsResolver.cs`, `HelperSettings.cs`, `HelperSettingsRow.cs`, `HelperOptions.cs`, `Data/HelperSettingsStore.cs` | The three layers: code defaults, then `Team:Helpers:<Id>`, then the Human's row, which is all or nothing (a null Model is the Adapter's own default, never the configured one). Read at each fire, never cached. |
| `Helpers/HelperStatus.cs`, `Data/HelperStatusStore.cs` | The last run, last result and last error per Helper, kept across a restart. |
| `Helpers/NeedsHuman/NeedsHumanTrigger.cs` | Watches `TaskEvents`: records where the Human turned the gate off, and queues a judgement for an Agent's create or title, description or Action change on an open, ungated Task whose content really changed. Does only in-memory work and swallows its own failures, because `TaskEvents` lets a throwing handler fail the saver. |
| `Helpers/NeedsHuman/NeedsHumanHelper.cs`, `NeedsHumanInput.cs`, `NeedsHumanVerdict.cs`, `NeedsHumanJsonContext.cs` | The first Helper. `ApplyAsync` re-reads the Task and drops a stale Verdict (gone, already gated, closed, overridden, saved again since, Helper off), then turns the gate on through `TaskService.Update` as `Needs Human (Helper)`. `NeedsHumanVerdict.NeedsHuman` is `[JsonRequired]`: a missing answer is unparseable, never a quiet "no". |
| `Helpers/HelperFeed.cs`, `HelperFeedStartup.cs`, `Feed/HelperPausedPayload.cs`, `HelperFailingPayload.cs` | Tells the Feed when a Helper pauses at its cap (`helper-paused`, once per pause) or fails three times in a row (`helper-failing`, once per streak) and resolves each when the condition ends; the dispatcher and the Helpers card only report what happened. `HelperFeedStartup` reconciles once at startup. ([Helpers in the Feed](../Huddle.HelpersInFeed-Specifications.md)) |
| `Data/NeedsHumanOverrideStore.cs` | The Tasks the Human turned the gate off on, loaded into memory and written through. |
| `Components/Settings/Helpers/HelpersPanel.razor`, `HelperCard.razor`, `HelpersCopy.cs` | Settings › Helpers (wireframes `G9`, `G9a`) and every string on it. |
| `Components/Shared/AdapterModelEffortPicker.razor` | The Adapter, Model and Effort cascade as one control (ADR-0015, rules.md). Used by the Helper card; `AgentProfileCard` still has its own copy of the same rules. |
| `Components/Feed/FeedHelperPreview.razor` | The read-only Helper preview in an expanded Feed card (state, what it runs on, today's usage, last result and error). |
| `Components/Tasks/HelperGateNote.cs` | The Task panel line "Turned on by the Needs Human Helper: …", from the latest `human gate on` Change log entry. |

Human gate — a Task that names an Action, or requires the Human's acceptance, waits in Needs You until its Proof is complete and the Human decides, `docs/Huddle.HumanGate-Specifications.md`. Paths are relative to `src/Huddle.App`:

| Path | Responsibility |
| --- | --- |
| `Tasks/HumanGate/HumanGateRules.cs` | The pure rules H-1 to H-5 that keep a gated Task from skipping the Human, for Human and Agent actors. `TaskService` calls them before a write. |
| `Tasks/HumanGate/Acceptance.cs`, `AcceptanceInfo.cs`, `AcceptanceState.cs` | Reads from the Change log whether the Human's acceptance still counts: `None`, `Valid` or `Stale`. |
| `Tasks/HumanGate/ContentFingerprint.cs` | The hash an acceptance is bound to: title, Action and description. |
| `Tasks/HumanGate/ProofEvaluator.cs` | Pure: turns a Team's Definition of Done and a `ProofContext` into a `Proof`. Merges the ADR-0039 gates once; an Unreadable item is never met; an empty list is complete. |
| `Tasks/HumanGate/Proof.cs`, `ProofItem.cs`, `ProofDocument.cs`, `ProofContext.cs` | The Proof and its lines (public, they reach component parameters), and the gathered input of the evaluator. |
| `Tasks/HumanGate/ProofContextBuilder.cs`, `ProofSource.cs`, `IProofSource.cs` | Gather a Task's `ProofContext` from the live stores and evaluate it; the interface is what the components and the tools take. |
| `Tasks/HumanGate/TaskDecision.cs` | The Human's three decisions: accept, request changes, decline. |
| `Teams/DoneItem.cs`, `DoneRule.cs`, `ProofItemSource.cs` | One line of a Definition of Done and its closed set of four rules (ADR-0062); where a Proof item came from. `ProcessDocument` parses the section. |
| `NeedsYou/NeedsYouTaskSource.cs`, `NeedsYouTaskEntry.cs` | The gated Tasks waiting on the Human. Holds no list: every read goes to `TaskStore`, so a restart loses nothing (ADR-0050 amended). |
| `Tasks/TaskService.cs` (`Decide`, `OnOutsideEdit`) | Applies a decision with its precondition and version check, ignores `humanOverridesGates` on gated Tasks, and reverts and logs an outside edit that the gate forbids. |
| `Components/Tasks/HumanGateCopy.cs` | Every user-facing string of the gate, in one place; the components and tests share it. |
| `Components/Tasks/TaskHumanGate.razor` | The Human gate section of the Task panel: switch, Action field, helper line, Done rule chip. |
| `Components/Tasks/TaskProofList.razor` | The Proof list with the `Ask <owner>` button that posts a Mention in the Task's Room. |
| `Components/Tasks/TaskDecisionCard.razor` | The decision card: Proof count, locked notice, Accept, Request changes and Decline. |
| `Components/Tasks/TaskDecisionDialog.razor` | The card as a dialog, opened when a gated card with complete Proof is dropped on Done (board). |
| `Components/NeedsYou/NeedsYouPage.razor` | The Needs you page at `/needs-you`: Task rows and Room rows, with its empty state. |

Tabs — the open pages in the strip above the content, `docs/Huddle.Tabs-Specifications.md`. Paths are relative to `src/Huddle.App`:

| Path | Responsibility |
| --- | --- |
| `Tabs/Tab.cs` | The `Tab` (with its pinned flag), `TabGroup` and `TabDescriptor` records. |
| `Tabs/TabKeys.cs` | Turns a URL into a tab key, title and icon. |
| `Tabs/TabService.cs` | The open Tabs in up to `MaxGroups` groups: preview, permanent and pinned Tabs, focus, move between groups, close, and the snapshot. |
| `Tabs/TabSnapshot.cs` | The stored JSON form of the open Tabs. |
| `Tabs/TabReconciler.cs` | Retitles a Room's Tab with the Room and closes it when the Room is archived or deleted. |
| `Tabs/TabIcons.cs` | The icon for each kind of Tab, and the two move icons of the Tab menu. |
| `Tabs/MaterialSymbols.cs` | Material Symbols that MudBlazor's icon set lacks, rescaled to its 24dp grid. |
| `Tabs/TabPageRoutes.cs` | Resolves a Tab's URL to its page and parameters from the `@page` templates, without the router; also `TabPage` and the cascaded `TabPageContext`. |
| `Components/Layout/TabHost.razor` | Draws the groups side by side, follows every location the router reaches, and takes the keyboard shortcuts. |
| `Components/Layout/TabGroupPane.razor` | One group: its strip over the page of its active Tab; focuses itself when pressed. |
| `Components/Layout/TabPageHost.razor` | Mounts one Tab's page (a `DynamicComponent`) from its URL. |
| `Components/Layout/TopBar.razor` | The fixed bar across the top of the window: the focused pane's breadcrumb trail, with cells reserved for a search box and toolbar buttons. |
| `Components/Layout/TabStrip.razor` | One group's strip of Tabs, with the right-click Tab menu (pin, move left, move right, close). |
| `Components/Layout/ActivityBar.razor` | The Activity bar (`S5`): one button per Sidebar view, pressed while selected, and a `Bottom` slot. Holds no state; `MainLayout` owns the selected view. The toggle rule and the list of views are in `SidebarView.cs` (`SidebarViews.Next`, `SidebarViews.Available`). |
| `Components/Layout/NavTabLink.razor` | A left-menu link that opens a Tab (single click previews, double click keeps). |
| `Components/Layout/TabSync.razor` | Keeps a Room's Tab title and lifetime in step with the Room, and closes the Tab of a removed Team or Project. |
| `Components/Layout/TabPersistence.razor` | Restores the stored Tabs after the first render and saves every change (`huddle:tabs`). |
| `Circuits/ICircuitConnection.cs` | Whether the browser behind a circuit is still connected. |
| `Circuits/CircuitConnectionTracker.cs` | The circuit's `CircuitHandler`: follows the framework's connection callbacks and answers `ICircuitConnection`. |
| `Circuits/ServerNavigator.cs` | Navigates for a handler that a server event started and skips the call once the browser is gone; `TabSync` and `Chat` use it. |
| `Circuits/CircuitServiceCollectionExtensions.cs` | `AddCircuitConnection`: registers one scoped tracker as both the handler and the connection. |

`src/Huddle.MockAdapter` — an Adapter this solution builds, assembly `mock-acp`:

Test infrastructure, not a stand-in. It exists so every Huddle-side path has an automated test
needing no Node, no subscription, no GPU and no second repository. It must never grow Huddle
behaviour of its own: it answers the protocol and records what it received, and any temptation to
make it *clever* means the assertion belongs in a Huddle test instead.

| Path | Responsibility |
| --- | --- |
| `Program.cs` | Runs the mock over stdin/stdout as a real child process. **stdout carries protocol bytes only** — every diagnostic goes to stderr, because one stray `Console.WriteLine` corrupts the JSON-RPC frame. Accepts `--script <path>`, parsed and deliberately inert, reserving the surface for a V2 script format. |
| `DuplexStream.cs` | Presents a process's separate stdin and stdout as the one `Stream` `FakeAcpAgent` takes. Its `CanRead`/`CanWrite`/`CanSeek` report `false` once disposed rather than throwing, per the `Stream` contract — a dispose-path helper is entitled to probe them. |
| `MockBehaviour.cs` | The default prompt handler: echoes the prompt back as **several** chunks, then `end_turn`. Chunked because it proves the thing most likely to be wrong — that text arrives incrementally, becomes a `MessageDelta` on the pipe, and renders live in a Room. |
| `MockElicitation.cs` | The five forms `mock-acp` can put to the Human, so the Elicitation card can be exercised without a real model. A prompt containing `[elicit:ask]` (two `AskUserQuestion` questions), `[elicit:one]` (one), `[elicit:form]` (an MCP-style typed form: date, integer, number, boolean, enum, multi select, text), `[elicit:refusal]` (the retry-after-refusal dialog) or `[elicit:unsupported]` (a nested-object schema the app declines) makes `MockBehaviour` send one `elicitation/create` before it echoes, then reply `Got action=<action> content=<json or none>`. It needs `elicitation.form` advertised, which the app does by default |
| *(linked)* `Fakes/FakeAcpAgent.cs`, `PromptContext.cs`, `FakeRpcError.cs` | **Linked, not copied**, from `tests/Huddle.Acp.Tests/Fakes/` with `<Compile Include=… Link=…>`. No file leaves the ACP effort's subtree and no `internal` widens — a linked file compiles into this assembly as its own. One implementation: their fake is that effort's statement of what an ACP agent does, and the mock inherits it. **Editing those files changes two assemblies.** |

`src/Huddle.SkillPacks` — the Skill Packs library, assembly `Agency.Huddle.SkillPacks`. It references
Markdig only and knows nothing of the app: the same code reads `SKILLS.md` for Huddle and for
`huddle-packs`, so the two can never disagree about a file.

| Path | Responsibility |
| --- | --- |
| `IFileReader.cs`, `PhysicalFileReader.cs`, `IFolderHasher.cs` | The only disk seam. The library reads through it and never writes. |
| `InstallContext.cs` | `install-context.json`: `Parse` and `ToJson`. |
| `Reading/RegistryReader.cs`, `RegistryRow.cs`, `PathEntries.cs` | Reads the Packs and Catalogs tables of `SKILLS.md` into rows; a per-computer path (`DESK-01: …<br>LAPTOP: …`) resolves for this computer's name, ignoring case. |
| `Files/PackFile.cs`, `CatalogFile.cs`, `SourceFile.cs`, `SchemaFiles.cs`, `JsonFields.cs` | The three file formats, each with `Parse`, and their embedded JSON Schemas. |
| `Resolving/RegistryResolver.cs`, `ResolvedRegistry.cs`, `SourceId.cs`, `InstalledProvenance.cs` | Rows to Packs, catalogs, Skills and Sources, without states. |
| `Checking/Checks.cs`, `CheckCatalog.cs`, `CheckContext.cs`, `CheckResult.cs`, `Finding.cs`, `SecretScanner.cs` | `Checks.Run`: every check once, each finding with its code (`R`, `P`, `D`, `S`, `O`, `X`) and the fix hint from `CheckCatalog`, which `InstallSkillPack.md` prints as its table. |
| `Folders/SkillFolderValidator.cs`, `SkillFolderRules.cs`, `SkillFrontmatter.cs`, `SkillFolderHash.cs`, `SkillReviewScanner.cs` | One Skill folder: its `SKILL.md`, its limits, its hash (and which files changed), and what it does that a Human should review. Moved here from `src/Huddle.App/Skills/`. |

`src/Huddle.Packs` — `huddle-packs`, the command an AI tool runs to check its work. Console exe;
references `Huddle.SkillPacks` only.

| Path | Responsibility |
| --- | --- |
| `Program.cs`, `ArgumentParser.cs`, `HelpText.cs` | `Program.Run` and hand-rolled parsing: `validate`, `roles`, `installed`, `skills`, `schema`; exit 0 with no errors, 1 with errors, 2 when it could not run. |
| `DataLocator.cs` | Finds Huddle's data folder (`--data-dir`, else `HUDDLE_DATA_DIR`, else `App_Data`) and its `install-context.json`. |
| `FindingWriter.cs` | One line per finding, or `--json`. |
| `Commands/ValidateCommand.cs`, `Commands/ListCommands.cs` | `validate` calls `Checks.Run` exactly as the app does; the list commands read the context file. |
