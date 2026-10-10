---
status: accepted
date: 2026-10-10
---

# A Tab is a remembered URL, and only the active one is mounted

> **Builds on [ADR-0029](0029-between-the-human-and-an-agent-the-last-write-wins.md)'s habit of
> re-reading on return.** Nothing earlier changes, except that the docked Library pane of the
> [Library specification](../Huddle.Library-Specifications.md) §6.9 is retired. The full design,
> with the behaviour table and the delivery plan, is in the
> [Tabs specification](../Huddle.Tabs-Specifications.md).

## The defect

The left menu replaced the page. Reaching a second Room meant losing the first: its scroll, its
half-written message, and the place in the menu you came from. The Library was the one exception, a
docked pane beside the page, so there were two ways to show a thing and two sets of rules for
closing it.

## The decision

**Everything opened from the left menu opens in a Tab. A Tab is a remembered URL with a title, an
icon and a preview flag. Only the active Tab is a live component.**

The URL stays the truth for the active Tab. `TabService` never navigates; the components that call
it do. A menu click opens a *preview* Tab (italic) that the next preview replaces; a double-click, or
working in the Tab, keeps it. Tabs are stored in the browser and restored after the first render,
and the address in the bar decides which one is active.

### The decisions inside it

Each beat a named alternative, kept here so a later reader who proposes it again can see why it was
not taken.

| # | Decision | The alternative it beat |
| --- | --- | --- |
| 1 | **Only the active Tab is mounted.** What survives a switch is what a page already re-reads from its stores, plus the Composer's unsent text. | Keep every Tab mounted and hide it. A hidden Chat still subscribes to `DraftChanged`, so streaming re-renders Tabs nobody sees, and the router would have to be replaced by a `DynamicComponent` host. |
| 2 | **The Tab key is derived from the URL, and a Team's sub-tabs, a Settings tab and a Library query share one key.** Opening a key that is open activates it. | Key by the full URL. Every in-page navigation (Team to its Tasks sub-tab, a Library root re-scope) would open another Tab. |
| 3 | **`NavTabLink` offers the page to `TabService` and the link still navigates; `TabHost` also offers every location the router reaches.** | Open Tabs only from `LocationChanged`. Clicking the menu item for the page already showing raises no location change, so the last closed Tab could never be reopened. Open Tabs only from the menu: Back, a link in a Message and a typed URL would not get a Tab. |
| 4 | **The Library is one page and one Tab, and the docked pane is retired.** A `?library=` link goes through `LibraryLinkRouter` to `/library?root=&path=`. | Keep the pane beside Tabs. Two surfaces for one thing, and a split inside a Tab. |
| 5 | **A Room's draft is kept by the Composer's script in `sessionStorage`, per Room.** | A server-side per-Tab state bag. A round trip per keystroke for text only the browser needs; and a draft that follows the Room also stops text leaking from one Room into the next, which it did before. |
| 6 | **Tabs persist in `localStorage` as versioned JSON, with a layout node as the root.** The key is not stored, a bad value is ignored whole, and nothing is written until the restore has run. | A flat list. Version 2's side-by-side groups would need a migration. Writing before restoring would overwrite what was stored with an empty start. |
| 7 | **An empty Team catalog is read as "not loaded yet".** Team and Project Tabs close only when the catalog announces a change and still lists a Team. | Close on any pass. A restored Tab would be lost at every start while the stores are still scanning. The cost, accepted: removing the very last Team leaves its Tab open. |
| 8 | **Shortcuts are Alt+W, Alt+Page Down and Alt+Page Up.** | Ctrl+W and Ctrl+Tab. The browser keeps them and a page cannot take them. |
| 9 | **No `ITabNavigator` in version 1.** Pages keep using `NavigationManager`. | Add the abstraction now for version 2. One implementation that only delegates adds nothing, and it would force every page's test fixture to register it. Version 2 adds it when it has a second implementation. |

## What this costs

- Two browser tabs on the app share one list in `localStorage`; the last to change wins.
- With many Tabs open each shrinks to little more than its icon, and the strip scrolls sideways
  without bringing the active Tab into view. Overflow handling is version 2 work.
- A page's Tab does not keep its scroll position.
- Editing a Task and saving a setting do not yet make a preview Tab permanent; typing in the
  Composer and a double-click do.

## Version 2

Side-by-side and stacked groups need two live pages at once, so they need a page host per group
(`DynamicComponent`, resolved from the Tab's URL), a navigator per group, and an audit of anything
page-global that assumes one open Room. The stored layout is already a tree for that reason.
