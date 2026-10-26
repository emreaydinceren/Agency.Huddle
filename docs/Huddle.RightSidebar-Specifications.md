# Right sidebar: contextual panels

**Status:** the shell is built. The toggle (`S1c`) and the drawer (`S4`) shipped on
`feat/right-sidebar`. The panel framework below is **built** on `feat/right-sidebar-panels` (2026-10-16). The first build
delivers the framework and five panels: Outline, Linked documents, Tasks in this chat,
Documents in this chat and Members ([v1 scope](#v1-scope)).

The Right sidebar is the drawer on the window's right edge (`S4` in
[wireframes.md](engineering/wireframes.md#figure-1-the-shell)). It shows panels about what the
focused pane is showing: an outline of the open document, the Tasks a Room has mentioned,
the documents a note links to. This page fixes three things before any panel is written:

1. **Which panels appear** for a given page.
2. **How a panel knows what the page is showing**, and keeps up as it changes.
3. **How a panel acts on the page**, such as scrolling to a heading or opening a Task.

## Vocabulary

| Term | Meaning |
| --- | --- |
| **Right sidebar** | The drawer, `S4`. Not the left Sidebar (`S2`) and not a detail drawer (`T7`) |
| **Subject** | A small typed record a page publishes that says what it is showing: *Room ab12*, *the Markdown file `Teams/Plan.md` in root `notes`* |
| **Panel** | One titled block in the Right sidebar: a component and a descriptor that says which Subjects it applies to |
| **Panel catalog** | Every registered panel. The sidebar asks it which panels apply to the active Subject |
| **Page command** | A request from a panel to the page beside it, for an action that has no address, such as "scroll to this heading" |

## The examples this must serve

| Panel | Shown when | Shows | Acts on the page by |
| --- | --- | --- | --- |
| Outline | A Markdown document is open | Its headings, nested | Scrolling the document to a heading |
| Tasks in this chat | A Room is open | Each Task its Messages reference, with live State | Opening the Task |
| Documents in this chat | A Room is open | Each Library file its Messages reference | Opening the file; jumping to the Message |
| Linked documents | A Markdown document is open | The notes it links to | Opening the note |
| Members | A Room is open | Each member of the Room, with live status | Opening the member's profile |

Several panels share a Subject, and no panel is about only one page. That is the shape
that decides the design.

## Decisions

### D-1. A page says what it shows, and a panel says what it applies to

A page publishes one **Subject**. Each panel declares which Subjects it applies to.
The sidebar draws every panel that applies to the active Tab's Subject. No page names a
panel.

**Why:** panels and pages are a many-to-many relationship. Room panels and document panels
will keep arriving. If a page had to list its panels, every new panel would mean editing
every page it belongs on, and a page in the Team area that shows a document would quietly
miss the Outline. With this split, adding a panel is one registration and no page changes.

**Rejected:** pages pushing their own blocks. This is the `RightSidebarContent` component
built with the shell; see [What changes in the built shell](#what-changes-in-the-built-shell).
It suits a one-off, but it puts the decision in the wrong place.

### D-2. A Subject is a closed set of immutable records, published by the page

```csharp
public abstract record PageSubject
{
    /// <summary>What the Subject is about, without its live state: "room:ab12", "doc:notes/Teams/Plan.md".</summary>
    public abstract string Key { get; }
}

public sealed record RoomSubject(string RoomId) : PageSubject { … }
public sealed record DocumentSubject(LibraryPath Path, LibraryFileKind Kind, string? Text) : PageSubject { … }
```

- **Typed, never a string bag.** A panel pattern-matches (`subject is DocumentSubject { Kind: LibraryFileKind.Markdown }`),
  and the compiler finds every panel when a Subject gains a field.
- **Published by the page, not derived from the Tab's URL.** The URL is not enough. Every
  Library Tab has the key `library`, whatever file it shows. A file's format is known only
  after it loads. A Room page that redirects knows its real Room before the URL settles.
  The page knows the truth, so the page says it.
- **Published with a component,** in the same way and for the same reason as the built
  `RightSidebarContent`: `<PageSubjectSource Value="@this.subject" />` anywhere in the page.
  It reads the cascaded `TabPageContext` for the Tab id, so two panes keep two Subjects
  apart. A page with no source has no Subject, and the sidebar shows its empty state.
- **One Subject per Tab.** A page that shows two things, such as the Library explorer with
  a tree and a document, publishes the one the Human is reading.

### D-3. A panel is a descriptor and a component, chosen by a pure function

```csharp
public sealed record SidebarPanel(
    string Id,                         // stable; keys the collapsed state
    string Title,
    string Icon,
    int Order,
    Func<PageSubject, bool> AppliesTo,
    Type Component,                    // receives [Parameter] PageSubject Subject and [Parameter] string TabId
    Func<TeamOptions, bool>? IsEnabled = null);   // null: always; else a feature switch such as Library.Enabled

services.AddSidebarPanel(new SidebarPanel("outline", "Outline", Icons.Material.Filled.Toc, 10,
    static subject => subject is DocumentSubject { Kind: LibraryFileKind.Markdown },
    typeof(OutlinePanel), static options => options.Library.Enabled));
```

`SidebarPanelCatalog.For(subject)` is `catalog.Where(p => (p.IsEnabled?.Invoke(options) ?? true) && p.AppliesTo(subject)).OrderBy(p => p.Order)`.
It is a pure function over registered data and the bound options, so "which panels does a
Room get?" is a unit test with no renderer. A panel whose feature is switched off never
appears, and is never mounted, so it never asks a store that feature gates.

**The host owns the chrome:** the header row (icon, title, an optional count, collapse).
A panel draws only its body. The panels then look alike without each one copying the
header, the same reasoning as `AgentCard` for Transcript cards. A panel reports its count
through a cascaded `SidebarPanelContext` (`SetCount(int? count)`), and the host draws it.

A panel also receives the **Tab id** of its Subject, so it can navigate that Tab
(`TabService.Navigate(tabId, …)`, D-6) or send it a Page command.

### D-4. A panel reads the stores, not the page

A panel loads its data from the same singletons the page uses, and listens to their
events, filtered to its Subject. It never reads the page component's fields.

**Why:**

- There is one source of truth. The Room page and the Tasks panel cannot disagree about a
  Task's State if both read `TaskStore`.
- A page re-render does not drive panels, and a panel still works while the page is
  loading.
- A panel can be tested with a Subject and fake stores, without mounting a page.

### D-5. Only state that no store holds rides on the Subject

Some of what the left side shows exists only in the page. The clearest case is an
unsaved edit in the Library editor, which the Outline should follow. That state travels
as a field on the Subject, re-published when it changes (`DocumentSubject.Text`).

The rule: **add a field to a Subject only when no store can answer it.** A Room's
Messages, a Task's State and a note's links are in stores, so they stay off the Subject.

To make re-publishing cheap, the host keys each mounted panel by `panel.Id` plus
`subject.Key`, never by the whole record:

- **Same Key, new snapshot:** the panel gets new parameters. Typing in the editor
  updates the Outline without remounting it.
- **New Key:** the panel remounts. Switching Rooms never shows the last Room's Tasks for
  a frame.

### D-6. A panel acts on the page by address first, and by Page command only when there is no address

| Need | Mechanism | Example |
| --- | --- | --- |
| Show something that has a page | Navigate to its address; the Tab rules decide preview or reuse | Open Task `SUPP-0010`; open a linked note |
| Show a place that already has an address | Navigate the **same Tab** to the address (`TabService.Navigate`) | Jump to a Message with `?message=` |
| A view action with no address | Send a **Page command** to the Subject's Tab | Scroll the document to a heading |

```csharp
public abstract record PageCommand;
public sealed record ScrollToHeading(string HeadingId, int Line) : PageCommand;

pageCommands.Send(tabId, new ScrollToHeading("doc-h-plan-risks", 12));   // from the panel
<PageCommandHandler OnCommand="this.HandleAsync" />                 // in the page
```

- A command nobody handles is a no-op, never an error. The page may have moved on.
- Commands are not stored and not replayed. A command arriving after a remount is lost by
  design.
- **Why not a `#fragment` URL for headings:** `TabKeys.ToRelative` drops fragments. A
  scroll is not a place worth a history entry or a new Tab. Adding fragments to Tab
  identity would ripple through the Tabs spec for one panel.

### D-7. Panels live only while they are seen

- **Mounting:** a panel is mounted only while the sidebar is open **and** it applies to
  the active Subject. Closing the sidebar or switching Tabs disposes it.
- **Disposal:** every load takes a `CancellationToken` that `Dispose` cancels. Every
  event subscription is removed in `Dispose`, as [rules.md](engineering/rules.md) requires
  of singleton hubs.
- **Two panes:** the sidebar follows `TabService.Active`, the focused pane's Tab. This is
  already built and tested.

### D-8. An empty panel stays, collapsed to one line

A panel that applies but has nothing to show, such as a Room that mentions no Tasks,
shows its header with a count of 0 and one line saying what would appear there. It does
not disappear.

**Why:** a panel that comes and goes as Messages arrive moves everything below it, and a
Human cannot tell "nothing mentioned yet" from "this panel does not exist for chats".

The collapsed state of each panel is remembered per viewer by `panel.Id`. It is a
per-viewer convenience, so it lives in local storage behind the usual try/catch.

## The five v1 panels

### Outline

| | |
| --- | --- |
| **Applies to** | `DocumentSubject { Kind: Markdown }`, while the Library is enabled |
| **Data** | The headings of `Subject.Text` with the frontmatter split off as `LibraryDocument` does, parsed with the **same pipeline** the renderer uses. Each heading's id comes from one shared function, so it is the id in the rendered page |
| **Refresh** | A new `DocumentSubject` (same Key). In Split mode `Text` follows the editor 300 ms after typing stops (the existing `PreviewTextChanged`). In Read and Edit modes it is the loaded or last saved text: text crosses from the editor only on save and on mode switch (`library-editor.js`), and this panel does not change that |
| **Acts** | `ScrollToHeading(id, line)`. In Read and Split the page scrolls the rendered heading into view. In Edit it reveals the heading's line in the editor |
| **Missing today** | Rendered headings have no `id`. They get one only on the note render path (`MarkdownRenderer.Render` with a `from` note), never in chat, where Messages would repeat ids. The id is `doc-h-` plus a lowercase slug, with `-2`, `-3` for repeats, so it cannot collide with an app element. Markdig's `UseAutoIdentifiers` is not used, because the pipeline is shared with chat and the parsers. Also needed: `huddleLibrary.scrollToHeading` in `app.js` and a `revealLine` on the editor handle in `library-editor.js` |

### Tasks in this chat

| | |
| --- | --- |
| **Applies to** | `RoomSubject` |
| **Data** | The distinct `MessageReference { Kind: Task }` across the Room's Messages (`IChatStore.ReadAllAsync`), re-resolved through `ITaskReferenceResolver` for the current State and title |
| **Refresh** | `RoomEvents.MessagePosted` for this Room adds new ones; `TaskEvents.TaskChanged` and `TasksReloaded` refresh State |
| **Acts** | Open `/tasks/item/{id}`; optionally jump to the first Message that mentioned it (`?message=`) |
| **Missing today** | Nothing structural. Messages from before reference snapshots existed carry no `References`; fall back to `ReferenceScanner.Scan(text)` for those |

### Documents in this chat

| | |
| --- | --- |
| **Applies to** | `RoomSubject` |
| **Data** | The distinct `MessageReference { Kind: LibraryFile }`, resolved for existence. A gone file is listed as gone, as `MessageReferences` does |
| **Refresh** | `RoomEvents.MessagePosted` for this Room; `WikiLinkIndex.Invalidated` for renames, moves and deletes |
| **Acts** | Open the file in the Library; jump to the Message |
| **Missing today** | Nothing structural; same fallback for old Messages as above |

### Linked documents

| | |
| --- | --- |
| **Applies to** | `DocumentSubject { Format: Markdown }` |
| **Data** | The note's outgoing **wikilinks** from `WikiLinkIndex`, resolved. A link to a note that does not exist is listed as unresolved, not hidden. Relative `[text](other.md)` links are out of v1 |
| **Refresh** | `WikiLinkIndex.Invalidated` for the note's root, and a new `DocumentSubject` (a save republishes it) |
| **Acts** | Open the linked note |
| **Missing today** | `LibraryChange` is an `EventCallback` inside the explorer, not an event a panel can hear, so `WikiLinkIndex` gains `internal event Action<string>? Invalidated` (the root id), raised **outside its lock** by `Invalidate` and once per root by a roots change. `Invalidate` is already called on every save (`LibraryDocument`) and every file operation (`LibraryFileService`). `WikiLinkIndex` also keeps each note's links privately. It gains an `internal LinksFrom(note)`, the outgoing mirror of the `LinksTo(target)` and `Backlinks(note)` lookups it already has. Backlinks stay in `BacklinksPanel` inside `LibraryDocument` |

### Members

| | |
| --- | --- |
| **Applies to** | `RoomSubject` |
| **Data** | `ITeamDirectory.GetRoomMembersAsync(roomId)`: Agents first, then Humans, each with its avatar (`AvatarStore`) and status dot from `PersonaStatusResolver` (pipe liveness, then reported health), as `TeamRoster` shows them. An Agent's row also carries a **context meter**: how full its context window is in this Room, from `ContextFill.Get(agentId, roomId)`, as a whole percent (`sidebar-row-meta`, `members-context`) and a thin `MudProgressLinear` (`members-context-bar`) under the name. The bar is Primary below 75%, Warning from 75% and Error from 90% (`ContextFillBand`; sessions auto-compact near 97%). A reading of under 1% reads "<1%". No reading yet, or an Adapter that reports no window size, reads "—" with no bar. A Human's row has no meter. Rows stay alphabetical; they are never sorted by fill. In shared mode (`SessionPerRoom: false`) one session serves every Room, so its single reading repeats in each of the Agent's Rooms and its tooltip says so |
| **Refresh** | `RoomEvents.RoomsChanged` reloads the member list (an invite or a removal); `PersonaHealth.Changed` and `IAgentGateway.PresenceChanged` redraw the dots; `AvatarStore.AvatarsChanged` redraws the avatars; `ContextFill.Changed` redraws the meters. `RoomSession` reports on every usage update; a fresh session open, a Welcome and a stopped runner clear the reading, while an idle close keeps it |
| **Acts** | Each row is a `ProfileAvatar` with the member's name, so a click opens the profile popover (Edit, Message), the same one the Room header opens |
| **Missing today** | Nothing structural. The Room header's avatar strip stays as a glance; the panel is the full list |

## What changes in the built shell

| Built | Becomes |
| --- | --- |
| `RightSidebarService`: open state plus per-Tab `RenderFragment` sections | Open state plus **per-Tab Subjects** |
| `RightSidebarContent` (a page pushes a block) | `PageSubjectSource` (a page publishes its Subject). No page uses `RightSidebarContent` yet, so it can be removed rather than kept beside the catalog |
| `RightSidebar` draws the sections | Draws `SidebarPanelCatalog.For(subject)` through `DynamicComponent`, keyed by `panel.Id` and `subject.Key`, inside the host's panel chrome |
| `TabPageContext(Url, TabId)` | Unchanged; it is how a source and a handler find their Tab |

The toggle, the drawer, its CSS and `wireframes.md`'s `S4` row stand as they are. `S4`'s
row gains the panel header once the chrome exists.

## Testing

- **The catalog:** given each Subject, the panels returned and their order. This is a pure
  function, tested with no renderer.
- **Each panel:** bUnit, with a Subject parameter and fake stores. Pin its data, its
  refresh on the right event (and **not** on another Room's event), and its action.
- **The host:** keyed remount on a new Key, parameter update on the same Key, disposal
  when closed, and following focus between panes. The stub-page pattern in
  `RightSidebarTests` already covers the last.
- **Page commands:** an unhandled command is a no-op; a handled one reaches only its own
  Tab's page.

## V1 scope

Settled on 2026-10-16:

| Question | Decision |
| --- | --- |
| Which panels | Outline, Linked documents, Tasks in this chat, Documents in this chat, Members |
| Backlinks | Stay in `BacklinksPanel` inside `LibraryDocument`. Moving them is a later change |
| Relative links | Wikilinks only. Relative `[text](other.md)` links are a later `WikiLinkIndex` change |
| Room header strip | Stays. Members adds the full list; it does not replace the glance |
| Outline source | Follows unsaved edits through `DocumentSubject.Text` in Split mode. Read and Edit show the loaded or saved text, because the editor sends text only on save and mode switch |
| Feature switches | Outline, Linked documents and Documents in this chat need `Library.Enabled`; Tasks in this chat needs `Tasks.Enabled`; Members needs neither |
| Subjects | `RoomSubject` and `DocumentSubject` only. `TaskSubject` and the others wait for a panel that needs them |
| Page commands | Built, because Outline needs `ScrollToHeading` |
| Open state | Unchanged: starts closed in every circuit |

## Open questions

1. **Later Subjects:** the Task page, a Task View, the Team page and a Project are the
   obvious next Subjects. Which panel arrives first decides which one is defined first.
