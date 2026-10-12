---
status: accepted
date: 2026-10-10
---

# A group mounts its own page, and the URL names the focused one

> **Builds on [ADR-0045](0045-a-tab-is-a-remembered-url-and-only-the-active-one-is-mounted.md).** Its
> decisions stand; this one adds the split and amends its decision 9 (no `ITabNavigator`). The
> behaviour is in section 9 of the [Tabs specification](../Huddle.Tabs-Specifications.md).

## The defect

Version 1 could show one page. The Human who wanted a Room beside the Tasks board, or a Library file
beside a Room, had to switch back and forth, and the router, which mounts exactly the page the
address names, could not be asked for two.

## The decision

**Tabs live in up to `TabService.MaxGroups` groups drawn side by side (2 today). Each group mounts the
page of its active Tab itself. The browser URL names the focused group's active Tab.**

### The decisions inside it

| # | Decision | The alternative it beat |
| --- | --- | --- |
| 1 | **Every group mounts its page through `TabPageHost` (`DynamicComponent`), resolved by `TabPageRoutes` from the same `@page` templates; the router still runs but its page is never rendered.** | Let the router render the focused page and `DynamicComponent` the other. Focusing a pane would swap which mechanism renders it, re-mounting both pages and losing the work in them. |
| 2 | **`TabPageRoutes` is a small matcher (most literal segments wins; route and query values fill `[Parameter]`s), not a copy of the router's table.** The router's `RouteTable` is internal. | Reflect into the framework. A private type that changes between releases. |
| 3 | **One Tab per key across all groups.** Opening a key open in the other group focuses it there. | Allow a page in both groups. Two live copies of one Room would double every assumption about one open Room (draft, focus, streaming view). |
| 4 | **Move Left and Move Right move a Tab between neighbouring groups; from the outermost group they open a new one while `MaxGroups` allows.** A moved Tab becomes permanent. | A separate "Split" command, or drag only. The menu icons already say "move this to the other side", and a menu works without a pointer. Reordering inside a strip is not offered; drag is the way for that. |
| 5 | **Pinned means pinned in the VS Code sense: kept at the front, icon only, permanent.** `TabService.Pin` therefore means this; the old "make permanent" call is now `Keep`. | Keep the name for both. One word for two ideas, and the menu item would be a lie. |
| 6 | **The pane that is not focused listens for the press (`@onpointerdown`, `@onfocusin`); the focused one has no handler.** Focusing navigates, so the address keeps naming the pane the Human is in. | A handler on both. A server round trip for every click in the pane the Human is using. |
| 7 | **A page reads its own address from the cascaded `TabPageContext`; `SupplyParameterFromQuery` is not used on routed pages.** The Library page's four query values are plain `[Parameter]`s filled by the resolver. | Keep reading `NavigationManager.Uri`. In the unfocused pane it is the other pane's address. |
| 8 | **Format version 2 stores a lone group as before and two or more as a `split` node with the focused index; version 1 is still read.** | A flat list per group with no tree. Stacked groups would need a migration. |
| 9 | **No `ITabNavigator`.** Pages still call `NavigationManager`, which acts on the focused group. | One navigator per group, as ADR-0045 planned. It only pays once a page in the unfocused pane must navigate its own group; no page needs that yet, and the limit below is the price. |

## Addendum: the breadcrumb bar

Superseded: the breadcrumb trail no longer lives in `TabPageHost`. It was one bar per pane, so a split
drew two and collapsed long trails to fit. The shell's `TopBar` (a fixed `MudAppBar` across the whole
window, above the menu and every pane) now draws one trail, for the focused pane's active Tab
(`TabService.Active`), so it follows focus. A crumb navigates that Tab in place
(`TabService.Navigate`), then the router is pointed at the Tab so the URL still names the focused
pane's page. The bar is laid out as three cells (trail, a middle cell for search, an end cell for
toolbar buttons) so those later features fill slots rather than reshape it.

## What this costs

- A page in the unfocused pane that navigates itself (a Room redirecting when it is archived) moves
  the focused pane, because the router has one location.
- Two Rooms side by side have not been watched live: the per-Room draft and the per-element composer
  script were written to allow it, but only the Settings and Teammates pages were seen in two panes.
- Both panes re-render on every Tab change. Cheap today; revisit if a third group is allowed.
- Dragging, stacked groups and overflow remain later work.
