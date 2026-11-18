# Finding your way around

Huddle's window works like a code editor: an Activity bar, sidebars, tabs and one search box for
everything. For step-by-step help, see the
[User Guide](../../Huddle.UserGuide.md#a-quick-tour-of-the-screen).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Search everything

Press `Ctrl+K` or `/`, then type at least 3 characters. One box finds Teams, Agents, Library files and
their contents, Tasks, and Rooms down to the matching Message.

| To | Do this |
| --- | --- |
| Narrow the search | Use a chip, or the prefix `team:`, `agent:`, `lib:`, `task:` or `room:` |
| Open in this tab | `Enter` |
| Open in a new tab | `Ctrl+Enter` |

## Move between areas

The **Activity bar** at the far left starts with the **Feed** icon, always first. Below it come the buttons that
switch the sidebar between **Rooms**, **Teams**, **Agents**, **Tasks**, **Library** and **Scheduled**, with
**Archived** and **Settings** at the bottom. Selecting the active button folds the sidebar away.

A tile for each Team sits between the Feed and those buttons. It narrows the sidebar to that one Team. Right-click
a tile to remove it or turn them all off, or open the Team's page and choose **Hide shortcut** or **Show shortcut**.

## Work in tabs

- **Preview.** One click opens an italic preview tab that the next click replaces.
- **Keep.** Double-click, or type on the page, to keep a tab.
- **Pin** a tab, or **close** it with ×, a middle-click or `Alt+W`.
- **Split.** Move a tab into a second pane to see two pages at once.
- **Come back.** Tabs and panes return after a reload or restart, per browser.

## Use the Right sidebar

The panel on the right changes with the page:

| On | Panels |
| --- | --- |
| A chat Room | **Members**, **Tasks in this chat**, **Documents in this chat** |
| A Markdown note | **Outline**, **Linked documents**, **Backlinks**, **History**, **Tags** |
| A Task from a List or Board | **Task** |
| A Task's edit page | **Activity**, **Blocked by**, **Blocks**, **Linked documents** |
| Anything, from the bell | **Notifications** |

The toggle pulses once when a panel becomes available. Both sidebars can be resized, and remember their
state.

## Use the Top Bar

The bar shows the focused tab's trail, with each crumb clickable, plus search, the light/dark switch, the
Notifications bell and the Right sidebar toggle.

## In more detail

### Search in depth

`/` works only when no text field has focus. `Esc` closes the box and returns you to where you were.

| Key | What it does |
| --- | --- |
| `Alt+1` to `Alt+6` | Pick a chip |
| `Ctrl+Left`, `Ctrl+Right` | Move between chips |

- **Matching** ignores case and looks for the exact phrase you typed.
- **The 3-character minimum** counts after any prefix, so `task:fi` waits and `task:fix` searches.
- **Library** matches contents as well as names, for entries up to 1 MB, and note aliases.
- **Tasks** match the title, assignee, Action and the first 4,000 characters of the description, with title hits first.
- **Result counts.** **All** shows up to five per group. One chip shows up to 50.
- **Older items.** Closed Tasks and archived chats come last, in grey.
- **What opens.** An Agent opens its card. A Room opens at the matching Message, highlighted for a moment.
- **No index.** Every search reads fresh, so new Messages and files appear straight away.
- **Agents search too.** An Agent's search lists every matching Message, not only the newest one in each Room, and
  can match a pattern. The Chief of Staff searches every Room; other Agents search the Rooms they are in unless they ask for all.
- **`Results may be incomplete.`** A limit was reached, so narrow the search or pick one chip.

### Sidebar views

| Button | What the sidebar shows |
| --- | --- |
| **Rooms** | The **Needs you** card, then **Group by: All \| Team**. **All** has **Needs you** and **All** folders. **Team** puts every Room in a folder per Team, in name order |
| **Teams** | One Team at a time, the one whose page you have open, with a Team selector, its **Projects**, and the **Rooms**, **Tasks** and **Library** of what you select. See [The Teams sidebar](#the-teams-sidebar) |
| **Agents** | A link to the page listing every Agent |
| **Tasks** | **All Tasks**, **My Tasks**, your Views and **New view**, listed right at the top with no group over them |
| **Library** | The folders and files of every root, with a search box, **New note** and **New folder** |
| **Scheduled** | Every Job, and **New job** |

**Archived** opens one page with **Teams**, **Projects** and **Rooms** tabs, where you **Restore** what you
archived. Remote access is a tab in **Settings**.

The sidebar resizes from 200 to 480 px, and the Right sidebar from 240 to 560 px. Both remember their state per
browser. The whole UI is drawn at 90% zoom.

### The Teams sidebar

The **Teams** button and a Team's tile show the same view, for one Team. The **Teams** button follows the Team of
the page you have open, and keeps the last one you used when the page has no Team.

- **Team selector.** The Team's name sits in a card at the top. Select **Switch team** (the arrows at its right) to
  list every Team and **Build a team**, then pick one.
- **Projects.** Under the card, **PROJECTS** lists the Team's Projects, each with its open Tasks, and its "+" starts
  a Project. Once a Project exists, a **No project** row narrows what is below to the Rooms, Tasks and files that
  belong to no Project. Select it again to go back to the whole Team.
- **Sections below.** **ROOMS**, **TASKS** and **LIBRARY** show the Team, the selected Project or **No project**.
  **ROOMS** splits into **Needs you** and **All** while a Room is waiting, and its "+" starts a new chat. **TASKS**
  lists the Task Views for that scope, and its "+" creates a View. **LIBRARY** links to the files of that scope.
- **Archiving.** The More (⋮) menus on a Team and on a Project archive it. Restore it from the **Archived** page.

### Team tiles

Selecting a tile shows that Team in the sidebar. To hide or bring back a tile, open the Team's page and choose
**Hide shortcut** or **Show shortcut** in its header, next to **Edit avatar...**, **Duplicate** and **Archive**. The
choice is remembered per browser.

### Tabs in depth

| To | Do this |
| --- | --- |
| Move to the next or previous tab | `Alt+Page Down`, `Alt+Page Up` |
| Open a tab's menu | Right-click it, or hover and press **More (⋮)** |
| Pin it | **Pin tab**, which keeps it at the start of the strip as an icon with no × |
| Split | **Move right** or **Move left**, which opens a second pane the first time |

`Ctrl+W` and `Ctrl+Tab` belong to the browser, so a page can't use them.

**With two panes:**

- The pane you last clicked is the one the address bar shows, and where new items open.
- A page already open in one pane is never opened again in the other, so opening it takes you to it.
- Closing the last tab in a pane closes the pane.

Tabs follow what they show. A Room's tab takes its new name, and closes when the Room is archived or deleted. A
Team's or Project's tab closes when that Team or Project is removed.

### Bookmarking a page

Every page has its own address:

| Address | Page |
| --- | --- |
| `/rooms/{roomId}` | A chat Room |
| `/agents` | Agents |
| `/teams/{Team}`, `/teams/{Team}/files` | A Team and its **Library** tab |
| `/teams/{Team}/no-project/files` | The files of a Team that belong to no Project |
| `/teams/{Team}/projects/{Project}` | A Project |
| `/teams/{Team}/tasks/{View}` | A View, narrowed to a Team |
| `/teams/{Team}/projects/{Project}/tasks/{View}` | A View, narrowed to a Project |
| `/teams/{Team}/no-project/tasks/{View}` | A View, narrowed to a Team's Tasks that belong to no Project |
| `/feed`, `/archived` | The Feed, and the Archived page |
| `/library` | The Library page, showing a document |
| `/tasks/{viewId}` | A saved View |
| `/tasks/item/{TaskId}` | One Task, over your last View |
| `/tasks/item/{TaskId}/edit` | One Task in its edit tab |
| `/settings/{tab}` | A Settings section |

### Right sidebar panels

| Panel | What it shows | What selecting it does |
| --- | --- | --- |
| **Members** | Everyone in the Room, Agents first, with status and context fill | Opens the member's profile |
| **Tasks in this chat** | Each Task mentioned, newest first | Opens the Task. The arrow scrolls to the Message |
| **Documents in this chat** | Each Library file mentioned, newest first | Opens the file. The arrow scrolls to the Message |
| **Outline** | The note's headings | Scrolls to the heading, or moves the cursor in **Edit** |
| **Linked documents** | The notes this one links to, with `(not found)` for a missing one | Opens the note |

Press a panel's header to fold it, and your browser remembers. With two panes, the sidebar follows the pane you last
clicked. On a page with no panels it says `Nothing to show here`, with a line on where panels appear.
