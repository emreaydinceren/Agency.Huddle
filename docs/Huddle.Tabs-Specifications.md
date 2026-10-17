# Tabs: specification

Status: version 1 **built 2026-10-10** on `feat/tabs` ([ADR-0045](adr/0045-a-tab-is-a-remembered-url-and-only-the-active-one-is-mounted.md)); version 2 (split, pinned Tabs, the Tab menu; section 9) **built 2026-10-10** on `feat/tabs-v2` ([ADR-0046](adr/0046-a-group-mounts-its-own-page-and-the-url-names-the-focused-one.md)). Owner: chat surface.

## 1. The idea

Everything the Human opens from the left menu opens in a **Tab**, as in VS Code. A click opens a
**preview Tab** (its title in italics); it is replaced by the next preview. A double-click, or
working in the Tab, makes it **permanent** (upright title). The Library is one page and so one Tab.

Version 2 adds a split: two Tabs side by side, a Tab menu, and pinned Tabs (section 9). Stacked groups
and more than two groups are still to come; the model and the persisted format already allow them.

## 2. Terms

| Term | Meaning |
| --- | --- |
| Tab | One open page: a key, a URL, a title, an icon and a preview flag. |
| Preview Tab | A Tab not yet made permanent. At most one exists per group. Shown in italics. |
| Permanent Tab | A Tab the Human kept. Never replaced by opening something else. |
| Tab key | What makes two URLs the same Tab (section 3). Opening an item whose key is open activates it. |
| Group | An ordered row of Tabs with one active, drawn as one pane. Version 1 has exactly one; version 2 allows up to `TabService.MaxGroups` (2). Exactly one group has focus. |
| Pinned Tab | A Tab kept at the start of its group, drawn as its icon alone, with no x. Pinning also makes it permanent. |

## 3. Behaviour

| Action | Result |
| --- | --- |
| Click a left-menu item | Opens a preview Tab, inserted right of the active Tab. An existing preview Tab is replaced in place. |
| Click an item whose key is open | Activates that Tab and updates its URL. It is never duplicated, and a permanent Tab stays permanent. |
| Double-click a menu item or a Tab; type in the composer | The Tab becomes permanent. Editing a Task and saving a setting should too, and do not yet. |
| Close (x, middle-click) | The right neighbour becomes active, else the left. Closing the last Tab shows an empty state. |
| The page navigates itself to the same key | The Tab's URL and title update. No new Tab. |
| The URL changes to a different key (browser Back, a link in a message, a typed URL) | Treated like a menu click: preview Tab, or activate. |
| A Room is renamed | Its Tab title follows. |
| A Room is archived or deleted | Its Tab closes (`TabSync`). A removed Team's or Project's Tab closes too, but only when the catalog announces a change and still lists at least one Team: an empty catalog is read as "not loaded yet", so removing the very last Team leaves its Tab open. When closing a Tab moves the active Tab, the router follows it, unless the browser is already gone (a closed or reloaded page whose circuit is still retained): the Tab still closes, and no navigation is issued (`ServerNavigator`). |
| Reload or restart | Tabs, their order, the active Tab and the preview flags are restored. |
| Any page opens in a Tab | The Top Bar shows its breadcrumb trail (below). One trail for the window, following the focused pane; no page draws its own. |
| Click an ancestor crumb | Navigates that Tab in place (`TabService.Navigate`): same slot and group, key, title and icon re-derived, a preview becomes permanent. If another Tab already has the target's key, that Tab is focused instead. |

### Top Bar and breadcrumbs

`TopBar` is a fixed `MudAppBar` across the whole window, above the menu and every pane, and draws
`PageBreadcrumbs` for the focused pane's active Tab (`TabService.Active`), so the trail is the shell's
and moves when focus moves. It is laid out as three cells: the trail, then reserved room for a search
box (`Search`) and for toolbar buttons (`Actions`), which later features fill.
`TabTrail.Build(url, tabTitle)` derives it from the URL, following the same table as the keys. The
last crumb is the Tab's own title (so a renamed Room follows), is plain text, and carries
`aria-current="page"`. An ancestor with a page of its own is a link; an area with none (*Teams*) is
plain text.

| URL | Trail |
| --- | --- |
| `/`, `/tasks`, `/teammates`, `/library`, `/settings` | the title alone |
| `/rooms/{id}` | Chats (link to `/`), the Room's name |
| `/teams/build` | Teams, Build a team |
| `/teams/{team}` | Teams, the Team |
| `/teams/{team}/projects/{project}` | Teams, the Team (link), the Project |
| `/tasks/{view}`, `/tasks/new`, `/tasks/item/{id}` | Tasks (link), the Tab's title |
| `/settings/{tab}` | Settings (link), the tab's name |

The bar is fixed, so it stays in view while a long page scrolls, and it spans the window, so a trail is
never collapsed. Pages drop a title that only repeats the last crumb; Tasks and Chat keep theirs, because
the Task view's Tab title is its id and the Room's title row carries rename and members.

### Tab keys

| URL | Key | Why |
| --- | --- | --- |
| `/`, `/rooms/{id}` | `room:{id}` (`chat` for `/`) | `/` redirects to the first Room. |
| `/teams/{team}` and its sub-tabs | `team:{team}` | A sub-tab is in-Tab navigation. |
| `/teams/{team}/projects/{project}` and its sub-tabs | `project:{team}/{project}` | |
| `/teams/build` | `team-build` | The query (`?type=`) is the page's own state, not part of the key. |
| `/tasks/{view}` | `tasks:{view}` | |
| `/tasks/item/{id}`, `/tasks/new` | `task:{id}`, `task-new` | |
| `/teammates` | `teammates` | |
| `/settings` and its sub-tabs | `settings` | |
| `/library` with any query | `library` | One page, one Tab. A root row re-scopes it. |
| anything else | the path | |

Keys compare ordinally and case-sensitively, except Team and Project names, which are lowered.

## 4. Where state lives

Only the active Tab is a live component (the router mounts one page). A Tab keeps nothing alive
while hidden. What survives a switch is the Composer's unsent text, kept by the Composer's script in
`sessionStorage` under `huddle:draft:{roomId}` (it survives a reload but not an app restart, and is
cleared on send). It follows the Room, so it also fixes text leaking from one Room into the next.
Everything else a page needs is already in its stores and is read again on activation, exactly as
when the Human navigates today. A scroll offset is not kept: the Chat list sticks to the bottom, and
no other page needs it yet.

The URL stays the source of truth for the active Tab. A Tab is a remembered URL.

## 5. Model and persistence

`TabService` (scoped per circuit) holds a **layout node**. Version 1 has one node kind, the group;
version 2 adds a split (horizontal or vertical) holding child nodes. Persisted as versioned JSON
in browser storage under `huddle:tabs`:

```json
{ "version": 1, "root": { "kind": "group", "activeId": "t2",
  "tabs": [ { "id": "t1", "url": "/rooms/ab", "title": "Design", "icon": "chat", "preview": false } ] } }
```

The key is not stored; it is derived from the URL on restore. A snapshot with an off-site URL, a
duplicate id or key, or more than one preview is refused whole. An unreadable, empty or
future-version value is ignored; the app starts with no Tabs. Restore runs
after the first render (no JavaScript during prerender), then reconciles with the current URL.

### Navigation

Version 1 pages keep calling `NavigationManager`. An `ITabNavigator` abstraction was planned for
version 1 and dropped (2026-10-10): with one implementation that only delegates, it would add
nothing and force every page's test fixture to register it. Version 2 introduces it when it has
a second implementation. Version 2 has two groups but still did not need it: each group gets its own
page host (`DynamicComponent`, resolved from the Tab's URL), and the browser URL names the focused
group's Tab, so the router's one location is enough (section 9, ADR-0046). The audit of anything
page-global that assumes one open Room found the composer script keyed per element and the draft keyed
per Room; render coalescing was not touched.

## 6. Library

The docked Library pane (`LibraryPaneHost`, `LibraryPane`, `LibraryPaneState`) and the Appearance
setting `LibraryPaneSide` are retired in this delivery. The full `/library` page is the only Library
surface. Wikilinks and `?library=` links keep their validation (`IsSafeLibraryHref`, the resolver)
and then open the Library Tab on the file. This is done: `LibraryLinkRouter` does the `?library=` routing.

## 7. Delivery plan

1. `Tabs/` domain: `Tab`, `TabKeys`, `TabService`, `TabSnapshot`; unit tests.
2. `TabStrip`, `NavTabLink`; wire `MainLayout` and the left-menu components; bUnit tests.
3. Title refresh, close on archive and delete, the per-Room draft, pin on typing. (Done.)
4. Retire the Library pane and the pane-side setting; reroute links to the Tab. (Done.)
5. Persistence, keyboard shortcuts, close removed Team and Project Tabs. (Done.)

   Shortcuts are **Alt+W** (close), **Alt+Page Down** and **Alt+Page Up** (next, previous). The
   obvious Ctrl+W, Ctrl+Tab and Ctrl+Page Up/Down are the browser's own and a page cannot take
   them. Storage is `localStorage`, so two browser tabs on the app share one list and the last to
   change wins.
6. User guide, manual tests in `shell-nav.md`, ADR. (Done.)

## 8. Not in version 1 (and what version 2 did not take)

Drag to reorder, stacked groups, three or more groups, per-Tab history, and everything about a crowded
strip (decided 2026-10-10 to leave for version 2):

- **Overflow.** Either wrap to further rows (an Appearance option; cheap, but the strip height changes
  as Tabs open and close, and narrow groups would wrap to many rows) or one row with a minimum Tab
  width of about 7rem, scrolling, and an "all Tabs" list at the right edge. Version 2 should pick one
  default when groups exist, since overflow behaves differently at narrow widths.
- **Scroll the active Tab into view** when it is activated off-screen.

Known limit until then: the strip is one row that scrolls sideways. A Tab is never narrower than 14rem, the width a long title is cut to
(a pinned Tab, an icon alone, is the exception), so with many open the strip scrolls rather than shrinking
the titles away; the active Tab is not yet scrolled into view.

## 9. Version 2: split, pinned Tabs and the Tab menu

**Groups.** The strip becomes one strip per group, each over its own page, side by side. The most groups at
once is `TabService.MaxGroups`, set in source to 2 (one split); raising it is the only change a third
group needs in the service. A new group opens only from the menu below, never by itself.

**Focus and the URL.** Exactly one group has focus, and the browser URL always names that group's active Tab.
Pressing or tabbing into the other pane focuses it and navigates to its active Tab; opening anything
(a menu item, a link in a Message) opens in the focused group. The other pane keeps its page mounted,
so the work in it is not lost; its active Tab's top accent is dimmed. A key is open in at most one group
across both: opening a page already open in the other group focuses it there instead of duplicating it
(this is what keeps one open Room per Room).

**The hover toolbar.** Hovering (or focusing into) an unpinned Tab fades in a toolbar over the right end of its title, just left of the x: a chevron that opens the Tab menu, then Move Left and Move Right, each drawn only when that move is possible for the Tab. Tabs are at least 14rem wide, so most of the title is visible before the toolbar appears.

**The Tab menu** (right-click a Tab, or the chevron):

| Item | Does |
| --- | --- |
| Pin Tab / Unpin Tab | Pins: permanent, moves to the end of the group's pinned Tabs, drawn as its icon alone (the title stays for screen readers and as a tooltip). Unpin keeps it permanent and puts it just after the pinned Tabs. Pinned Tabs always stay at the front of their group; a new preview opens after them. |
| Move Left / Move Right | Moves the Tab into the neighbouring group on that side, where it becomes the active Tab and the group takes focus. From the outermost group it opens a new group on that side, if fewer than `MaxGroups` exist and the Tab has company. A lone Tab, or one at the outer edge at the limit, has the item disabled. A moved Tab becomes permanent. Emptying a group closes it. |
| Close | Closes the Tab. |

Moving is between groups only; reordering within a strip (drag) is still to come.

**Rendering.** The router can mount only the page the URL names, so each group mounts its active Tab's
page itself (`TabPageHost`, a `DynamicComponent`) resolved from the Tab's URL by `TabPageRoutes` over the
same `@page` templates (most literal segments wins; route and query values fill the page's `[Parameter]`s).
The router still runs but its page is never rendered. A page that needs its own address reads the cascaded
`TabPageContext`, not `NavigationManager.Uri`: the Tasks page does, and the Library page's four query
values are now plain parameters.

**Persistence.** Format version 2 stores a lone group as before, or a `split` node with the groups and the
focused index; every entry gains `pinned`. Version 1 is still read. A snapshot with a pinned Tab after
an unpinned one, a pinned preview, an empty group in a split, or more groups than `MaxGroups` is refused
whole.

**Navigation.** The planned `ITabNavigator` was not needed: pages keep calling `NavigationManager`, and
that always acts on the focused group. See the limit below.

**Still to come.** Stacked groups, drag to reorder and to move between groups, three or more groups, overflow
(section 8). Known limit: a page that navigates itself while it is in the unfocused pane (a Room redirecting
after it is archived) moves the focused pane, because the router has one location. See `known-limits.md`.
