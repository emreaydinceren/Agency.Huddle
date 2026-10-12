# Huddle.GlobalSearch — Design Specification

**Date:** 2026-10-12 · **Status:** Built 2026-10-12; the Top Bar it lives in is built (PR #162,
`4864ef6`) · **Owner:** chat surface · **Decision records:**
[ADR-0046](adr/0046-a-group-mounts-its-own-page-and-the-url-names-the-focused-one.md) (its addendum
records the Top Bar) and [ADR-0050](adr/0050-search-keeps-no-index-of-its-own.md) for *search keeps no index of its own*
(written) · **Vocabulary:** [language.md](engineering/language.md) (**Team**, **Teammate**,
**Persona**, **Title**, **Alias**, **Library**, **Library Root**, **Task**, **Closed**, **Room**,
**Archived**, **Message**, **Transcript**, **Turn**) · **Builds on:**
[Tabs](Huddle.Tabs-Specifications.md) (the Top Bar and its breadcrumb),
[Library](Huddle.Library-Specifications.md) (`FindAsync`, the hiding rules),
[Tasks](Huddle.Tasks-Specifications.md) (the Task index)

> [!IMPORTANT]
> Binding before any code: [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Read
> [agents/MudBlazorImplementation.md](../agents/MudBlazorImplementation.md) before building the UI
> and [agents/Testing.md](../agents/Testing.md) before writing a test. Nothing here overrides them.
> Build and test the **solution**, with the trailing `--`: `dotnet build Huddle.slnx` then
> `dotnet test Huddle.slnx --`.

This is the design for **Global Search**: one search box in the Top Bar above everything, in the
manner of VS Code's quick open. The Human types, and a dropdown under the box fills, group by group,
with matching Teams, Teammates, Library entries, Tasks and Rooms. The product owner's brief was
*"nothing fancy, no index, just grep style"*: every search reads what it needs again, from memory or
from disk, and search keeps nothing between searches.

Every design decision below was settled by the product owner, in a brainstorm on 2026-10-12 and in
answers to its open points the same day. They are recorded in §13 and §15 with the reason for each.
One point, OP-2, is **assumed** and awaits the owner's confirmation.

---

## Contents

1. Goal · 2. Non-goals · 3. Vocabulary · 4. What exists today · 5. The shell: the Top Bar ·
6. The dropdown · 7. The search pipeline · 8. Providers · 9. Opening a hit, and the Room jump ·
10. Configuration · 11. Accessibility · 12. Testing · 13. Decisions · 14. Version 2 ·
15. Open points · 16. Blast radius · 17. Delivery plan

---

## 1. Goal

1. **One box finds anything.** Teams, Teammates, the Library, Tasks and Rooms, from a bar the
   window owns, not any page.
2. **Grep style; search keeps no index of its own.** Exact phrase, ignoring case, re-read on every
   search (D-6, D-21).
3. **Fast things first.** Each provider's results appear as soon as that provider finishes, so
   Teams and Teammates show at once while the Library and the Rooms are still being read (D-17).
4. **One failure is one group.** A locked folder or an unreadable Transcript empties one group with
   a message; the rest still render (D-20).
5. **The keyboard is enough.** Open, filter, move, open a hit and close without the mouse (§6.5).

## 2. Non-goals

Out of scope for version 1, by the owner's decision:

| Not in V1 | Note |
| --- | --- |
| Fuzzy matching | Matching is the exact phrase (D-6) |
| Highlighting beyond bolding the match | The match in a snippet is bold, nothing more |
| Search history and recents | Recents are V2 (§14) |
| Anything that writes | Search reads only; the card's action slot stays empty (D-12) |
| An index of its own | Every search re-reads (D-21) |
| A full results page | A single-filter dropdown is the "see all" (D-9) |
| Searching Sprints | Not planned, in V1 or V2 (§14) |
| Commands (`>`) and actions | V2 (§14) |
| `?` listing the prefixes | V2 (§14; OP-14) |

---

## 3. Vocabulary

The defined terms keep their meanings from [language.md](engineering/language.md). This spec adds
the following; the plan's D14 adds the first four to the glossary (OP-13).

| Term | Meaning |
| --- | --- |
| **Global Search** | The feature: the box in the Top Bar and its dropdown |
| **Top Bar** | The fixed bar across the top of the window (`TopBar.razor`), above the left menu and every pane. It holds the breadcrumb trail and the search box. The window owns it, not a Tab. Built (PR #162) |
| **Filter** | One of All, Teams, Teammates, Library, Tasks, Rooms. Drawn as a chip; the same thing as a prefix |
| **Card** (search sense) | One result row in the dropdown, drawn by a component chosen by the kind of hit. Not a Board's card for a Task, nor the Teammate card |
| **Prefix** | `team:`, `teammate:`, `lib:`, `task:` or `room:` typed at the start of the query. Picking a chip and typing its prefix are the same act |
| **Group** | The results of one provider in the dropdown, under a heading |
| **Hit** | One result as data: a `SearchHit` subtype (§7.1) |
| **Provider** | The code that searches one category: an `ISearchProvider` (§7.3) |
| **Pass** | One provider's search returning its result. Never called a "turn": a **Turn** is an agent reply |
| **Active / Archived scope** | The first and the second search of the same query. Archived means Archived Rooms and Closed Tasks (§7.5) |
| **Tier** | The provider's coarse judgement of how well a hit matches (`MatchTier`, §7.4) |

The filter labels follow the glossary (OP-1, decided): **Teammates**, not "Members" (a Member is a
Teammate in a Room), and **Rooms**, not "Chats" (the glossary avoids "chat" for a Room).
Interface copy says **Library**, never "documents" or "files" (D-5).

---

## 4. What exists today

Verified against `main` at `4864ef6` on 2026-10-12. The implementer should not need to search for
these.

| Fact | Where |
| --- | --- |
| **Library name search exists, for one scope only.** `FindAsync(LibraryPath scope, string term, int max, CancellationToken ct)` walks breadth-first through `ListAsync`, so every tree hiding rule applies, matches the last path segment with `OrdinalIgnoreCase`, stops after `max` hits or after `LibraryOptions.MaxIndexedFiles` visited entries, and returns `LibrarySearchResult(Hits, Truncated)`. A folder that cannot be listed is skipped. | `src/Huddle.App/Library/LibraryFileService.cs:207` |
| `LibrarySearchResult` is an `internal sealed record` of `LibraryEntry` hits. **Global Search does not rename or reuse it as its own result type** (D-16). | `src/Huddle.App/Library/LibrarySearchResult.cs:4` |
| `FindAsync`'s one caller is the Library explorer's own search box (200 hits at most, across its scopes). It stays as it is. | `src/Huddle.App/Components/Library/LibraryExplorer.razor:274` |
| **There is no content search anywhere.** Nothing reads a file's text to match a term. | — |
| **Search keeps no index of its own.** The one index in the Library is `WikiLinkIndex`, an in-memory index of wikilinks per root, which refuses to build above `MaxIndexedFiles` (default `5000`); it holds links, not text, and search does not use it. In `FindAsync` the same key is only a per-walk visit budget. Global Search gets its own budget, `Search:MaxVisitedEntries` (§10, OP-2). | `src/Huddle.App/Library/LibraryOptions.cs:21`, `src/Huddle.App/Library/WikiLinkIndex.cs:14` |
| Hidden folders: `.obsidian`, `.trash`, `.git`, `FileChanges:Ignore`, and under the Teams root any `_`-prefixed folder. So `_tasks/` and `_closed/` are never walked under the Teams root. | `src/Huddle.App/Library/LibraryHiddenFolders.cs:18` |
| `LibraryPathResolver` classifies a path by its **full path** under the Teams and Teammates folders, whatever root reached it, so a Persona file is `LibraryNodeRole.TeammateDefinition` even through a pinned root, and a `_`-prefixed segment under the Teams folder is refused there too. | `src/Huddle.App/Library/LibraryPathResolver.cs:305-333` |
| A Task file is recognised by `TaskLayout.TryMap`: `<Team>/[<Project>/]_tasks/[_closed/]<ID>.md` under `Team:Teams:Dir`. | `src/Huddle.App/Tasks/TaskLayout.cs:22` |
| A Persona file is a Teammate folder's definition, `Teammates/<Name>/<Name>.md`, which the resolver marks `LibraryNodeRole.TeammateDefinition`. | `src/Huddle.App/Library/LibraryPathResolver.cs:393` |
| Tasks are in memory: `TaskStore.All`, Active and Closed (`TaskItem.Location.Closed`). `TaskQuery.MatchesSearch` already matches id and title with `OrdinalIgnoreCase` for Views. | `src/Huddle.App/Tasks/TaskStore.cs:131`, `src/Huddle.App/Tasks/Views/TaskQuery.cs:149` |
| Teams are in memory: `ITeamCatalog.Teams`, each a `TeamSummary(Name, Projects, Members, HasFolder)` with `TeamType` (a `VersionedRef`) from `_team.md`. A Team's identity is its Name, compared ignoring case. | `src/Huddle.App/Teams/ITeamCatalog.cs`, `src/Huddle.App/Teams/TeamSummary.cs` |
| Personas are in memory: `PersonaEntry(Name, Title, Alias, Teams, Path, Text, Adapter, Skills, Builtin)`, assembled by `PersonaIndex.Build`. | `src/Huddle.App/Acp/PersonaEntry.cs:22`, `src/Huddle.App/Acp/PersonaIndex.cs` |
| A Room is `Room(Id, Name, Created) { Archived }` from the Team Directory (`ITeamDirectory.GetRoomsAsync`). **A Room has no Team.** | `src/Huddle.App/Data/Models.cs:7`, `src/Huddle.App/Data/SqliteTeamDirectory.cs:241` |
| A Transcript is one JSONL file per Room, `{DataDir}/rooms/<id>.jsonl` (ADR-0002), each line a `ChatMessage(Id, Timestamp, SenderId, SenderName, Text)`. Tool activity, Drafts and Turn detail are never written to it. `ReadAllAsync` reads the whole file under the Room's lock and skips an unparsable line with a warning. | `src/Huddle.Contracts/ChatMessage.cs`, `src/Huddle.App/Data/FileChatStore.cs:51`, `src/Huddle.App/Data/IChatStore.cs` |
| **The Room view loads the whole Transcript** (`IChatStore.ReadAllAsync`) and renders every Message. Nothing is paged or virtualised. | `src/Huddle.App/Components/Pages/Chat.razor:325` |
| **The only scroll code is "to the bottom".** `MessageList.OnAfterRenderAsync` calls `teamScroll.toBottom` whenever the rendered signature (Messages plus Draft growth) changes, unconditionally. Message rows carry no DOM id. There is no scroll-to-Message. | `src/Huddle.App/Components/Shared/MessageList.razor:290`, `src/Huddle.App/wwwroot/app.js:155` |
| **The Top Bar is built** (PR #162). `TopBar.razor` is a fixed `MudAppBar` across the whole window, above the left menu (the drawer uses `ClipMode="DrawerClipMode.Always"`, so it starts below the bar) and every pane. It draws `PageBreadcrumbs` for `TabService.Active`, so the trail follows the focused pane; a crumb navigates that Tab in place. It has three cells, `.top-bar-start` (the trail), `.top-bar-center` and `.top-bar-end`, and two `RenderFragment?` parameters, **`Search`** (drawn in `.top-bar-center`) and **`Actions`** (`.top-bar-end`). `MainLayout` draws `<TopBar />` with both slots empty today. | `src/Huddle.App/Components/Layout/TopBar.razor`, `src/Huddle.App/Components/Layout/MainLayout.razor` |
| **No pane draws a breadcrumb.** `TabPageHost` mounts only the page; `PageBreadcrumbs` has `Crumbs` and `OnNavigate` and no `Collapse` (a trail spanning the window is never collapsed). `TabTrail.Build` makes a page with no trail show its title alone, as `/settings` does. | `src/Huddle.App/Components/Layout/TabPageHost.razor`, `src/Huddle.App/Components/Shared/PageBreadcrumbs.razor`, `tests/Huddle.Tests/Ui/Layout/PageBreadcrumbsTests.cs` |
| **The whole UI is drawn at 90%.** `html { zoom: 0.9; }`. Viewport units are not zoomed, so `.main-column` is `height: calc(100vh / 0.9)`. MudBlazor places a popover in viewport pixels, so `.mud-popover-provider` cancels the zoom (`zoom: calc(1 / 0.9)`) and `.mud-popover-provider .mud-popover > *` takes it back (`zoom: 0.9`). The scale is a literal `0.9` in each place, never a `var()`, because every `var()` in `app.css` must name a MudBlazor variable (`ThemeSourceTests`). Dialogs, snackbars and `MudSelect` under the zoom have not been checked. | `src/Huddle.App/wwwroot/app.css:19-41`, `:108-116` |
| Global keys: `window.huddleTabs.attach` adds one `document` `keydown` listener for Alt+W, Alt+PageDown and Alt+PageUp, attached from `TabHost.OnAfterRenderAsync`. Alt, because the browser keeps Ctrl+W and Ctrl+Tab. | `src/Huddle.App/wwwroot/app.js:164`, `src/Huddle.App/Components/Layout/TabHost.razor:36` |
| `TabService` has `Open(url)`, `Navigate(id, url)` (in place; focuses another Tab with the same key instead), `Keep(id)` (makes permanent) and `Active`. | `src/Huddle.App/Tabs/TabService.cs` |
| `TabPageRoutes` copies every query value onto the page's `[Parameter]` of the same name, so a page reads `?message=` without `SupplyParameterFromQuery`. | `src/Huddle.App/Tabs/TabPageRoutes.cs:88-97` |
| The Teammates page has no per-Teammate URL. A tile opens the card with `IDialogService.ShowAsync<TeammateCard>(name, Teammates.BuildViewParameters(…), TeammateCard.Options)`; `BuildViewParameters` is an `internal static` pure function of the Persona's entry, its `Persona`, its status, its Room id and its file path. | `src/Huddle.App/Components/Pages/Teammates.razor:239`, `:270` |
| The Library page takes `root` and `path` as plain parameters, filled from the Tab's URL by `TabPageRoutes`. | `src/Huddle.App/Components/Pages/Library.razor:51` |

---

## 5. The shell: the Top Bar

### 5.1 Layout (built)

The Top Bar is built and needs no layout change for search (D-1, D-2, OP-3; PR #162). The search box
is a **slot fill**: `MainLayout` passes it to `TopBar`'s `Search` parameter, and `TopBar` draws it in
its middle cell.

```text
┌───────────────────────────────────────────────────────────────────────────────────────────┐
│ .top-bar-start               .top-bar-center                              .top-bar-end    │
│ Teams › Marketing › Launch Q4  [🔍 Search teams, teammates, library, tasks, rooms…  Ctrl K]  (Actions) │
├──────────────┬────────────────────────────────────────────────────────────────────────────┤
│ left menu    │ [Tab] [Tab] [Tab]                     │ [Tab] [Tab]                        │
│              │ page                                  │ page                               │
```

- **Left: the breadcrumb**, `.top-bar-start`. `TopBar` draws `PageBreadcrumbs` for the focused
  pane's active Tab and follows focus; a crumb navigates that Tab in place (`TabService.Navigate`,
  then the router). A page with no trail shows its title as the last crumb. No pane draws its own.
- **Middle: the search box**, `.top-bar-center`, filled through `<TopBar Search="…" />` in
  `MainLayout.razor`. The cell is a grid column of at most `32rem` (`.top-bar-grid`). The box has the
  placeholder `Search teams, teammates, library, tasks, rooms…` and a `Ctrl K` hint drawn inside it
  at the right.
- **Right: `Actions`**, `.top-bar-end`, empty in V1.
- With no Tab open, the breadcrumb is empty and the search box still works.

### 5.2 Focusing the box

| Key or action | Result |
| --- | --- |
| `Ctrl+K` | Focuses the box and opens the dropdown, from anywhere in the app. The handler calls `preventDefault()`, because Edge otherwise gives `Ctrl+K` to its address-bar search |
| `/` | The same, but only when no text field has focus: not an `input`, a `textarea`, a `contenteditable` element or the CodeMirror editor, so typing `/compact` in the composer is never stolen |
| Click in the box | Always works |
| `Esc` | Closes the dropdown and returns focus to the element that had it before the box opened |

> [!WARNING]
> **`Ctrl+K` in Edge must be tested when this is built.** If `preventDefault()` does not keep it
> from the address bar, the next candidate is `Ctrl+Shift+K` (D-4). Record the result in the manual
> test (GS-M2) and in [known-limits.md](engineering/known-limits.md) if it fails.

**Where the keys attach.** A new `window.huddleSearch` object in `wwwroot/app.js`, shaped like
`window.huddleTabs`: `attach(dotnetRef, inputElement)` adds one `keydown` listener, and `detach()`
removes it. It listens on `window` in the **capture** phase, so a component that handles `keydown`
itself (the composer, CodeMirror) cannot swallow `Ctrl+K` first. The search bar component attaches
it in `OnAfterRenderAsync(firstRender)` and detaches it in `Dispose`, the way `TabHost` attaches,
catching `JSDisconnectedException`. Focus and the "restore focus" element are handled in script,
which remembers `document.activeElement` when the box opens.

---

## 6. The dropdown

### 6.1 Shape

The dropdown follows VS Code's model exactly (D-7):

- Anchored under the search box, **wider than the box**: about 720 px of the UI, left-aligned under
  the box, shrinking to the window's width on a narrow window.
- At most **80% of the browser window's height**; the list inside scrolls. §6.8 says how to compute
  that under the 90% zoom.
- **Typing stays in the bar.** The dropdown has no second input.

```text
 [🔍 task:login                                              Ctrl K]
 ┌──────────────────────────────────────────────────────────────────┐
 │ (All) (Teams) (Teammates) (Library) [Tasks] (Rooms)               │
 │ Search results for: 'login'                     (after a reopen)  │
 │ TASKS                                                             │
 │ ☐ Support SAML login        [In Progress]  Nova · Platform  [   ] │
 │ ☐ Login page copy           [To Do]        Kai  · Marketing [   ] │
 │ CLOSED TASKS  (dimmed)                                            │
 │ ☐ Old login spike           [Done]         Nova · Platform  [   ] │
 └──────────────────────────────────────────────────────────────────┘
```

`[   ]` is the card's action slot, empty in V1 (D-12).

### 6.2 Filter chips and prefixes

The first row is the chips **All, Teams, Teammates, Library, Tasks, Rooms** (D-8, OP-1).

- **A chip is a prefix.** Picking Teams is the same as typing `team:`; typing `team:` lights the
  Teams chip. Picking a chip rewrites the prefix at the start of the box and keeps the rest of the
  text; picking All removes it.
- Prefixes: `team:` Teams, `teammate:` Teammates, `lib:` Library, `task:` Tasks, `room:` Rooms.
  Compared ignoring case; only at the very start of the text; the text after the colon is the
  query, trimmed. (`team:` is not a prefix of `teammate:`: the colon decides.)
- **Reserved:** `>` for V2 commands. It does not search in V1. `?` has no meaning in V1 (OP-14).
- **No counts on chips.**

### 6.3 What a query shows

Every string below is **decided** (OP-15): tests assert them exactly
([Testing.md](../agents/Testing.md)). Four came from the owner verbatim: the placeholder,
`Search results for: '<term>'`, `Couldn't search <Group>.` and `Results may be incomplete.`; the rest
were settled under OP-15. `<term>` is the Human's phrase as typed after any prefix, inside single
quotes. `<Group>` is the group heading below.

**Group headings**, in this fixed order (D-9): **Teams**, **Teammates**, **Library**, **Tasks**,
**Closed Tasks**, **Rooms**, **Archived Rooms**. They are drawn upper case by CSS
(`text-transform`), so a screen reader reads them in normal case.

| State | The dropdown shows |
| --- | --- |
| Box empty, never searched in this circuit | The chips, and one quiet line: `Type 3 or more characters, or pick a filter.` No recents in V1 |
| Box empty, filter **Teams** or **Teammates** | **Browse:** every Team, or every Teammate, in rank order (§7.4), up to 50 |
| Fewer than 3 characters after any prefix (other filters), or a phrase starting `>` | The chips and the same quiet line. `task:fi` does not search; `task:fix` does |
| Filter **All**, 3 or more characters | Groups in the fixed order. Up to **5** cards per group. **No "See all" row** |
| One filter, 3 or more characters | Only that group and its archived side, up to **50** cards each (OP-5). This view *is* "see all" |
| Archived side (Tasks, Rooms) | A second, dimmed group below the active one, within the same filter: **Closed Tasks**, **Archived Rooms** (§7.5). Drawn only once its result arrives |
| An active group still searching | Its heading with a small `MudProgressCircular`; nothing below it yet. A group is searching from the start of a search until its result arrives |
| A group with no hits | In All, the group is not drawn. In a single filter, the active group shows `Nothing in <Group> matches '<term>'.`; an empty archived group is not drawn |
| No group has a hit, every group finished | `No results for '<term>'.` |
| A provider failed | The heading, then `Couldn't search <Group>.` (`Couldn't search Library.`, `Couldn't search Archived Rooms.`). The other groups render as normal (D-20) |
| A provider's result is `Truncated` | Under the group's cards: `Results may be incomplete.` |
| Every group finished (live region, §11) | `<n> results`, `1 result`, or `No results for '<term>'` |

### 6.4 The sticky filter

The filter **sticks** after the bar closes (D-10). On reopening:

- the box is **empty**;
- the last filter is still lit;
- a small header reads `Search results for: '<previous term>'`;
- the previous query is **re-run** at once, so its results are fresh, not a cached copy.

Typing replaces the header and the results with the new query. The sticky state lives in the
search bar component for the life of the circuit; it is not persisted (V2 decides whether it stays
at all, §14).

### 6.5 Keyboard

| Key | With the dropdown open |
| --- | --- |
| Typing | Edits the query in the bar; each keystroke cancels the search in flight (§7.6) |
| `Up`, `Down` | Move through the cards of every group, top to bottom, skipping headings |
| `Ctrl+Left`, `Ctrl+Right` | Move to the previous or next chip (OP-7, decided: this takes word-jump away from the box while the dropdown is open) |
| `Alt+1` … `Alt+6` | Jump to All, Teams, Teammates, Library, Tasks, Rooms |
| `Enter` | Opens the highlighted hit in the current Tab (§9) |
| `Ctrl+Enter` | Opens the highlighted hit in a new, permanent Tab, or focuses it when already open (§9) |
| `Esc` | Closes and returns focus (§5.2) |

`Alt+1`…`Alt+6` are registered only while the dropdown is open, so they never collide with the
Tab shortcuts in `window.huddleTabs`, which use `Alt+W` and `Alt+PageUp/PageDown`.

### 6.6 Cards

Each kind of hit has its own card, chosen by the hit's subtype with a pattern match in Razor (D-15):

```razor
@switch (hit)
{
    case TeamHit team: <TeamSearchCard Hit="@team" /> break;
    case TeammateHit teammate: <TeammateSearchCard Hit="@teammate" /> break;
    case LibraryHit library: <LibrarySearchCard Hit="@library" /> break;
    case TaskHit task: <TaskSearchCard Hit="@task" /> break;
    case RoomHit room: <RoomSearchCard Hit="@room" /> break;
}
```

Every card is one row with the same three zones: what it is (icon or avatar), what it says, and an
**action slot** at the right that is **empty in V1** and reserves its width (D-12). V2 puts buttons
there, such as closing an Ask.

| Card | Shows | Matched on |
| --- | --- | --- |
| **Team** | Team icon, Name, team type, Teammate count (`Development Team · 4 teammates`, `1 teammate`; no type: `4 teammates`) | Name, team type |
| **Teammate** | Avatar (`TeammateAvatar`), Name, Title, Teams (`Product Manager · Marketing, Platform`; no Team: `Product Manager · No team`, OP-8) | Name, Alias, Title |
| **Library** | File-type icon, name, folder path within its root (the root's name at the root); for a content hit, a one-line snippet with the match in **bold** | Name, then contents |
| **Task** | Title, status chip (the Board's status `Color`), assignee and Team (`Nova · Platform`; unassigned: `Platform`) | Title, id, assignee Name |
| **Room** | Room name, then count, sender and time (`14 matches · Kai · 14:02`, `1 match · Kai · 14:02`; a name-only hit has no meta line), then the snippet with the match in bold; **one card per Room**, collapsing many matches | Message text, Room name |

```text
 Team      [▣]  Marketing                     Development Team · 4 teammates   [   ]
 Teammate  (NV) Nova                          Product Manager · Marketing      [   ]
 Library   [📄] plan.md                        Marketing/Launch Q4              [   ]
               …the **login** flow moves to SSO in Q4…
 Task      Support SAML login   [In Progress] Nova · Platform                  [   ]
 Room      #findings                          14 matches · Kai · 14:02         [   ]
               …we should drop the **login** cookie…
```

The match is bolded with `MudHighlighter` (`Text` and `HighlightedText`, ignoring case). An
Archived Room's or Closed Task's card is dimmed and its group heading says so in words, so the
state never rests on colour alone (§11).

### 6.7 MudBlazor components

Picked from [MudBlazorDesign.md](../agents/MudBlazorDesign.md)'s *Finding a component by what you
need*. Confirm each parameter against 9.10.0 before building
([MudBlazorImplementation.md](../agents/MudBlazorImplementation.md)).

| Need | Component | Notes |
| --- | --- | --- |
| The Top Bar | `TopBar` (built: `MudAppBar` with a three-cell grid) | Filled through its `Search` slot, never reshaped |
| The breadcrumb | `PageBreadcrumbs` (built, `MudBreadcrumbs`) | Untouched by search |
| The search box | `MudTextField<string>` with a start adornment (search icon) and an end adornment (`Ctrl K` hint) | Not `MudAutocomplete`: that offers one flat list of one type, and this dropdown is grouped, mixed and chip-filtered |
| The dropdown | `MudPopover` anchored to the box | Size rules in the `/* Global search */` block of `app.css`, `--mud-*` variables only, under the zoom rules of §6.8 |
| Filter chips | `MudChipSet<SearchFilter>` with single selection | `SelectedValue` bound to the filter |
| Group headings, card text | `MudText` | |
| Avatars | `TeammateAvatar` | The house component |
| Status chip on a Task card | `MudChip` with the Board's status-to-`Color` helper (`TaskColors.For`) | Reuse the one helper, do not add a second mapping |
| Bold match | `MudHighlighter` | |
| A group still searching | `MudProgressCircular Indeterminate="true" Size="Size.Small"` | |
| A failed or truncated group | `MudText` with `Color.Warning` and an icon | Not a `MudAlert`: it is one line inside a list |

### 6.8 Under the 90% zoom

The UI is drawn at 90% (`html { zoom: 0.9; }`, §4). The dropdown is a `MudPopover`, so it lives in
the popover provider and must follow the provider's rule exactly. Read the zoom comment and the two
provider rules at the top of `app.css` (`:19-41`) before writing any dropdown CSS, and copy that
approach; do not invent another.

1. **Placement cancels the zoom.** The `.mud-popover` element sits inside `.mud-popover-provider`,
   whose `zoom: calc(1 / 0.9)` undoes the page's 0.9, so MudBlazor's viewport-pixel placement is
   right. Do not set a `zoom` on the popover element itself, and do not move the dropdown out of
   the provider.
2. **Content re-applies 0.9.** The provider's `.mud-popover > *` rule gives the popover's content
   `zoom: 0.9`, so the dropdown is drawn at the same size as the rest of the UI. The dropdown's
   sizing and scrolling live on its **content wrapper** (`.search-dropdown`, the popover's direct
   child), which is inside that 0.9.
3. **Heights and widths in viewport units divide by the scale**, as `.main-column` does, because
   viewport units are not zoomed: inside a 0.9 zoom, `80vh` would come out at 72% of the window.
   So the wrapper is exactly:

   ```css
   .search-dropdown {
       width: min(720px, calc(100vw / 0.9));
       max-height: calc(80vh / 0.9);
       overflow-y: auto;
   }
   ```

   which is 80% of the window's height and never wider than the window. `720px` is in the UI's own
   pixels, like every other px size in the app. The two `0.9` literals join the scale's other
   places, and the comment above `html { zoom }` must be updated to name them, so a change of scale
   changes all of them together.
4. **Verify at 90%, by hand.** bUnit runs no layout. GS-M10 checks that the dropdown is anchored
   flush under the box at its left edge, that its height reaches 80% of the window with a long
   result list and no further, and that the list scrolls inside it. GS-M5 checks the Room jump's
   centring and highlight at 90%.

---

## 7. The search pipeline

```text
 SearchBar.razor ──text──► SearchOrchestrator ──SearchQuery──► ISearchProvider × 5
   ▲                        │  parse prefix                     (Teams, Teammates, Library, Tasks, Rooms)
   │                        │  fan out: Active pass, then Archived pass
   │   GroupUpdated event   │  each provider's SearchResult ──► SearchResultRanker (pure)
   └────────────────────────┘  one CancellationToken for both passes
```

New code lives in `src/Huddle.App/Search/`, namespace `Agency.Huddle.App.Search`; components in
`src/Huddle.App/Components/Search/`. Types that a component receives as a `[Parameter]` must be
`public` (CS0053, [rules.md](engineering/rules.md)); the rest stay `internal`.

### 7.1 Types

```csharp
/// <summary>Which category the dropdown shows: All, or one filter. A chip and a prefix both set it.</summary>
public enum SearchFilter { All, Teams, Teammates, Library, Tasks, Rooms }

/// <summary>Which side of a category a call searches: the live items, or Archived Rooms and Closed Tasks.</summary>
public enum SearchScope { Active, Archived }

/// <summary>The provider's judgement of how well a hit matched, best first.</summary>
public enum MatchTier { ExactName, NamePrefix, NameContains, ContentMatch }

/// <summary>One search as every provider receives it.</summary>
/// <param name="Text">The phrase, with any prefix already removed and trimmed. Empty means browse.</param>
/// <param name="Filter">The active filter.</param>
/// <param name="OpenTeam">The Team of the focused Tab, or null (§7.4).</param>
/// <param name="Scope">Active or Archived.</param>
/// <param name="MaxHits">The per-group cap for this view.</param>
public sealed record SearchQuery(string Text, SearchFilter Filter, string? OpenTeam, SearchScope Scope, int MaxHits);

/// <summary>One provider's batch: its hits and whether a limit cut the search short.</summary>
public sealed record SearchResult(IReadOnlyList<SearchHit> Hits, bool Truncated);

/// <summary>One result. The card is chosen by the subtype, never by a kind enum.</summary>
public abstract record SearchHit(string Title, string? TeamName, bool IsArchived, MatchTier Tier);

public sealed record TeamHit(...) : SearchHit(...);      // type label, Teammate count
public sealed record TeammateHit(...) : SearchHit(...);  // Name, Title, Alias, Teams
public sealed record LibraryHit(...) : SearchHit(...);   // root id and name, relative path, folder path, snippet
public sealed record TaskHit(...) : SearchHit(...);      // TaskId, status, assignee
public sealed record RoomHit(...) : SearchHit(...);      // Room id and name, match count, newest matching Message id, sender, snippet, time
```

- **`SearchResult` is not `LibrarySearchResult`.** The Library's record stays exactly as it is,
  `internal`, with `LibraryEntry` hits, and keeps serving the explorer. The Library provider reads
  the walk's result and maps each entry to a `LibraryHit` (D-16).
- Each hit carries **everything its card renders** (Name, Title, Alias, snippet …), its Team, its
  `IsArchived` flag and its **rank** (the `MatchTier`; the ranker adds the rest, §7.4). A card
  never calls a service.
- `TeamName` is the Team the hit belongs to, used only for "open Team first" (§7.4). A Room has none
  (OP-4); a Library hit has one only under the Teams root (its first path segment).
- The plan gives the exact record signatures. `TeammateHit` names the Persona's Title
  `PersonaTitle`, because the base record's `Title` is the card's main text (the Name).
- Records, `IReadOnlyList<T>`, explicit types and target-typed `new()`, `CancellationToken` last,
  `TimeProvider` injected: [CSharpPrinciples.md](../agents/CSharpPrinciples.md) throughout.

### 7.2 `SearchQueryParser` (pure)

`SearchQueryParser.Parse(string boxText, SearchFilter chipFilter)` returns the filter and the
phrase:

1. If the text starts with a known prefix (ordinal, ignoring case), the prefix sets the filter and
   is removed. Otherwise the chip's filter stands.
2. The remainder is trimmed. That is the phrase.
3. `IsSearchable` is true when the phrase has **3 or more characters** and does not start with `>`,
   or when it is empty and the filter is Teams or Teammates (browse).

`3`, the per-group cap of `5`, the single-filter cap of `50` and the `200` ms debounce are
**constants in V1** (`SearchLimits.MinChars`, `SearchLimits.GroupCap`,
`SearchLimits.SingleFilterCap`, `SearchLimits.Debounce`), not configuration (D-24, OP-5).

### 7.3 `ISearchProvider` and `SearchOrchestrator`

```csharp
/// <summary>Searches one category.</summary>
internal interface ISearchProvider
{
    /// <summary>The group this provider fills.</summary>
    SearchFilter Group { get; }

    /// <summary>Whether this provider reads from disk, and so waits for the debounce.</summary>
    bool IsExpensive { get; }

    /// <summary>Searches for <paramref name="query"/>. Returns an empty result at once for
    /// <see cref="SearchScope.Archived"/> when the category has no archived side.</summary>
    Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct);
}
```

**`SearchOrchestrator`** (scoped, one per circuit) owns the flow (D-17):

1. Takes the parsed box text (§7.2; the bar parses it first, so its chip lights before any provider
   runs). Not searchable: cancels and raises nothing.
2. **Cancels the previous search** and creates one `CancellationTokenSource` for this one.
3. Starts the **Active** pass: every provider the filter selects, at once. Cheap providers (Teams,
   Teammates, Tasks) start immediately; expensive ones (Library, Rooms) wait the 200 ms debounce on
   the injected `TimeProvider` first, so a fast typist's every keystroke cancels them before they
   read a file.
4. **As each provider completes**, hands its `SearchResult` to `SearchResultRanker` and raises
   `GroupUpdated` with the search's id, the group, the scope and a `SearchGroupState`. The dropdown
   fills group by group. There is **no streaming inside a provider in V1**: no `IAsyncEnumerable`,
   one batch per provider per pass (D-18).
5. **Only when every Active call has finished** does it start the **Archived** pass, last in the
   queue (D-19). The same token covers both passes.
6. A new keystroke, a filter change, `Esc` or disposal cancels the token. A result that arrives
   after its token was cancelled is dropped, never raised.

`SearchGroupState` is a small record family: `Done(SearchResult ranked)` or `Failed`. The
orchestrator raises exactly one update per provider per pass; "searching" is not an event but what
the view shows for an active group of the current search that has no update yet (§6.3). The bar's
component subscribes to `GroupUpdated`, ignores an update whose search id is not the current one,
and re-renders through `RendererDispatch.RunAsync(this.InvokeAsync, …)`, the house pattern, and
unsubscribes in `Dispose` ([rules.md](engineering/rules.md): a leaked subscription never dies).

**Failure isolation (D-20).** The orchestrator awaits each provider inside its own `try`. An
`OperationCanceledException` from the search's own token ends quietly. Any other exception is
logged with the provider's group and becomes `Failed` for that group alone: `Couldn't search
<Group>.` This is the "supervisor that must not die" case in CSharpPrinciples, so the general
`catch` carries a comment saying why swallowing is safe (`Huddle.App` already suppresses
`CA1031`). Providers still catch the specific, expected failures themselves (a locked folder, a
missing Transcript) and skip the one item, as `FindAsync` already does.

### 7.4 `SearchResultRanker` (pure)

The provider knows how well each hit matched; it does not know what else is on screen. The ranker
is a **pure static function with no I/O** (D-22) that orders one group's hits by:

1. **The open Team first.** A hit whose `TeamName` equals `SearchQuery.OpenTeam` (ordinal, ignoring
   case) comes before every other. A **Teammate ranks first when any of its Teams is the open
   Team**; a **Room never ranks by Team** (OP-4, decided). The open Team is the Team of the focused
   Tab: `/teams/{team}` and its Projects and sub-tabs; otherwise none.
2. **Match tier**, best first: `ExactName`, `NamePrefix`, `NameContains`, `ContentMatch`.
3. **Active before Archived.** Within one group this only matters if a provider mixes them; the
   passes keep them apart in practice.
4. **Alphabetical** by `Title`, `StringComparer.OrdinalIgnoreCase`, as the tie-break.

It then cuts the list to `MaxHits`, and sets `Truncated` when it cut anything or the provider
already said so.

Tier per field, as the providers assign it (OP-6, decided as written). Where a row lists several
fields, the **first field that matches** decides the tier:

| Category | `ExactName` / `NamePrefix` / `NameContains` against | `ContentMatch` |
| --- | --- | --- |
| Teams | Name, else team type | — |
| Teammates | Name, else Alias, else Title | — |
| Library | File or folder name (an exact match also counts against the name without its extension) | Contents |
| Tasks | Title, else id | Assignee Name |
| Rooms | Room name | Message text |

### 7.5 The two passes

Archived and Closed items are searched **after** the active ones, and shown in a separate, dimmed
group below the active results **within each filter** (D-19).

| Category | Archived side | In the dropdown |
| --- | --- | --- |
| Rooms | Archived Rooms (`Room.Archived`) | **Archived Rooms** |
| Tasks | Closed Tasks (`TaskItem.Location.Closed`) | **Closed Tasks** |
| Teams, Teammates, Library | None | Return an empty result for `Archived` at once |

The code says `SearchScope.Archived` for both, because the owner settled one word for the pass;
the interface keeps each feature's own word (**Archived** for Rooms, **Closed** for Tasks), as
[language.md](engineering/language.md) requires.

### 7.6 Cancellation and timing

- **One token per search**, covering both passes and every provider.
- **Every keystroke cancels** the search in flight; the debounce means the expensive providers
  have usually not started.
- Every awaited call that accepts a token gets it (`CA2016`); a provider checks it between files
  and between Rooms, so a cancel stops a scan within one file read.
- The debounce waits on the injected `TimeProvider`, never `Task.Delay` on the system clock
  (`S6354`), so tests drive it with `FiringTimeProvider`.

---

## 8. Providers

A provider whose feature is switched off (`Team:Library:Enabled` or `Team:Tasks:Enabled` set to
`false`) returns an empty, not truncated result at once; its chip stays.

### 8.1 Teams

- **Source:** `ITeamCatalog.Teams`, in memory. Cheap.
- **Matches:** Team Name, then team type (the type's display name, `TeamTypeCatalog.Find(id)`, or
  its id when the type is unknown).
- **Browse:** an empty phrase under the Teams filter lists every Team.
- **Hit:** Name, type label, `Members.Count` (shown as Teammates), `TeamName` = itself.
- **Opens:** the Team page, `/teams/{team}`.

### 8.2 Teammates

- **Source:** the Personas in `PersonaStore`'s current `PersonaIndex` (`Entries`). Cheap. **Every
  Persona is a Teammate result**, showing *No team* when it has none; the Human is never listed
  (OP-8, decided). Rejected files are not listed.
- **Matches:** Name, Alias, Title (§7.4 for the tiers).
- **Browse:** an empty phrase under the Teammates filter lists every Teammate.
- **Hit:** Name, Title, Alias, Teams; the card resolves the avatar from `AvatarStore` by Name, as the
  Teammates page does.
- **Opens:** that Teammate's card **dialog, directly**, over the current page, with no URL and no
  Tab change (§9, OP-9).

### 8.3 Library

- **Source:** every Library Root the tree shows (`LibraryRootStore.VisibleRoots`; a hidden built-in
  root is skipped, OP-17), walked from disk on every search. Expensive. **Search keeps no index of
  its own** (D-21).
- **Parallelism:** the roots are searched concurrently, at most `Team:Search:LibraryParallelism`
  (default `4`) at a time, with `Parallel.ForEachAsync` and the search's token.
- **Names:** reuses `FindAsync`'s walk for each root, so every tree hiding rule applies
  (`LibraryHiddenFolders`, reserved folders, junctions refused by `LibraryPathResolver`).
- **Contents (new):** in the same walk, for each **file** whose name did not match, the provider
  reads its text when, and only when:
  - its extension is in `Team:Search:TextExtensions` (ordinal, ignoring case); and
  - its length is at most `Team:Search:MaxContentFileBytes`. A larger file is matched by **name
    only**.

  The text is decoded with `TextFileCodec` (BOM, UTF-8, UTF-16), so it reads what the Library
  shows. A file that fails to decode, is locked or vanishes is skipped. The first matching line
  becomes the snippet, cut to a fixed width around the match (a constant).
- **Visit budget:** `Team:Search:MaxVisitedEntries` (default `5000`) is each root walk's visit
  budget, counting every entry visited, as `FindAsync` counts today. It is a budget, not an index:
  nothing is kept after the search. **Assumed, the owner has not confirmed (OP-2).** `FindAsync`
  keeps using `Library:MaxIndexedFiles`.
- **Truncated:** when the visit budget, the hit cap or a skipped over-size file cuts a root short,
  the group's result is `Truncated` and shows `Results may be incomplete.` A folder that cannot be
  listed, like a file that cannot be read, is skipped silently and does not set `Truncated`.
- **Excluded:** see §8.6.
- **Hit:** root id and name, relative path, the folder path within the root, the snippet for a
  content hit, `TeamName` when the path is in a Team folder under the Teams root.
- **Opens:** the Library Tab on the file, `/library?root={rootId}&path={relativePath}`. No
  jump-to-line in V1.

`FindAsync` itself does not change. The walk is factored into one private method that `FindAsync`
and a new sibling on `LibraryFileService` both use, the sibling adding the content test, so there is
**one** walk and one set of hiding rules.

### 8.4 Tasks

- **Source:** `TaskStore.All`, in memory. Cheap.
- **Active pass:** Tasks not Closed. **Archived pass:** Closed Tasks.
- **Matches:** title, id and assignee Name, ordinal ignoring case (the same comparison
  `TaskQuery.MatchesSearch` uses for title and id).
- **An exact Task id gets no special jump behaviour** (D-14): `PLAT-0042` is a search like any
  other, and ranks `ExactName`.
- **Hit:** `TaskId`, title, status, assignee, Team, `IsArchived` for a Closed Task.
- **Opens:** the Task's detail, `/tasks/item/{id}`.

### 8.5 Rooms

- **Source:** the Rooms from `ITeamDirectory.GetRoomsAsync`, then each Room's Transcript through
  `IChatStore.ReadAllAsync`, one Room at a time, checking the token between Rooms. Expensive.
- **Active pass:** Rooms not Archived. **Archived pass:** Archived Rooms.
- **Matches:** the **Message text only**, and the Room name. Never tool activity, Drafts or Turn
  detail: none of them is in the Transcript, and nothing else is read.
- **One card per Room** (D-13), however many Messages match: the count, and the newest matching
  Message's sender, time, snippet and `Id`.
- A Transcript that cannot be read (a locked or vanished file) skips that Room; an unparsable line
  is already skipped by `FileChatStore`. If every Room fails, the group is `Failed`.
- **Opens:** the Room, scrolled to that Message (§9.1).

### 8.6 Exclusions from the Library group

A Persona file and a Task file each appear once, in their own group, and **never** as a Library
entry (D-11, OP-10 confirmed):

| Excluded | Rule | Under the built-in roots |
| --- | --- | --- |
| **Task files** | `TaskLayout.TryMap` accepts the path relative to `Team:Teams:Dir` | Never reached: `_tasks/` is an `_`-prefixed folder, hidden under the Teams root |
| **Persona files** | The path is a Teammate folder's definition, `Teammates/<Name>/<Name>.md` (`LibraryNodeRole.TeammateDefinition`) | Reached in the Teammates root; skipped |

The rule is tested against the **full path**, not the root, because a pinned root may contain
`DataDir` (Library spec §6.10) and reach both kinds through a root that hides nothing.

---

## 9. Opening a hit, and the Room jump

| Hit | `Enter` and `Ctrl+Enter` open |
| --- | --- |
| Team | `/teams/{team}` |
| Teammate | The Teammate card dialog, directly (OP-9); `Enter` and `Ctrl+Enter` do the same, and no Tab changes |
| Library | `/library?root={rootId}&path={relativePath}` |
| Task | `/tasks/item/{id}` |
| Room | `/rooms/{roomId}?message={messageId}` (§9.1) |

In Tab terms (OP-11, confirmed): **Enter** navigates the focused group's active Tab in place with
`TabService.Navigate(active.Id, url)` (or opens a Tab when none is open); **Ctrl+Enter** opens it
with `TabService.Open(url)` and makes it permanent with `Keep`. Both follow the Tabs rule that a key
is open at most once: if the target is already open in either group, that Tab is focused instead,
so Ctrl+Enter on an open item only focuses it. A click on a card is Enter.

**The Teammate card** opens the way the Teammates page opens it:
`IDialogService.ShowAsync<TeammateCard>(name, Teammates.BuildViewParameters(entry, persona, status,
roomId, filePath), TeammateCard.Options)`, the inputs gathered as `Teammates.razor` gathers them
(`PersonaStore.Entries` and `Get`, the status from `PersonaStatusResolver.Resolve`, the Room from
`ITeamDirectory`, the path from `PersonaStore.PathFor`). One shared opener does this for both, so the
two doors cannot drift. There is **no `?name=` parameter** on `/teammates`.

### 9.1 The Room jump (new work)

A Room hit opens the Room **scrolled to the matching Message**, found by its Message `Id`, with a
**fading highlight** of about two seconds. If the Human is already in that Room, it scrolls in
place with no reload.

Today none of this exists (§4). The work:

1. **`Chat.razor` takes a target.** A plain `[Parameter] public string? Message { get; set; }`,
   filled from the Tab URL's `?message=` by `TabPageRoutes`, as the Library page's `root` and
   `path` are. Not `SupplyParameterFromQuery`, which reads the browser URL and names only the
   focused group's Tab. The Tab key is still `room:{id}`, so the query never opens a second Tab.
2. **`MessageList.razor` marks every row** with its Message id (`data-message-id="@message.Id"`),
   and takes the target id as a parameter.
3. **`app.js` gains `teamScroll.toMessage(container, messageId)`**: finds the row, scrolls it into
   the middle of the list, adds a `search-hit-flash` class, and removes it after the fade. The
   colour comes from `--mud-palette-*` in `app.css`; the fade is a CSS transition, and
   `prefers-reduced-motion` drops it to a plain outline that clears. `scrollIntoView` works in the
   zoomed page's own coordinates; GS-M5 checks the centring and the highlight at 90% (§6.8).
4. **A rule stops the stick-to-bottom logic overriding the jump.** `MessageList.OnAfterRenderAsync`
   today scrolls to the bottom whenever the rendered signature changes, which a newly loaded Room
   always does. With a target set, the first render scrolls to the target instead, and the list
   **does not stick to the bottom** until the Human scrolls back to the bottom or sends a Message.
   A Message that arrives meanwhile is rendered but does not move the view.
5. **The jump happens once.** After it, the `?message=` is dropped from the Tab's URL with
   `TabService.Navigate`, so a reload or a return to the Tab does not jump again (OP-16, confirmed).
6. **A Message id that is not in the Transcript** (deleted Room content, a stale hit) opens the
   Room at the bottom, as today, with no error.

Because the Room view loads the whole Transcript (`Chat.razor:325`), every Message is in the DOM
and the target can always be found. If the Room view is ever paged or virtualised, the jump must
load the target's page first; record that in [known-limits.md](engineering/known-limits.md) when
this is built.

---

## 10. Configuration

New keys under `Team:Search`, bound to a new `SearchOptions` class on `TeamOptions`. The config
root stays `Team:` and must not be renamed ([CLAUDE.md](../CLAUDE.md)).

| Key | Default | Meaning |
| --- | --- | --- |
| `Search:LibraryParallelism` | `4` | How many Library Roots are searched at once. Below `1` is raised to `1` |
| `Search:MaxContentFileBytes` | `1048576` (1 MB) | Above this, a Library file is matched by name only, never read |
| `Search:TextExtensions` | `.md`, `.markdown`, `.txt`, `.json`, `.yaml`, `.yml`, `.csv`, `.log`, `.xml`, `.cs`, `.js`, `.ts`, `.css`, `.ps1` | The only extensions whose contents are read. Compared ignoring case. Matches the Library's text kinds (Library spec §6.11) plus Markdown |
| `Search:MaxVisitedEntries` | `5000` | How many entries one Library Root's walk visits before it stops and the group says `Results may be incomplete.` A visit budget, not an index. **Assumed, the owner has not confirmed (OP-2)** |

- `Search:TextExtensions` is a list option: declare it **nullable with no initialiser** and expose
  an `EffectiveTextExtensions` that falls back to the default, because `ConfigurationBinder`
  appends to an already-populated list (see the remark on `LibraryOptions.Roots` and the
  `FileChangesOptions.EffectiveIgnore` precedent).
- `Team:Library:MaxIndexedFiles` keeps its two existing meanings (the wikilink index cap and the
  explorer's `FindAsync` budget) and is not read by Global Search.
- The 3-character minimum, the caps of 5 and 50 and the 200 ms debounce are constants (§7.2).

These four rows are in the configuration table of
[Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md), marked *designed, not built*. When the
feature is built, the mark comes off.

---

## 11. Accessibility

The pattern is the WAI-ARIA combobox with a listbox popup.

- **The box** is `role="combobox"` with `aria-expanded`, `aria-controls` naming the listbox,
  `aria-autocomplete="list"`, and `aria-activedescendant` naming the highlighted card. Focus stays
  in the box the whole time; `Up` and `Down` move `aria-activedescendant`, not focus.
- **The results** are one `role="listbox"`. Each group is `role="group"` with `aria-labelledby` its
  heading; each card is `role="option"` with `aria-selected` on the highlighted one.
- **The chips** are a `role="radiogroup"` labelled *Filter*, each chip `role="radio"` with
  `aria-checked`. `Ctrl+Left`/`Right` and `Alt+1`…`6` change them without moving focus from the box.
- **Archived and Closed** groups are dimmed **and** headed in words (*Archived Rooms*, *Closed
  Tasks*), and each such card's accessible name ends with `, archived` or `, closed`.
- **A polite live region** (`aria-live="polite"`) announces when every group has finished:
  `<n> results`, `1 result` or `No results for '<term>'`. It does not announce each group, which
  would chatter while the Library scans.
- **Failed and truncated** lines are text, with an icon that is `aria-hidden`.
- **The `Ctrl K` hint** is `aria-hidden`; the box's `aria-keyshortcuts="Control+K /"` says it
  instead.
- **Focus:** `Esc` returns focus to the element that had it before the box opened (§5.2). Opening a
  hit moves focus into the opened page, as a click in the left menu does; a Teammate hit moves it
  into the card dialog.
- **The Room jump** moves focus to the target Message row (`tabindex="-1"`) so a screen reader
  reads it, and the highlight respects `prefers-reduced-motion`.

---

## 12. Testing

Rules from [agents/Testing.md](../agents/Testing.md) apply throughout: every test has a `///`
summary and at least one `Assert`; user-facing text is asserted **exactly**; a collection is
asserted **whole**; a test that passes on arrival is proved with a mutation; every token is
`TestContext.Current.CancellationToken`. New folders: `tests/Huddle.Tests/Search/` and
`tests/Huddle.Tests/Ui/Search/`.

### 12.1 Pure tests

| Id | Class | Pins |
| --- | --- | --- |
| GS-T1 | `SearchQueryParserTests` | Each prefix sets its filter, ignoring case; only at the start; `team:` and `teammate:` kept apart; the chip stands when there is none; trimming; `task:fi` not searchable and `task:fix` searchable; empty Teams and Teammates browse, empty Tasks does not; `>` never searches |
| GS-T2 | `SearchResultRankerTests` | Open Team first, including a Teammate in any open Team and never a Room; then each tier in order; active before archived; alphabetical tie-break, ignoring case; the cap and `Truncated` (cut by the ranker, or already set by the provider); an empty input; no open Team. One test per clause |
| GS-T3 | `SearchOptionsTests` | Defaults, `MaxVisitedEntries` among them; a configured `TextExtensions` replaces the default rather than appending; `LibraryParallelism` below 1 is raised |

### 12.2 Provider tests

Real stores over `TempDataDir` (`tests/Huddle.Tests/TempDataDir.cs`), no mocks, as
[testing.md](engineering/testing.md) requires. The "fake file system" is a real temporary folder.

| Id | Class | Pins |
| --- | --- | --- |
| GS-T4 | `LibraryFileServiceSearchTests`, `LibrarySearchProviderTests` | Name hit; content hit with its snippet; a file over `MaxContentFileBytes` matched by name only, and `Truncated`; an extension outside `TextExtensions` never read; `MaxVisitedEntries` sets `Truncated`; hidden folders never walked; **a Task file and a Persona file excluded, through the built-in roots and through a pinned root that contains `DataDir`**; a locked file skipped; a UTF-16 file found; roots searched in parallel up to the setting (a gated root proves the limit); a hidden built-in root not searched |
| GS-T5 | `TaskSearchProviderTests` | Title, id and assignee matches with their tiers; Active never returns a Closed Task and Archived only Closed ones; an exact id ranks `ExactName` and nothing else |
| GS-T6 | `RoomSearchProviderTests` | One hit per Room with the count; the newest matching Message's id; Room-name match; Archived Rooms only in the Archived pass; an unreadable Transcript skips that Room |
| GS-T7 | `TeamSearchProviderTests`, `TeammateSearchProviderTests` | Browse lists every item; name, type, Title and Alias matches; a Persona with no Team listed; `Archived` returns empty at once |

### 12.3 Orchestrator tests

| Id | Pins |
| --- | --- |
| GS-T8 | Groups are raised **as each provider completes**: with gated fake providers, the cheap group arrives while the expensive one is still held |
| GS-T9 | The Archived pass starts **only after every Active call finished** (a held Active provider keeps every Archived call from starting) |
| GS-T10 | A new query cancels the old: the old token is cancelled, and a late result from it is **never** raised |
| GS-T11 | The debounce: expensive providers do not start until 200 ms pass on `FiringTimeProvider`; cheap ones start at once; a keystroke inside the window means the expensive ones never run |
| GS-T12 | One provider throwing makes that group `Failed` and every other group still `Done` |
| GS-T13 | Exactly one `GroupUpdated` per provider per pass |

The fake providers here are test doubles of `ISearchProvider`, an interface the code owns; the
providers under them are not faked in §12.2.

### 12.4 Component tests (bUnit)

`MudBunitContext` with `RenderWithPopovers`, per [BlazorTesting.md](../agents/BlazorTesting.md);
`await ClickAsync()`, never a synchronous click.

| Id | Pins |
| --- | --- |
| GS-T14 | The empty state text exactly; the chips in order; a chip and its prefix light the same chip |
| GS-T15 | All shows groups in the fixed order, at most 5 cards each, with no "See all" row; a single filter shows one group |
| GS-T16 | Each card renders its fields, chosen by subtype; the action slot is present and empty |
| GS-T17 | `Couldn't search Library.`, `Results may be incomplete.`, `Nothing in Tasks matches 'x'.`, `No results for 'x'.` exactly |
| GS-T18 | Reopening shows `Search results for: 'x'`, an empty box and the last filter, and re-runs the query |
| GS-T19 | ARIA: `role="combobox"`, `aria-expanded`, `aria-activedescendant` following `Down`; groups and options roles |
| GS-T20 | **Delivered (PR #162).** The Top Bar draws one breadcrumb for the focused group, follows focus, and `TabPageHost` draws none: `PageBreadcrumbsTests` |
| GS-T21 | `Chat.razor` with `Message` set calls `teamScroll.toMessage`, not `toBottom`, on its first render, and a later Message does not call `toBottom` until the Human returns to the bottom |
| GS-T22 | `MessageList` rows carry `data-message-id` |
| GS-T23 | The search box is drawn in the Top Bar's `.top-bar-center` cell through the `Search` slot, and `TopBar` itself is unchanged |
| GS-T24 | `app.css` pins the dropdown's zoom rules as source facts: `.search-dropdown` has `max-height: calc(80vh / 0.9)` and `width: min(720px, calc(100vw / 0.9))`, and no rule sets `zoom` on a search popover |
| GS-T25 | `Enter` on a Teammate hit opens the `TeammateCard` dialog for that Name and changes no Tab |

### 12.5 Manual tests

A new page, `docs/engineering/manual-tests/global-search.md`, in the format of
`persona-lifecycle.md`, with rows in `tracker.md` and a row in `docs/Index.md` §5, written when the
feature is built. All free.

| Id | Proves |
| --- | --- |
| GS-M1 | `Ctrl+K`, `/` and a click open the box; `/` in the composer types a slash; `Esc` restores focus |
| GS-M2 | **`Ctrl+K` in Edge** reaches the app and not the address bar; if not, record it and try `Ctrl+Shift+K` |
| GS-M3 | Groups fill one by one on a large pinned root; typing fast never shows stale results; the time to search every Transcript on the largest real install is recorded (OP-18) |
| GS-M4 | Content hits, the snippet's bold match, the 1 MB cut-off and the incomplete note |
| GS-M5 | A Room hit jumps to the Message with a fading highlight, in a closed Room and in the open one, at the 90% zoom: the row is centred in the list and the highlight covers it; a new Message during the highlight does not yank the view |
| GS-M6 | Archived Rooms and Closed Tasks appear dimmed below, after the active groups |
| GS-M7 | A locked folder in a pinned root shows `Couldn't search Library.` or skips, and the other groups still render |
| GS-M8 | Keyboard only: chips, `Alt+1`…`6`, `Ctrl+Left`/`Right`, `Up`/`Down`, `Enter`, `Ctrl+Enter`; a screen reader announces the highlighted card and the result count |
| GS-M9 | The search box sits in the Top Bar's middle cell beside the breadcrumb, in one pane and in a split, and the trail still follows focus |
| GS-M10 | **Under the 90% zoom**, the dropdown is anchored flush under the box at its left edge; with a long list its height is 80% of the window (measured: `getBoundingClientRect().height` ≈ `0.8 × window.innerHeight`) and no more; the list scrolls inside it; on a narrow window it is no wider than the window. Checked in Chrome and Edge |

---

## 13. Decisions

All settled by the product owner on 2026-10-12. The open points of the brainstorm were answered the
same day; their record is §15.

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **A search box in the Top Bar above the tab strip, owned by the window** | One place to find anything, never tied to the page in view |
| D-2 | **The breadcrumb moves from each pane into the Top Bar and follows the focused group** | One trail for the window, next to the box; it keeps its links. Built (PR #162, ADR-0046 addendum) |
| D-3 | **`Ctrl+K` with `preventDefault()`, `/` when no text field has focus, click always** | VS Code and web-app habit; `/` must never eat a slash being typed |
| D-4 | **`Ctrl+Shift+K` is the fallback if Edge keeps `Ctrl+K`** | Decided now, so a failed check is a one-line change |
| D-5 | **The word is Library, never documents or files** | It is the product's name for that place |
| D-6 | **Exact phrase, ordinal ignoring case; no word splitting, no quotes needed** | Grep style: predictable, and no query language to learn |
| D-7 | **VS Code's dropdown: anchored, ~720 px, 80% height, typing stays in the bar** | A known model; no second input to lose focus to |
| D-8 | **Chips All, Teams, Teammates, Library, Tasks, Rooms; a chip is a prefix** | Mouse and keyboard reach the same state |
| D-9 | **All shows 5 per group, fixed order, no "See all"; a single filter is the see-all** | One fewer kind of row; the filter already is that view |
| D-10 | **The filter sticks; on reopen the box is empty, the last term is shown and re-run** | Returning to a search is common; re-running keeps it fresh |
| D-11 | **Persona and Task files are never Library hits** | They appear once, as Teammates and Tasks, with their own cards |
| D-12 | **One card component per kind, with an empty action slot in V1** | Each kind shows what matters about it; V2 adds actions without a layout change |
| D-13 | **One Room card per Room, collapsing its matches** | A busy Room would otherwise fill the group |
| D-14 | **An exact Task id gets no special jump** | One rule for every query |
| D-15 | **`SearchHit` is an abstract record with sealed subtypes; the card is chosen by pattern match** | The compiler checks that every kind is drawn; no kind enum to keep in step |
| D-16 | **`SearchResult` is a new type; `LibrarySearchResult` stays as it is** | The explorer keeps working unchanged, and the two cannot be confused |
| D-17 | **The orchestrator raises each provider's batch as it completes** | Cheap groups appear at once while disk-bound ones scan |
| D-18 | **No streaming inside a provider in V1** | One batch per provider is simpler to rank, cancel and test; streaming is V2 |
| D-19 | **Archived and Closed are a second pass, after every active call, in a dimmed group** | Live work is what is usually wanted, and it arrives first |
| D-20 | **A provider failing empties only its own group** | A locked folder must not hide every other result |
| D-21 | **Search keeps no index of its own: every search re-reads** | Nothing to build, invalidate or keep in step; acceptable at Huddle's sizes. ADR-0050 records it |
| D-22 | **`SearchResultRanker` is pure and separate from the providers** | What a provider cannot know (the open Team) is ranked in one tested place |
| D-23 | **Library roots are searched in parallel, default 4, configurable** | Disk-bound walks overlap; the setting caps load on a slow drive |
| D-24 | **The 3-character minimum, the caps of 5 and 50 and the 200 ms debounce are constants** | Nobody needs to tune them yet |
| D-25 | **Teams and Teammates browse when empty; everything else needs 3 characters** | Both lists are small and in memory; the rest would match too much |

---

## 14. Version 2

Kept here so nothing from the brainstorm is lost. None is in V1.

| Item | Note |
| --- | --- |
| Recents | An empty-state "recently opened" list, sourced from Tab history; V2 decides the details |
| Actions and `>` commands | An Actions section, and commands such as *Create a team* after `>`; the Top Bar's `Actions` slot is reserved for toolbar buttons |
| `?` lists the prefixes | Typing `?` alone shows the prefixes as a hint (OP-14: out of V1) |
| Inline action buttons on cards | In the reserved slot, such as closing an Ask |
| Accent-insensitive matching | V1 ignores case only |
| Jump to line | Opening a Library content hit at the matching line |
| Streaming results from providers | `IAsyncEnumerable` inside a provider, so a long Library scan shows hits as it finds them |
| The sticky filter | Whether it stays, once it has been used |
| Sprints | **Not searched, and not planned** |

---

## 15. Open points

The brainstorm left eighteen points open. The owner answered all but one on 2026-10-12. This is the
record "as decided".

### 15.1 Assumed, awaiting the owner's confirmation

| # | Point | Assumed |
| --- | --- | --- |
| OP-2 | **Which visit budget the Library content scan uses**, and the wording around "no index" | **A new key, `Team:Search:MaxVisitedEntries`** (default `5000`, the current `MaxIndexedFiles` value), rather than reusing `Library:MaxIndexedFiles`, which also caps the wikilink index. The wording is "search keeps no index of its own", with `WikiLinkIndex` named as the per-root wikilink index search does not use. **The owner has not answered; this is the recommended option, applied so work can start** |

### 15.2 Decided

| # | Point | Decision |
| --- | --- | --- |
| OP-1 | Two labels against the glossary | **Follow the glossary**: *Members* → **Teammates**, *Chats* → **Rooms**, in the placeholder, chips, prefixes (`teammate:`, `room:`), headings (*Archived Rooms*), cards, types (`TeammateHit`, `RoomHit`), providers and tests. **Library** stays |
| OP-3 | How wide the Top Bar is | **The whole window**, over the left menu. Built (PR #162) |
| OP-4 | Which Team a hit belongs to | **Accepted**: a Room never ranks by Team; a Teammate ranks first when any of its Teams is open; a Library hit has a Team only under the Teams root |
| OP-5 | The cap for a single-filter view | **Accepted**: 50, a constant |
| OP-6 | Tiers for secondary fields | **Accepted as §7.4 reads**; the first field that matches decides the tier |
| OP-7 | `Ctrl+Left`/`Right` | **Accepted**: they move between chips while the dropdown is open |
| OP-8 | Which Personas are Teammate results | **Every Persona**, showing *No team* when it has none; the Human is never listed |
| OP-9 | Opening a Teammate card from search | **Open the card's dialog directly**; no `?name=` parameter on `/teammates` (§9) |
| OP-10 | Excluding Persona files from the Library group | **Confirmed** |
| OP-11 | Enter and Ctrl+Enter in Tab terms | **Confirmed** as §9: Enter navigates the focused Tab in place; Ctrl+Enter opens a permanent Tab, or only focuses it when already open |
| OP-12 | ADRs | **Approved**: write ADR-0050 for *search keeps no index of its own*. The breadcrumb's move is already recorded in ADR-0046's addendum |
| OP-13 | Glossary entries | **Approved**: *Global Search*, *Top Bar*, *Filter* and *Card* (search sense) go into [language.md](engineering/language.md), the Card entry noting that *card* already means a Board's drawing of a Task and the Teammate card |
| OP-14 | `?` lists the prefixes | **Out of V1**; moved to §14. `>` stays reserved for V2 |
| OP-15 | Interface strings | **Decided** in §6.3 and §6.6 by the spec's best judgement, as the owner asked, with the four owner strings kept verbatim; tests use them exactly |
| OP-16 | Dropping `?message=` after the jump | **Confirmed** |
| OP-17 | Which Library Roots are searched | **Confirmed**: the roots the tree shows; a hidden built-in root is skipped |
| OP-18 | Large Transcripts | **Accepted for V1**; the timing check stays in GS-M3 |

Two gaps no open point named were settled while planning and are written into the design above:
a disabled Library or Tasks feature gives an empty group (§8), and "searching" is a view state, not
an event, so GS-T13's one update per provider per pass holds (§7.3).

---

## 16. Blast radius

Existing files that change when this is built. New files go under `src/Huddle.App/Search/`,
`src/Huddle.App/Components/Search/` and the test folders in §12. The Top Bar's own files
(`TopBar.razor`, `TabPageHost.razor`, `PageBreadcrumbs.razor`, the Tabs spec, ADR-0046) are already
as they need to be (PR #162) and do not change.

| File | Change |
| --- | --- |
| `src/Huddle.App/Components/Layout/MainLayout.razor` | Fills `<TopBar Search="…" />` with the search bar |
| `src/Huddle.App/Components/Pages/Chat.razor` | A `Message` target parameter; passes it to `MessageList` |
| `src/Huddle.App/Components/Shared/MessageList.razor` | `data-message-id` on every row; the jump instead of `toBottom`, and the rule that holds stick-to-bottom off |
| `src/Huddle.App/Components/Pages/Teammates.razor` | Its card opening moves into the shared opener the search bar also calls (§9); behaviour unchanged |
| `src/Huddle.App/wwwroot/app.js` | `window.huddleSearch` (keys, focus restore) and `teamScroll.toMessage` |
| `src/Huddle.App/wwwroot/app.css` | A `/* Global search */` block: the dropdown size under the zoom (§6.8), the flash, the dimmed group; the zoom comment names the new `0.9` places. A protected shared root file: announce the change |
| `src/Huddle.App/Library/LibraryFileService.cs` | The shared walk gains the content test (§8.3); `FindAsync` behaves as before |
| `src/Huddle.App/TeamOptions.cs` | A `Search` property of the new `SearchOptions` |
| `src/Huddle.App/ServiceCollectionExtensions.cs` | Registers the orchestrator (scoped) and the five providers |
| `docs/Huddle.EngineeringGuide.md` | The four `Team:Search` keys (added now, marked designed) |
| `docs/engineering/language.md` | Global Search, Top Bar, Filter, Card (OP-13) |
| `docs/adr/0050-…` | Search keeps no index of its own (OP-12) |
| `docs/Huddle.UserGuide.md` | Global Search, from the Human's side, in the same change as the build |
| `docs/engineering/manual-tests/shell-nav.md`, new `global-search.md`, `tracker.md` | Manual tests (§12.5) |
| `docs/engineering/code-map.md` | The `Search/` folder |

---

## 17. Delivery plan

The plan that builds this design lives in its own page,
[global-search-plan.md](engineering/global-search-plan.md), so this spec stays the design and the
plan can be tracked with `Set-Tracker.ps1`. It cuts the work into deliverables `D0` to `D15` (no
hyphen, unlike the decisions `D-1` to `D-25` above), each one concern that ends on a green build,
and cites the section, decision and test ids of this spec in every task. D10, the Top Bar, is
already delivered (PR #162).

| Wave | Deliverables | Model |
| --- | --- | --- |
| 0 | D1 contracts, options and text helpers | Haiku |
| 0 | D0 ADR-0050 | Sonnet |
| 1 | D2 parser, ranker, open Team and hit links; D3 Teams, Teammates and Tasks providers; D4 Rooms provider; D8 cards and the zoom-aware stylesheet; D9 dropdown view model | Haiku |
| 1 | D5 Library walk with the content test; D13 the Room jump and the Teammate card opener | Sonnet |
| 2 | D6 Library provider | Haiku |
| 2 | D7 orchestrator and DI | Sonnet |
| 3 | D11 the search bar in the Top Bar's `Search` slot; D12 global keys and focus | Sonnet |
| 4 | D14 docs, glossary, manual tests and the user guide | Haiku |
| 4 | D15 integration and QA | Sonnet |

More than half of the remaining work is Haiku's: 8 of 15 deliverables, 16 of 30 size points and 33
of 59 tasks. Only OP-2 is an assumption; every other open point is decided (§15).
