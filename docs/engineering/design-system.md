# Design system

How Agency.Huddle looks and the rules that keep it consistent: tokens, type, spacing,
components, layout containers, overlays and copy. Read it before you design a screen, add
a component or write a stylesheet for `src/Huddle.App`.
[UXDesignGuidelines.md](UXDesignGuidelines.md) is the hub that routes you here and to
every other UI resource.

**These rules are binding for new and changed code.** Where today's code does not follow
a rule yet, the gap is a row in [UX-Issues.md](UX-Issues.md), and the row's ID is named
next to the rule. Don't copy a pattern from the code without checking it against this
page: the code still holds drift, and this page wins. If a rule is wrong for your case,
change the rule in the same change and say why. Don't quietly make an exception.

[rules.md](rules.md) stays the binding list for behaviour the compiler can't check. This
page is the binding list for how things look, read and behave on screen.

## The one idea

**MudBlazor is the design system.** Huddle does not keep a token file, a component
library or a second stylesheet beside it. A *Theme* is a `MudTheme` object
([ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md)), MudBlazor turns it into
`--mud-*` CSS custom properties, and `app.css` and every scoped stylesheet read only
those. Everything below follows from that.

| Layer | Owner | Where |
| --- | --- | --- |
| Palette, typography, corner radius, elevation | `MudTheme` built in C# | `src/Huddle.App/Themes/` |
| Components | MudBlazor 9.10.0, used with the house defaults below | `src/Huddle.App/Components/` |
| Compositions and Huddle-only widgets | Plain CSS over `--mud-*` variables | `wwwroot/app.css`, `*.razor.css` |
| The one non-MudBlazor token | `--font-mono` | `wwwroot/app-vars.css` |

[ADR-0009](../adr/0009-a-theme-is-a-stylesheet-layered-over-the-tokens.md) describes the
earlier design, a `theme.css` of 39 tokens. It is superseded; do not rebuild it.

**Consistency comes from one place per decision.** A colour comes from a palette role, a
status colour from a pure mapping function, an icon from an icon map, an avatar from
`TeammateAvatar`, and a recurring shape from a shared component. If you are about to
write the same markup or declaration block a second time, extract it instead. The shared
components still to be built are listed in
[Components to build](#components-to-build).

## Foundations

### Colour

Colour comes from the palette of the active Theme. Use a semantic role, never a value.

| Role | MudBlazor variable | Use it for |
| --- | --- | --- |
| Ground | `--mud-palette-background` | The page behind everything |
| Surface | `--mud-palette-surface` | Cards, inputs, dialogs, the raised layer |
| Sidebar | `--mud-palette-drawer-background` | The left drawer |
| Quiet fill | `--mud-palette-background-gray` | The segmented control's track, chips, preview blocks, the step between ground and surface |
| Text | `--mud-palette-text-primary`, `-secondary`, `-disabled` | Body, supporting text, inert text |
| Accent | `--mud-palette-primary` | Active nav link, primary action, the Human's bubble tint, links |
| Status | `-success`, `-warning`, `-error`, `-info`, `-secondary` | State, never decoration |

**Lines.** Each kind of line has one token and one width (UX-061, UX-062).

| Line | Rule | Examples |
| --- | --- | --- |
| Chrome | `1px solid var(--mud-palette-lines-default)` | Top bar, tab strip, room header, pane splitter, drawer edge |
| Card or row border | `1px solid var(--mud-palette-divider)` | Cards, tiles, list-row separators, floating panels |
| Rule inside a card | `1px solid var(--mud-palette-divider-light)` | A sub-section inside a card or dialog |
| Input border | MudBlazor's own (`--mud-palette-lines-inputs`) | Never restyle it |

**States.** Each interaction state has one treatment (UX-064 to UX-067).

| State | Treatment |
| --- | --- |
| Hover | `background: var(--mud-palette-action-default-hover)`. Not `table-hover`, except inside a table |
| Selected | `color-mix(in srgb, var(--mud-palette-primary) 14%, var(--mud-palette-surface))` |
| Selected and hovered | The same mix at 24% |
| Accent-tinted surface (the Human's bubble) | 14% fill with a 28% border, over `surface`. Never a solid accent fill |
| Drop target | `1px dashed var(--mud-palette-primary)` plus the hover background |
| De-emphasised text | `color: var(--mud-palette-text-secondary)`, or `text-disabled` for inert text. Don't use opacity |
| Archived or gone | `opacity: 0.6` on the whole item, the one use of opacity for muting |

Always mix a tint over `surface`, never over `transparent`, so it reads the same on every
ground.

**How to use colour in a component:**

- Write `Color="Color.Primary"`, not a hex. Write `var(--mud-palette-primary)` in CSS.
- Never write `Style="color: #..."` in a `.razor` file. Per-Teammate avatar colours are
  user data and arrive as an inline `style` built in C#; a test still forbids a literal.
- Don't colour body text with a status colour. Status colours mark an edge, a chip, a dot,
  an icon or a short value such as a date. A whole card of orange text fails contrast and
  hides the state (UX-043).

**Status colours carry the same meaning everywhere.** A status colour is chosen by a pure
mapping function, never at a call site (UX-V14).

| Subject | Mapping | Source |
| --- | --- | --- |
| Task state | Backlog default, To Do info, In Progress warning, Review secondary, Done success, Cancelled/Duplicate/Rejected error | [`TaskColors.cs`](../../src/Huddle.App/Components/Tasks/TaskColors.cs) |
| Task priority | Low default, Medium info, High warning, Urgent error | `TaskColors.cs` (no caller yet: UX-045) |
| Task due date | Overdue is error, on the date and its icon only | Add it to `TaskColors.cs` (UX-043) |
| Presence | Awake success, Asleep default, Offline error | `TaskColors.cs` |
| Teammate health | Online success, Degraded warning, Starting warning-lighten, Offline error | [`StatusDot.razor`](../../src/Huddle.App/Components/Shared/StatusDot.razor) |
| Needs You reason, search notice | Not yet mapped | Move into a sibling of `TaskColors.cs` (UX-V14) |

A status colour never stands alone: a chip has its text, a dot has an accessible name
(UX-V08).

**Contrast is tested.** `ThemeCatalogTests` holds every Theme to six pairs, including
`Primary` against both Background and Surface at 4.5:1 (WCAG 1.4.3). The Human's accent
colour is run through `AccentPalette.ClampForContrast`, which darkens or lightens it
until it clears the same floor. A new palette that fails these tests does not ship. The
tests cover palettes, not tinted states; see [Known gaps](#known-gaps).

**The shipped palettes.** Huddle Light and Huddle Dark are authored in
[`HuddleTheme.cs`](../../src/Huddle.App/Themes/HuddleTheme.cs). Their neutrals are warm,
low-contrast greys and near-neutral charcoals sampled from the Claude desktop app.
`Primary` is a teal in light (`#007c85`) and a mauve in dark (`#c07bc3`). The catalog
also holds an accent pair whose `Primary` the Human picks, and 17 palettes converted from
VS Code ([ADR-0016](../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md)).
All 21 sit in three picker groups: Light, Dark and High Contrast.

**One palette per Theme.** A Theme carries one palette and its own `ThemeMode`; the mode
is derived from the Theme and from nothing else
([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). The Human picks one light
and one dark Theme, and the top bar's `ThemeModeToggle` switches between them
([ADR-0054](../adr/0054-a-light-theme-and-a-dark-theme.md)).

### Typography

| Aspect | Value |
| --- | --- |
| UI face | Inter, then Helvetica Neue, then `sans-serif`; loaded from Google Fonts in `App.razor` |
| Monospace | `--font-mono`: `ui-monospace`, Cascadia Mono, Consolas, Courier New |
| Case | Sentence case everywhere. `Typo.button` is `text-transform: none`; tab rails must be too (UX-009) |
| Heading override | `Typo.h5` is 1.375rem at weight 600 |
| Root size | `html { font-size: var(--mud-typography-default-size) }`, which is 0.875rem, so **1rem is 14px** before zoom |
| Zoom | `html { zoom: 0.9 }`: the whole UI is drawn at 90% |

Set up once in [`ThemeDefaults.cs`](../../src/Huddle.App/Themes/ThemeDefaults.cs). VS Code
themes carry no fonts, so every Theme inherits this stack.

**The type scale, by role.** Pick the role, then use its `Typo` and tag. The scale stops
at `h5`; don't use `h1` to `h4` as a `Typo`.

| Role | `Typo` | `HtmlTag` | Examples |
| --- | --- | --- | --- |
| Page title | `h5` | `h1` | "All Tasks", a Room's name (`C2`, UX-038), "Settings", a Team's name |
| Dialog or drawer title | `h6` | MudBlazor's | "New team", "Task PLAT-12" |
| Section heading inside a page or panel | `subtitle2` | `h2` | Teammates groups, Prompts groups (UX-001) |
| Card title | `subtitle1` | | A Teammate tile's name, an agent card's "Nova is asking" (UX-031) |
| Group label inside a panel, small and uppercase | `overline` | | "Your avatar", "Problems", the search group headings |
| Prose, empty states | `body1` | | Intro paragraphs, "No tasks yet." |
| Dense UI text, rows, cells | `body2` | | List rows, table cells, menu items |
| Metadata, timestamps, helper lines, counts | `caption` | | "11:48", "3 Members + you" (UX-037) |

**In stylesheets, font sizes come from the typography variables**:
`var(--mud-typography-body1-size)`, `-body2-size`, `-caption-size`, `-subtitle2-size` and
`-overline-size`. Don't write `0.8rem`, `0.85rem` or `14.5px` (UX-068, UX-V18). A
hand-written `mud-typography-*` class on a `<span>` is a `MudText` with `HtmlTag="span"`
(UX-V31).

**The sidebar is the one exception.** `.mud-nav-link-text` is 15px, 14px inside a
`MudNavGroup`, and a group header adds weight 600 in the secondary text colour
([app.css](../../src/Huddle.App/wwwroot/app.css) "Left menu").

### Spacing, density and size

**Density is the default, and it is set on every control, not left to MudBlazor's
default.** MudBlazor's default `Size` is `Medium`, so a `MudButton` with no `Size`
renders at 30px against 25px for Small (UX-V01).

| Control | Required |
| --- | --- |
| `MudButton`, `MudIconButton`, `MudChip`, `MudToggleGroup` | `Size="Size.Small"` |
| `MudTextField`, `MudSelect`, `MudAutocomplete`, `MudNumericField`, `MudDatePicker` | `Variant="Variant.Outlined"`, `Margin="Margin.Dense"` |
| `MudToolBar` | `Dense="true"`, `Gutters="false"` so it lines up with the page title (UX-005) |
| `MudAlert` with one line | `Dense="true"` (UX-040) |
| `Size.Large` | Avatars on a tile and an empty pane's icon only. No large buttons |

**Spacing in markup uses MudBlazor's utility classes**, on a 4px step:

| Place | Class |
| --- | --- |
| Page container, drawer content, dialog content | `pa-4` |
| Card, panel, tile | `pa-3` |
| Between sections | `mb-4` |
| Between related fields | `mb-2` |
| A stack | `MudStack` with `Spacing="2"` for fields and `Spacing="4"` for sections |

**Spacing in stylesheets uses `rem`, on these steps only:** `0.25`, `0.375`, `0.5`,
`0.75`, `1`, `1.5` and `2rem`. With a 14px root, `1rem` is 14px, so MudBlazor's `pa-4`
(16px) and the stylesheet's `1rem` (14px) are not the same length. Don't mix them on one
element. `px` is allowed in a stylesheet only for a border width, an outline offset and
the sidebar exception (UX-073, UX-V18).

**Full-height areas divide by the zoom.** Viewport units are not zoomed, so a height or
width meant to fill the window is `calc(100vh / 0.9)`, and `100vh - x` becomes
`calc((100vh / 0.9) - x)`. Every site is listed in the comment on `html` in `app.css`, and
a new site is added there in the same change (UX-V19, UX-D07). The popover provider
cancels the zoom, so don't re-scale a menu or popover.

### Shape

| Token | Value | Use it for |
| --- | --- | --- |
| `var(--mud-default-borderradius)` | 8px in both Huddle Themes, 4px elsewhere | Cards, tiles, inputs, chips, preview blocks, drawers' inner panels |
| `calc(var(--mud-default-borderradius) * 1.5)` | 12px or 6px | Message bubbles, the Composer, larger containers |
| `calc(var(--mud-default-borderradius) * 0.75)` | 6px or 3px | An element nested inside a rounded one, such as the segmented indicator |
| `50%` | Circle | Avatars and status dots, always |
| `999px` | Pill | Count badges, pill buttons |

**No literal radius.** `4px`, `6px`, `8px`, `10px`, `12px` and `14px` don't follow a VS
Code Theme's 4px radius, so a literal card stays at 8px while everything around it
drops to 4px (UX-060).

### Elevation and layering

**Prefer a hairline to a shadow.** A page, panel, card or tile is flat with a card border:
`MudPaper Outlined="true"` with `Elevation="0"`, never the default elevation-1 shadow
(UX-013). The drawer and the app bar are `Elevation="0"`.

| Layer | Elevation | `z-index` |
| --- | --- | --- |
| Page content, cards, tiles | 0 with a card border | none, or `1`/`2` for stacking inside one component |
| A floating panel anchored to a control (Invite, the `@` picker) | `var(--mud-elevation-4)` with a card border, through one shared `.floating-panel` class (UX-063) | `var(--mud-zindex-popover)` |
| Menus and popovers | MudBlazor's own | MudBlazor's own |
| Dialogs, drawers, snackbars, tooltips | MudBlazor's own | MudBlazor's own |
| The error banner `#blazor-error-ui` | | `var(--mud-zindex-snackbar)`, so the sidebar can't cover it (UX-V20) |

Write no literal `z-index` above `2`; use a `--mud-zindex-*` variable.

### Icons

Icons are MudBlazor's Material set. A Tab stores a symbolic name and
[`TabIcons.For`](../../src/Huddle.App/Tabs/TabIcons.cs) resolves it, so persisted state
never holds SVG. An icon MudBlazor lacks comes from
[`MaterialSymbols.cs`](../../src/Huddle.App/Tabs/MaterialSymbols.cs).

**One concept, one icon; one icon, one concept.** Take an entity's icon from `TabIcons`
and an action's icon from the table below, in the weight given. A new concept gets a new
row here before it gets an icon in the code (UX-083, UX-084, UX-R26).

| Entity | Icon |
| --- | --- |
| Room (chat) | `Filled.Chat` |
| Team | `Outlined.Groups` |
| Project | `Filled.LibraryBooks`, for nothing else |
| Teammates, a Team's members | `Filled.Group` |
| Tasks | `Outlined.TaskAlt` |
| Library | `Filled.MenuBook` |
| A folder, including a Library root | `Outlined.Folder` |
| A note or document | `Filled.Description` |
| Settings | `Filled.Settings` |

| Action | Icon | Not this |
| --- | --- | --- |
| Add, New | `Filled.Add` | `Outlined.Add`, or a literal "+ " in the label |
| Close a dialog, panel or Tab; remove an item from a set the Human is building (a chip, an upload) | `Filled.Close` | Dismissing a notice |
| Delete permanently | `Filled.Delete` | `PersonRemove` |
| Remove from a Team or a list, where the thing itself survives | `Filled.PersonRemove` for a member, otherwise `Filled.RemoveCircleOutline` | `Delete` |
| Dismiss a notice or a Needs You row | `Filled.Check` | `Close` |
| Edit, Rename | `Filled.Edit` | |
| Copy text, an id or a path | `Filled.ContentCopy` | `ContentPaste` |
| Copy a link | `Filled.Link` | |
| Duplicate an entity | `Filled.FileCopy` | `ContentCopy` |
| More actions | `Filled.MoreVert` | A FAB or a chevron (UX-050b) |
| Search | `Filled.Search` | |

### Avatars and status

- **`TeammateAvatar` is the only place an avatar becomes pixels.** Never draw a raw
  `MudAvatar` for a Teammate, and never compute a monogram elsewhere (UX-051).
- **An avatar is a circle at a MudBlazor `Size`**: `Small` in rows, chips and the
  Transcript, `Medium` in pickers, `Large` on a tile. No CSS overrides its size or radius.
- **A Teammate's handle is written `@alias`**, wherever it is shown.
- **A status dot is `StatusDot`**: 0.5rem, a circle, with an accessible name and a title
  from its own label function (UX-070, UX-V08). Don't copy its state-to-class switch.

### Motion

| Motion | Rule |
| --- | --- |
| Hover or focus reveal, a small transform | `transition: opacity 120ms ease` (or the property that changes) |
| The segmented control's indicator | `300ms cubic-bezier(.4, 0, .2, 1)` |
| Anything longer, or anything that loops | Must have a `@media (prefers-reduced-motion: reduce)` branch that replaces it with a static cue. `.search-hit-flash` shows the pattern; the draft caret and ReconnectModal still lack it (UX-V06) |

Write durations in `ms`. Every hover reveal also has a `@media (hover: none)` branch
that shows the control, or it can't be reached on a touch screen (UX-075).

### Focus and accessibility

- **The focus ring is `2px solid var(--mud-palette-text-primary)`, offset 2px**, on
  `a:focus-visible` and `button:focus-visible`. MudBlazor resets the outline, so this
  rule restores it. It uses text colour and not the accent because the dark accent
  measured 1.63:1; text colour clears 3:1 in every Theme. Don't override it with the
  accent, a different width or a hover ring (UX-V07)
  ([app.css](../../src/Huddle.App/wwwroot/app.css) "Keyboard focus ring").
- **Every `MudAlert` has a role**: `role="alert"` for an interruption, `role="status"` for
  a consequence (UX-V05).
- **An icon-only control has an `aria-label` that names its target** ("Delete Grace",
  not "Delete") when it repeats per row. A tooltip alone is not enough (UX-V09).
- **State is never shown by colour alone.** Use text, an icon or `aria-pressed` with it
  (UX-V08).
- **An inert input uses `Disabled`, not `ReadOnly`.** MudBlazor 9.10.0 styles only the
  disabled state (UX-V04).
- **An avatar image has an empty `alt`** because the Teammate's name is always rendered
  beside it.
- **A drag handle also works from the keyboard**: `tabindex="0"`, `aria-value*` and the
  arrow keys (UX-V10).
- **Use the MudBlazor control** rather than a raw `<button>`, a clickable `<div>` or a bare
  `<a>` (UX-V13).

## Layout containers

The shell and its regions are in
[`MainLayout.razor`](../../src/Huddle.App/Components/Layout/MainLayout.razor), and every
container's code (`S2`, `C7a`, `T3`) is in [wireframes.md](wireframes.md).

| Region | Component | Notes |
| --- | --- | --- |
| Top bar | `TopBar` (a `MudAppBar`) | Slots: `Search`, `Actions`. Shows the breadcrumb of the focused pane |
| Sidebar | `MudDrawer`, persistent, 240px | Sections in order: Needs You, Rooms, Teams, Teammates, Tasks, Library, then Archived chats and Settings pinned to the bottom with `.nav-settings { margin-top: auto }` |
| Workspace | `MudMainContent.main-column` | A flex column, `overflow: hidden`, holding `TabHost` |
| Tab groups | `TabHost` then `TabGroupPane` | One or two panes side by side, each a `TabStrip` over a `.tab-body`. The focused pane owns the URL; the other dims its active tab |
| Page | `TabPageHost` mounts a `DynamicComponent` | Only each group's active Tab is mounted |
| Inside a page | `MudTabs` (Team page, Settings), `MudSplitPanel` (Library) | `MudDrawer` is not resizable, so use `MudSplitPanel` for two resizable panes |

### The sidebar

- **Every row is 36px with 14px text.** A top-level entry is a section header (weight
  600, secondary colour) whether it is a group or a single link (UX-014). Footer entries
  are rows too, or icon-only buttons with an `aria-label` (UX-017).
- **Rows at the same level share one indent**, set by one rule in `app.css` "Left menu".
  Within a level, either every row has an icon or none does (UX-015).
- **A 1:1 Room shows the Teammate's small avatar; a group Room shows the Room icon**
  (UX-016).
- **Sidebar action links** ("New chat", "New view", "Add folder") take the
  `nav-action-link` class: primary colour, 2px between icon and label. Don't repeat those
  rules inline.
- **Row actions are revealed on hover or focus** with a `120ms` opacity transition, and
  stay reachable by keyboard and on touch.

### A page

1. **Route it.** Make it a routable component (`@page`); `TabPageRoutes` resolves a Tab's
   URL to it. Teach `TabKeys.Describe` the URL's key, title and icon name. A page does not
   draw its own breadcrumb.
2. **Contain it.** Wrap it in a `MudContainer` with `Class="pa-4"`. Nothing sets the
   page's padding to 0 (UX-002).

   | Kind | `MaxWidth` | Pages |
   | --- | --- | --- |
   | Reading page: forms, settings, a directory | `MaxWidth.Large` | Settings, Teammates, Team setup |
   | Wide page: boards, grids, split panes, tabs that hold those | `MaxWidth.False` | Tasks, Library, a Team or Project page |

3. **Title it.** Every page opens with a page header: the title as
   `MudText Typo="Typo.h5" HtmlTag="h1"` on the left and the page's actions on the right,
   each a `Size.Small` button at its natural width, never full width. Set `<PageTitle>` to
   the same text (UX-001, UX-004). A page's toolbar sits under the header, lined up with
   the title.
4. **Fill the pane.** The page's root is `h-100`, with `min-height: 0` on a flex child so
   inner regions scroll instead of the page growing past the window (`.room-column` is the
   model). Split panes fill the height too (UX-006).
5. **Scroll inside the page, not the window.** A horizontally scrolling region keeps its
   scrollbar and headers in view (UX-007).

Two tabbed pages (Team page, Settings) use one tab-rail style: an icon per tab from
`TabIcons` or the entity table, and sentence-case labels (UX-008, UX-009). A control that
acts on one tab lives inside that tab, not above the rail (UX-010).

## Overlays

| Need | Use | Notes |
| --- | --- | --- |
| A form or a short flow | `MudDialog` through `IDialogService` | Pass an id or a snapshot, not live objects: an open dialog's parameters are frozen |
| A destructive yes or no | The shared confirm (see [Confirming](#confirming)) | Not a custom dialog per site |
| A destructive yes or no on a list row | `InlineConfirm` | Swaps in for the row |
| A side panel over a page | `MudDrawer`, `Variant.Temporary`, `Anchor.End` | Task detail and the View editor |
| A transient result | `ISnackbar` | Always with a `key` |
| A menu or picker | `MudMenu`, `MudPopover` | The popover provider cancels the 90% zoom; don't re-scale |

### Dialog anatomy

| Part | Rule |
| --- | --- |
| Options | `CloseButton = true`, `MaxWidth.Small`, `FullWidth = true` for a form; `BackdropClick = false` when it holds typed input. Don't hand-build a close button in `TitleContent` (UX-022, UX-023) |
| Title | `Typo.h6`: a verb phrase for an action ("New team", "Rename note") or the item's name for a view. When a caller passes a title, show it |
| Fields | `Variant.Outlined`, `Margin.Dense`. `AutoFocus` on the first field. In a single-field dialog, Enter submits (UX-024) |
| Validation | Live, through the feature's pure validator, shown with `Error` and `ErrorText` on the field. Disable the primary action while invalid |
| An error from the action | `MudAlert Severity.Error Dense role="alert"` above the actions. Not a raw `<p>` or a `MudText` (UX-025) |
| Actions | Right-aligned in this order: **Cancel** (`Variant.Text`), then **Back** if there is one, then the **primary action** (`Variant.Filled`, `Color.Primary`, or `Color.Error` when it destroys). All `Size.Small` (UX-020, UX-021) |
| Busy | The primary action disables and shows a busy label ("Saving…") or a spinner |
| Picker dialog | No actions row; `CloseButton = true` is the way out |

### Drawer anatomy

A side drawer (`T7`, the View editor) has a header row with its title (`Typo.h6`) and a
close `MudIconButton`, content in `pa-4` with `MudStack Spacing="4"`, and its actions at
the bottom in the dialog order. A resizable drawer uses one shared handle (UX-048,
UX-R05).

### In-place action rows

Cards in the Transcript (`C8`), notices with actions (`C9c`) and banners lay their actions
out **left-aligned, primary first**: the Filled primary, then Outlined secondaries, then
Text quiet actions. This differs from dialogs on purpose: a left-aligned row reads from
its first button, and a dialog's footer reads from the right (UX-032).

### Confirming

- **Anything that can't be undone asks first**, including removing a Library root
  (UX-026).
- **The confirm button repeats the verb**: "Delete", "Remove", "Reset". Never "Confirm",
  "OK" or "Yes, …" (UX-080).
- **The title is a question ending in "?"**: "Delete 'Notes'?". One sentence of body
  says what is lost.
- A destructive confirm button is `Color.Error`. All destructive confirms go through one
  shared confirm component (UX-R12); a list row uses `InlineConfirm` (UX-R13).

### Notices and snackbars

- **A notice is a `MudAlert`** with a role, `Dense` when it is one line, placed in the
  page's notice zone (`C9`, `T9`, `L3d`). Not a styled `<div>` (UX-056, UX-V13).
- **A snackbar passes a `key`** so repeats collapse (UX-V25), and carries an action when
  there is a way forward, such as "Open task" or "Move anyway" (UX-V26).

## Components

### House defaults

Start from the pattern in the file the table names; the full list is in
[MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md).

| Need | Component and defaults |
| --- | --- |
| Button | `MudButton Size="Size.Small"`. `Variant.Filled` for the one primary action per view, `Variant.Outlined` for secondary, `Variant.Text` for quiet and for Cancel |
| Icon button | `MudIconButton Size="Size.Small"`, with an `aria-label`, and a `MudTooltip` when it is not obvious |
| Text input | `MudTextField Variant="Variant.Outlined" Margin="Margin.Dense"` |
| Search field | The same, with a `Filled.Search` start adornment, `Clearable`, `Immediate`, one debounce constant, a placeholder ("Search members") and an `aria-label` (UX-055) |
| Choice | `MudSelect` and `MudAutocomplete` (Outlined, Dense), `MudRadioGroup`, `MudToggleGroup`; for a two- to five-way mode switch, `HuddleSegmentedControl` |
| Container | `MudPaper Outlined="true" Elevation="0"` for a panel or card; `MudStack` for layout |
| Status | `MudChip Size.Small`, `MudBadge`, `StatusDot` |
| Table | `MudSimpleTable` for short lists, with an empty row; `MudDataGrid` for sortable, groupable data |
| Disclosure | `MudExpansionPanels`, `MudCollapse` |

### Huddle components

| Component | Purpose | Rule |
| --- | --- | --- |
| `TeammateAvatar` | The only place an avatar becomes pixels: image, then text label, then monogram | Keep a single `MudAvatar` root; tests locate `.mud-avatar` by position |
| `StatusDot` | A coloured dot for a Teammate's health | Use it; don't copy the state-to-class switch |
| `HuddleSegmentedControl` | A pill track with a sliding indicator, up to five options | Colours come from `background-gray` and `surface` |
| `InlineConfirm` | A Confirm/Cancel row that swaps in for a list row while a destructive action awaits an answer | Confirm takes focus, Escape cancels, both buttons disable while busy. Pass `ConfirmText` |
| `ProposalCard`, `QuestionCard`, `ElicitationCard`, `RetroProposalCard`, `SprintProposalCard` | Cards an Agent puts in front of the Human | Rendered inline in the Transcript. The outcome is never card state: once decided, the card goes and the decision appears as a posted Message (Spec §6.11). Each is an outlined panel with a title and an in-place action row (UX-030 to UX-035). `QuestionCard` uses buttons with `aria-pressed` rather than radios because it owns the selection (see its header comment); `ElicitationCard` uses radios and checkboxes |
| `NeedsYouCard` | Rooms waiting on the Human | Sidebar, above the Room list |
| `Composer` and `ReferencePicker` | Message input and the `@` picker | The picker is a popover; its keyboard shortcuts are in the User Guide |
| `PageBreadcrumbs` | A pane's trail | Drawn by the shell, not by a page |
| `ChipCloud` | A wrapping cloud of filter chips, selected first | Selection reads as text and `aria-pressed`, never by colour alone. Use it wherever chips filter a list (UX-R19) |

### Components to build

These shapes recur, and today each site hand-builds its own. **The next change that
touches one of these sites builds the component and moves that site onto it**, rather
than adding another copy. Until then, follow the anatomy on this page.

| Component | Replaces | Rows |
| --- | --- | --- |
| `PageHeader` | Per-page titles and loose action buttons | UX-001, UX-004 |
| `EmptyState` | About 17 hand-written empty lines: `Typo.body1`, secondary colour, `role="status"`, a next-step sentence, an optional icon and action | UX-R20, UX-V11 |
| `NameDialog` | Rename, NamePrompt, AddFolder, NewTeam and Duplicate | UX-R10 |
| `ConfirmDialog` | `DeleteDialog` and every destructive message box | UX-R12 |
| `AgentCard` and `AgentCardBase` | The five Transcript cards' shell and busy logic | UX-R01 |
| `ResizableEndDrawer` | The Task detail and View editor drawers and their handles | UX-R05 |
| `SearchField` | The Team page, Tasks and Skill import search fields | UX-055 |
| `TaskStatusChip`, `TaskPriorityChip`, `TaskAssignee` | Board, List and search renderings of the same fields | UX-R07 |
| `TeammateLabel` | Dot, avatar and name rows in New chat and Invite | UX-R23 |

### Patterns worth copying

- **Transcript.** An Agent's message sits in a grid with an avatar gutter. The Human's
  message is a right-aligned `.message-bubble`, max `min(75%, 40rem)`, tinted with the
  accent. Day separators and system rows are slim and use secondary text. The sender's
  name, the body and the Composer use the body size; the name is set apart by weight, not
  by being smaller (UX-039).
- **Empty state.** `Typo.body1` with `role="status"`, in secondary text, saying what to do
  next ("No pages are open. Choose one from the menu."). Inline empty states take `1rem`
  padding, a full-pane one `2rem` (UX-077).
- **A list row with actions.** Actions sit in a `MoreVert` menu revealed on hover or
  focus. The same item has the same menu items and label everywhere it appears (UX-050a).

## Interface copy

Words follow [language.md](language.md). These rules decide the form.

- **Controls are sentence case**: buttons, menu items, tab labels, field labels, column
  headers and dialog titles. Only the first word is capitalised, even when a defined term
  follows: "New team", "Invite teammate", "Add folder", "New note", "Consult when" (UX-078).
- **Prose capitalises a defined term**, as language.md does: notices, helper text and
  empty states write "This Task was deleted" and "No Teammates in this Team yet".
- **One verb per meaning:**

  | Verb | Means | Not |
  | --- | --- | --- |
  | Cancel | Abandon this dialog or edit; nothing changes | Close, Dismiss |
  | Close | Put away a view; nothing is lost | Cancel |
  | Delete | Destroy permanently | Remove |
  | Remove | Take out of a set; the thing survives | Delete |
  | Archive | Hide, recoverably | |
  | Dismiss | Hide a notice or request without acting on it | Skip, Close |
  | Decline | Refuse an Agent's proposal | Reject, Leave paused |
  | Invite | Bring an existing Teammate into a Room | Add |
  | Add | Put something into a set | Create |
  | Create | The primary action of a "New …" dialog | OK |
  | Save | Write pending edits | Save to view |

- **One label per field and per action across the app**: "Team name", never "Team title"
  in one dialog and "Team name" in another (UX-079, UX-082).
- **Count with a plural helper**: "1 member", "2 members". Never "1 Members" (UX-037).
- **Show a display name, never a raw id or slug**: "Checks before merge", not
  "checks-before-merge" (UX-082).
- **Busy labels end in an ellipsis character**: "Saving…", "Sending…".
- **Copy lives in markup**, not in a CSS `content:` (UX-V23).
- **Don't say "AI"**; language.md lists it under Avoid (UX-082).

## Writing the stylesheet

1. **Put a rule in `app.css`** when it targets a MudBlazor component's internals
   (`.mud-drawer-content`, `.mud-nav-link-text`, `.mud-toggle-item`). Blazor's CSS
   isolation stamps its attribute only onto HTML elements written in your own markup, so a
   `::deep` rule under a component root never matches and fails silently. This holds for a
   Huddle component's own scoped file too (UX-V16).
2. **Put a rule in a `.razor.css`** only when it targets markup in that component.
3. **Colour and font:** `var(--mud-palette-*)` and `var(--mud-typography-*)` only. A
   literal fails `AppCss_DeclaresNoColourLiteral` or `AppCss_DeclaresNoFontFamilyLiteral`.
   The one allowed exception is `#blazor-error-ui` in `MainLayout.razor.css`.
4. **Every `var()` names a MudBlazor variable or `--font-mono`.** An unresolved property
   resolves to nothing, not to a fallback, and a surface goes invisible
   (`AppCss_UsesOnlyMudBlazorVariables`, `ScopedCss_UsesOnlyMudBlazorVariables`). That
   includes a feature-local custom property such as `--task-ref-dot` (UX-V15).
5. **Sizes, radii and layers use the tokens above**: rem steps, typography variables,
   radius tokens, `--mud-zindex-*`. No literal radius, no px spacing, no literal font size.
6. **Reuse before you write.** Search `app.css` for an existing class before writing a
   declaration block. Truncation is `.truncate`; screen-reader-only text is
   `.visually-hidden`, one global class in `app.css`. Add a utility there the first time a
   second site needs it (UX-R21, UX-R22).
7. **No `!important`**, except where it is the only way past an inline style MudBlazor
   writes, with a comment saying so (UX-V16, UX-V22).
8. **Global MudBlazor overrides** (`.mud-button-root`, `.mud-drawer`, `.mud-avatar img`)
   live in one "Global MudBlazor overrides" section near the top of `app.css`, never in a
   feature's section (UX-V17).
9. **Every feature has a section header** in the form
   `/* ---- Room list: the hover/focus-revealed row actions menu ---- */`. The headers are
   the index. Comment the reason, not the property (UX-V21).
10. **Inline `style=` is for user data only**, such as a Teammate's avatar colour. Layout
    and sizes go in a class (UX-V28).

## Adding a Theme or changing a palette

1. Build the palette in C# and register a `ThemeDescriptor` in
   [`ThemeCatalog.BuiltIn`](../../src/Huddle.App/Themes/ThemeCatalog.cs). Never insert
   before `BuiltIn[0]`; three call sites treat it as the default.
2. Give the descriptor a `ThemeMode` and a `ThemeGroup`; the picker groups by `Group`.
3. For a converted VS Code palette, comment each value with the key it came from. No
   importer ships, so the comment is the only record
   ([ADR-0016](../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md)).
4. Run `ThemeCatalogTests`. Choose the contrast fix (a darker or lighter `Primary`) over
   an exception.
5. Update [manual-tests/appearance-theme.md](manual-tests/appearance-theme.md) if the
   picker changes.

## How the rules are enforced

| Rule | Enforced by |
| --- | --- |
| No colour or font-family literal in a stylesheet | `AppCss_DeclaresNoColourLiteral`, `AppCss_DeclaresNoFontFamilyLiteral` |
| Only MudBlazor variables and `--font-mono` in `var()` | `AppCss_UsesOnlyMudBlazorVariables`, `ScopedCss_UsesOnlyMudBlazorVariables` |
| Palette contrast | `ThemeCatalogTests` |
| No colour literal in an inline `style` attribute | `RazorComponents_NoInlineColourLiteralInStyleAttribute` |
| Everything else on this page | Review, and the checklist below |

The rules most often broken are the ones only review enforces: `Size.Small`,
`Variant.Outlined`, a `MudAlert` role and a snackbar `key`. Each could become a source
test in the style of the `AppCss_*` tests. Until one exists, review checks them.

## Checklist for a new or changed screen

**Before you start**

- [ ] Read the rows of [UX-Issues.md](UX-Issues.md) for the area you are touching. If your
      change fixes a row, delete it in the same change.
- [ ] Find the nearest existing screen in [wireframes.md](wireframes.md) and reuse its
      containers and codes.

**Foundations**

- [ ] Every colour is a palette role or a `Color.*`; none is a literal. Status colours
      come from a mapping function.
- [ ] Every line, hover, selected and drop state uses the treatments under Colour.
- [ ] Every text size comes from the type roles; the page has one `h1`.
- [ ] Every spacing, radius and `z-index` value comes from the scales above.
- [ ] Every icon comes from the entity or action table.

**Components**

- [ ] Every `MudButton`, `MudIconButton` and `MudChip` is `Size.Small`; every input is
      `Variant.Outlined` and `Margin.Dense`.
- [ ] Dialogs, drawers, confirms and action rows follow their anatomy and button order.
- [ ] Every avatar is a `TeammateAvatar`; every empty state says what to do next.
- [ ] No markup or CSS block was copied from another component; a shared one was used or
      built.

**Accessibility**

- [ ] Each icon-only control has an `aria-label` that names its target; each alert has a
      `role`.
- [ ] Keyboard focus is visible, nothing overrides the ring, and every control, including
      hover-revealed ones and drag handles, is reachable by keyboard and on touch.
- [ ] Any animation has a reduced-motion branch.

**Copy and verification**

- [ ] Labels are sentence case, verbs come from the verb table, counts are pluralised.
- [ ] It looks right in Huddle Light, Huddle Dark and one High Contrast Theme, run with
      `.\run.ps1 -Seed`.
- [ ] It fills its pane and scrolls inside it at a narrow width.
- [ ] [Huddle.UserGuide.md](../Huddle.UserGuide.md) is updated if a Human sees the change,
      and [wireframes.md](wireframes.md) if a container changed shape.

## Known gaps

These are open questions, not decisions. Raise them before relying on them.

- **No global MudBlazor defaults.** `Size.Small` and `Variant.Outlined` are set per call
  site. Whether 9.10.0 can set them once is unchecked; if it can't, a thin wrapper is the
  fallback (UX-V01, UX-V02).
- **No narrow-width guidance for the shell.** The drawer is persistent at 240px with no
  collapse, and only the Task list has a narrow-width rule.
- **Two spacing scales.** Markup uses MudBlazor's px utilities and the stylesheet uses
  rem at a 14px root, so `pa-4` and `1rem` differ by 2px.
- **Contrast tests cover palettes, not component states.** A tinted background such as the
  Human's bubble, or a status colour on a card, is not measured.

## See also

- [UXDesignGuidelines.md](UXDesignGuidelines.md): the hub for every UI resource, by task.
- [UX-Issues.md](UX-Issues.md): where today's code breaks these rules.
- [wireframes.md](wireframes.md): the code and name of every UI container.
- [rules.md](rules.md): the binding behaviour rules, including the stylesheet and Avatar rows.
- [MudBlazorDesign.md](../../agents/MudBlazorDesign.md): choosing a component for a spec.
- [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md): the house
  patterns, and facts checked against 9.10.0.
- [Huddle.Tabs-Specifications.md](../Huddle.Tabs-Specifications.md): the tab and pane model.
- [language.md](language.md): the words to use in interface copy.
