# Tabs: specification

Status: **designed 2026-10-10**, delivery on `feat/tabs`. Owner: chat surface.

## 1. The idea

Everything the Human opens from the left menu opens in a **Tab**, as in VS Code. A click opens a
**preview Tab** (its title in italics); it is replaced by the next preview. A double-click, or
working in the Tab, makes it **permanent** (upright title). The Library is one page and so one Tab.

Version 2 adds Tabs side by side and stacked. Version 1 does not draw that, but its model and its
persisted format already allow it (section 5).

## 2. Terms

| Term | Meaning |
| --- | --- |
| Tab | One open page: a key, a URL, a title, an icon and a preview flag. |
| Preview Tab | A Tab not yet made permanent. At most one exists per group. Shown in italics. |
| Permanent Tab | A Tab the Human kept. Never replaced by opening something else. |
| Tab key | What makes two URLs the same Tab (section 3). Opening an item whose key is open activates it. |
| Group | An ordered row of Tabs with one active. Version 1 has exactly one. |

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
| A Room is archived or deleted | Its Tab closes (`TabRoomSync`). A removed Team's or Project's Tab does not yet: the Team catalog may not be loaded when a stored Tab is restored, and closing on an empty catalog would lose good Tabs. |
| Reload or restart | Tabs, their order, the active Tab and the preview flags are restored. |

### Tab keys

| URL | Key | Why |
| --- | --- | --- |
| `/`, `/rooms/{id}` | `room:{id}` (`chat` for `/`) | `/` redirects to the first Room. |
| `/teams/{team}` and its sub-tabs | `team:{team}` | A sub-tab is in-Tab navigation. |
| `/teams/{team}/projects/{project}` and its sub-tabs | `project:{team}/{project}` | |
| `/teams/new`, `/teams/{team}/setup` | `team-setup:{team}` or `team-new` | |
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
a second implementation: each group gets its own navigator and its own page host
(`DynamicComponent`, resolved from the Tab's URL), because two groups show two pages at once and the
browser URL names only the focused one. Version 2 must also audit anything page-global that assumes
one open Room (render coalescing, `Drafts`, focus).

## 6. Library

The docked Library pane (`LibraryPaneHost`, `LibraryPane`, `LibraryPaneState`) and the Appearance
setting `LibraryPaneSide` are retired in this delivery. The full `/library` page is the only Library
surface. Wikilinks and `?library=` links keep their validation (`IsSafeLibraryHref`, the resolver)
and then open the Library Tab on the file.

## 7. Delivery plan

1. `Tabs/` domain: `Tab`, `TabKeys`, `TabService`, `TabSnapshot`; unit tests.
2. `TabStrip`, `NavTabLink`; wire `MainLayout` and the left-menu components; bUnit tests.
3. Title refresh, close on archive and delete, the per-Room draft, pin on typing. (Done.)
4. Retire the Library pane and the pane-side setting; reroute links to the Tab.
5. Persistence, keyboard shortcuts (`Ctrl+W`, `Ctrl+Tab`).
6. User guide, manual tests in `shell-nav.md`, ADR.

## 8. Not in version 1

Drag to reorder, side-by-side and stacked groups, per-Tab history, and everything about a crowded
strip (decided 2026-10-10 to leave for version 2):

- **Overflow.** Either wrap to further rows (an Appearance option; cheap, but the strip height changes
  as Tabs open and close, and narrow groups would wrap to many rows) or one row with a minimum Tab
  width of about 7rem, scrolling, and an "all Tabs" list at the right edge. Version 2 should pick one
  default when groups exist, since overflow behaves differently at narrow widths.
- **Scroll the active Tab into view** when it is activated off-screen.

Known limit until then: the strip is one row that scrolls sideways, and Tabs shrink with no lower
width, so with many open each shows little more than its icon.
