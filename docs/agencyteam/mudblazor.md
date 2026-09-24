# MudBlazor component index

Use this page before you build UI in `src/Huddle.App`. It tells you, in one
scan, whether MudBlazor already ships what you need, and links straight to the
official example that shows it. It does not re-document MudBlazor: every row is
a pointer, and the linked page is the reference.

Applies to **MudBlazor 9.10.0**, the version pinned in
[`Directory.Packages.props`](../../Directory.Packages.props). Built and link-checked
on 2026-09-24.

## Checking before you build

1. **Find the need** in [Finding a component by what you need](#finding-a-component-by-what-you-need).
   If it is not there, scan the [catalogue](#the-catalogue) for your category.
2. **Check whether Huddle already uses it** in
   [Components Huddle already uses](#components-huddle-already-uses). Copy the
   house pattern from that file before copying the MudBlazor example.
3. **Open the example link.** The "Examples on its page" column names every
   section of the official page, and each one deep-links to a live, runnable
   demo with its source.
4. **Read the [Huddle rules](#huddle-rules-that-bite-mudblazor-code)** below
   before you style or bind anything.
5. **Confirm every parameter you rely on against the package, not the website.**
   mudblazor.com documents the latest release. [Checking an API against
   9.10.0](#checking-an-api-against-9100) shows how, and lists the facts already
   checked.

Write your own component only when no row fits, or when composing two or three
MudBlazor components is the component. A new Razor component that wraps one
MudBlazor component to restyle it is almost always a Theme change or a
parameter you missed.

## Finding a component by what you need

| I need to… | Use |
| --- | --- |
| Lay things out in a row or column | [Stack](https://mudblazor.com/components/stack) — not hand-written flex CSS |
| Lay out a responsive multi-column page | [Grid](https://mudblazor.com/components/grid), [Container](https://mudblazor.com/components/container) |
| Show or hide something by screen size | [Hidden](https://mudblazor.com/components/hidden), [Breakpoint Provider](https://mudblazor.com/components/breakpointprovider) |
| Put content on a raised or outlined surface | [Paper](https://mudblazor.com/components/paper), [Card](https://mudblazor.com/components/card) |
| Show a list of records with sort, filter or paging | [Data Grid](https://mudblazor.com/components/datagrid). In 9.10 it is read-only, and sorts on several columns, by default. It groups on several levels through each column's `GroupByOrder` |
| Show a list the user can group, sort and choose columns for | [Data Grid](https://mudblazor.com/components/datagrid#grouping), with `Hidden` bound per column. Turn off `ShowColumnOptions` and `Filterable` when a saved view, not the grid, owns those choices |
| Show a small fixed table I write by hand | [Simple Table](https://mudblazor.com/components/simpletable) |
| Show a hierarchy (folders, org chart) | [Tree View](https://mudblazor.com/components/treeview) |
| Let the user reorder items or drag between columns | [Drop Zone](https://mudblazor.com/components/dropzone#miscellaneous) (has a kanban example) |
| Collapse sections of a page | [Expansion Panels](https://mudblazor.com/components/expansionpanels), [Collapse](https://mudblazor.com/components/collapse) |
| Switch between views in one place | [Tabs](https://mudblazor.com/components/tabs), [Toggle Group](https://mudblazor.com/components/togglegroup) |
| Walk the user through steps | [Stepper](https://mudblazor.com/components/stepper) |
| Confirm a destructive action | [Message Box](https://mudblazor.com/components/messagebox) via `IDialogService.ShowMessageBoxAsync` |
| Open a form or detail view in a modal | [Dialog](https://mudblazor.com/components/dialog#passing-data) |
| Tell the user something happened, briefly | [Snackbar](https://mudblazor.com/components/snackbar) (`ISnackbar`). Pass a `key` to [collapse repeats](https://mudblazor.com/components/snackbar#preventing-duplication) |
| Show a persistent warning or error on the page | [Alert](https://mudblazor.com/components/alert) |
| Show that something is loading | [Progress](https://mudblazor.com/components/progress), [Skeleton](https://mudblazor.com/components/skeleton), [Overlay](https://mudblazor.com/components/overlay#childcontent-as-a-loader) |
| Show a count or a dot on an icon | [Badge](https://mudblazor.com/components/badge) |
| Show someone's status on their avatar | [Badge](https://mudblazor.com/components/badge) with `Dot="true" Overlap="true"` around `TeammateAvatar`, plus a [Tooltip](https://mudblazor.com/components/tooltip) that says the status in words |
| Show that work is in progress inline, in a chip or button | [Progress](https://mudblazor.com/components/progress#circular-progress) — `MudProgressCircular Indeterminate="true" Size="Size.Small"`, not a CSS animation |
| Show a person or agent | [Avatar](https://mudblazor.com/components/avatar) |
| Show tags, labels or removable filters | [Chips](https://mudblazor.com/components/chips), [Chip Set](https://mudblazor.com/components/chipset) |
| Pick one of many from a long list, by typing | [Autocomplete](https://mudblazor.com/components/autocomplete) (one value only in 9.10) |
| Pick **several** from a long list, by typing | [Chip Set](https://mudblazor.com/components/chipset#adding-and-removing-chips) of closable chips, plus an [Autocomplete](https://mudblazor.com/components/autocomplete) that adds one and clears itself. 9.10's `MudAutocomplete` has no `MultiSelection` |
| Pick one or several from a known list | [Select](https://mudblazor.com/components/select#multiselect) |
| Pick one of a few, all visible | [Radio](https://mudblazor.com/components/radio), [Toggle Group](https://mudblazor.com/components/togglegroup) |
| Validate a form | [Form](https://mudblazor.com/components/form) |
| Wrap custom content so it looks like an input | [Field](https://mudblazor.com/components/field) |
| Accept a file, including drag-and-drop | [File Upload](https://mudblazor.com/components/fileupload) |
| Open a menu from a button or a right-click | [Menu](https://mudblazor.com/components/menu#advanced-usage) |
| Float arbitrary content next to an element | [Popover](https://mudblazor.com/components/popover) |
| Explain an icon on hover | [Tooltip](https://mudblazor.com/components/tooltip) |
| Add a keyboard shortcut | [Hotkey](https://mudblazor.com/components/hotkey) |
| Warn before leaving with unsaved changes | [Exit Prompt](https://mudblazor.com/components/exitprompt) |
| Highlight search matches in text | [Highlighter](https://mudblazor.com/components/highlighter) |
| Show events in order, such as a history or an audit log | [Timeline](https://mudblazor.com/components/timeline) |
| Put a row of actions above a list | [Tool Bar](https://mudblazor.com/components/toolbar) with a [Spacer](https://mudblazor.com/components/spacer) — not hand-written flex CSS |
| Colour a chip, badge, icon or alert by meaning | The component's `Color` (or `Severity` for alerts) parameter — not a CSS class. Keep the meaning-to-`Color` mapping in one static helper |
| Show two resizable panes | [Split Panel](https://mudblazor.com/components/splitpanel) |
| Chart numbers | [Charts](#charts) |
| Find an icon name | [Icon Reference](https://mudblazor.com/features/icons) (`Icons.Material.Filled.*`) |

## Components Huddle already uses

These have a house pattern. Read the named file first; it shows how Huddle binds,
styles and tests the component, which the MudBlazor example does not.

| Component | Read this first |
| --- | --- |
| Layout shell: `MudLayout`, `MudDrawer`, `MudMainContent`, providers | [`MainLayout.razor`](../../src/Huddle.App/Components/Layout/MainLayout.razor) |
| `MudNavMenu`, `MudNavLink`, `MudMenu` | [`RoomList.razor`](../../src/Huddle.App/Components/Shared/RoomList.razor) |
| `MudTextField`, `MudButton`, `MudIconButton`, `MudAlert`, `MudStack` | [`Chat.razor`](../../src/Huddle.App/Components/Pages/Chat.razor) |
| `MudTabs`, `MudTabPanel` | [`Settings.razor`](../../src/Huddle.App/Components/Pages/Settings.razor) |
| `MudSelect`, `MudRadioGroup`, `MudColorPicker`, `MudFileUpload` | [`TeammateCard.razor`](../../src/Huddle.App/Components/Shared/TeammateCard.razor) |
| `MudList`, `MudPaper` | [`Appearance.razor`](../../src/Huddle.App/Components/Settings/Appearance.razor) |
| `MudDialog` opened through `IDialogService` | [`ArchivedChatsDialog.razor`](../../src/Huddle.App/Components/Shared/ArchivedChatsDialog.razor), opened from [`ArchivedChats.razor`](../../src/Huddle.App/Components/Shared/ArchivedChats.razor) |
| Confirm through `ShowMessageBoxAsync`, report through `ISnackbar` | [`SkillsPanel.razor`](../../src/Huddle.App/Components/Settings/SkillsPanel.razor) |
| `MudExpansionPanels`, `MudTable` | [`ProposalCard.razor`](../../src/Huddle.App/Components/Shared/ProposalCard.razor) |
| `MudSimpleTable`, `MudChip` | [`SkillsPanel.razor`](../../src/Huddle.App/Components/Settings/SkillsPanel.razor) |
| `MudCollapse`, `MudCheckBox` | [`NewChat.razor`](../../src/Huddle.App/Components/Shared/NewChat.razor) |
| `MudAvatar` | [`TeammateAvatar.razor`](../../src/Huddle.App/Components/Shared/TeammateAvatar.razor) |

To refresh this list, search `src/Huddle.App` for `<Mud`.

## Huddle rules that bite MudBlazor code

[Rules](rules.md) is binding for `src/Huddle.App`. These rows are the ones a
MudBlazor example will lead you to break:

- **No colour or font literals.** Stylesheets read `var(--mud-palette-*)` and
  `var(--mud-typography-*)` only, and inline `Style` carries no colour literal.
  Tests fail the build otherwise. Pass `Color="Color.Primary"` rather than a hex.
  A Theme is a `MudTheme` — see [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md).
- **`Disabled`, not `ReadOnly`, on an input that must look inert.** MudBlazor
  9.10.0 styles only the disabled state.
- **A `string` parameter needs a leading `@`.** `Label="this.name"` passes the
  literal text; the compiler cannot catch it.
- **No `MaxLength` on a `MudTextField` whose limit is counted in characters
  a human sees** (the avatar label is the case in the rules). It truncates by
  UTF-16 unit and can split a surrogate pair.

[Traps](traps.md) and the house components record the rest. These are the ones
a MudBlazor example won't warn you about:

- **`MudAlert` has no `role`.** It emits no role and no aria. Add `role="alert"`
  for an interruption and `role="status"` for a consequence, yourself
  ([traps.md](traps.md), the `MudAlert` entry).
- **An open dialog's parameters are frozen.** MudBlazor ignores every
  `SetParametersAsync` after `IDialogService.ShowAsync` opens it. Pass an id or
  a snapshot, and let the dialog load and subscribe for itself. The reasoning is
  in the header comment of [`TeammateCard.razor`](../../src/Huddle.App/Components/Shared/TeammateCard.razor).
- **Scoped CSS can't reach inside a MudBlazor component.** `::deep` only
  matches under an HTML element written in your own markup, and a `MudDrawer` or
  `MudLayout` is a component, not one. Such rules go in `app.css`; see the
  comment above `.mud-drawer-content` there.
- **bUnit needs the real popover and dialog hosts.** `MudSelect` items appear
  only after a real click, and a `MudDialog` renders nothing on its own. Render
  through `MudBunitContext.RenderWithPopovers` and drive dialogs through the
  real `IDialogService` ([testing.md](testing.md)).

## Checking an API against 9.10.0

mudblazor.com shows the latest release, so a parameter, overload or default can
differ from the pinned package. Two local checks settle it, with no build.

**Does the member exist, and what does its doc comment say?** Search the XML
docs that ship in the package:

```powershell
$xml = "$env:USERPROFILE\.nuget\packages\mudblazor\9.10.0\lib\net10.0\MudBlazor.xml"
Select-String -Path $xml -Pattern 'name="P:MudBlazor\.MudDataGrid`1\.(\w+)"' |
    ForEach-Object { $_.Matches[0].Groups[1].Value } | Sort-Object -Unique
```

Generic types carry a backtick and their arity: `MudDataGrid` is ``MudDataGrid`1``
and `Column` is ``Column`1``. The prefix `P:` is a property, `M:` a method and `T:`
a type.

**What is its exact type?** The XML docs don't carry types. Load the assembly by
reflection, and resolve its dependencies from the ASP.NET Core shared framework:

```powershell
$fw  = Get-ChildItem "C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App" -Directory |
       Sort-Object Name -Descending | Select-Object -First 1
$dll = "$env:USERPROFILE\.nuget\packages\mudblazor\9.10.0\lib\net10.0\MudBlazor.dll"
$dirs = @($fw.FullName, (Split-Path $dll))
[AppDomain]::CurrentDomain.add_AssemblyResolve({ param($s, $e)
    $n = (New-Object Reflection.AssemblyName($e.Name)).Name
    foreach ($d in $dirs) { $f = Join-Path $d "$n.dll"; if (Test-Path $f) { return [Reflection.Assembly]::LoadFrom($f) } }
    $null })
$asm = [Reflection.Assembly]::LoadFrom($dll)
$asm.GetType('MudBlazor.MudDropContainer`1').GetMembers() |
    Where-Object Name -Match 'Transaction|ItemsSelector|CanDrop' |
    ForEach-Object { "$($_.MemberType) $($_.Name)" }
```

A member listed as an `Event` is a C# event, not a `[Parameter]`. You can't bind it
in markup; subscribe to it in code (see the table below).

### Facts already checked

Each of these was confirmed against the 9.10.0 package on 2026-09-24, while
specifying the Tasks feature. They are the ones most likely to differ from the
website, or from what a MudBlazor example suggests.

| Component | Fact |
| --- | --- |
| `MudDataGrid<T>` | `ReadOnly` defaults to `true`. `SortMode` defaults to `SortMode.Multiple`. Grouping can have several levels: each `Column` has `Grouping`, `GroupBy`, `GroupByOrder` and `GroupExpanded`. Columns have `Hidden`. `ShowColumnOptions`, `RowClick` and `RowClassFunc` exist |
| `MudTable<T>` | Multi-level grouping goes through `TableGroupDefinition<T>.InnerGroup` |
| `MudDropContainer<T>` | `ItemsSelector` and `CanDrop` are `Func<T, string, bool>`, where the string is the zone `Identifier`. `ItemDropped` is `EventCallback<MudItemDropInfo<T>>`, and the info has `Item`, `DropzoneIdentifier` and `IndexInZone` |
| `MudDropContainer<T>` | **`TransactionStarted` and `TransactionEnded` are C# events**, not parameters. Their types are `EventHandler<MudDragAndDropItemTransaction<T>>` and `EventHandler<MudDragAndDropTransactionFinishedEventArgs<T>>`. Subscribe after the first render through `@ref`, and unsubscribe in `Dispose`. `Refresh()`, `StartTransaction(…)` and `CancelTransaction()` are public methods |
| `MudDropZone<T>` | Its own `ItemsSelector` and `CanDrop` are `Func<T, bool>`, **with no zone argument**. `OnlyZone="true"` makes a zone that only receives drops. `AllowReorder` enables reordering within the zone |
| `MudAutocomplete<T>` | Selects one value only: there is no `MultiSelection`. `Strict`, `CoerceValue`, `CoerceText`, `SearchFunc` and `ToStringFunc` exist |
| `MudDatePicker` | `Date` is `DateTime?`. Convert to and from `DateOnly` at the binding |
| `MudToggleGroup<T>` | `Value`/`ValueChanged` for a single choice and `Values`/`ValuesChanged` for several, with `SelectionMode` and `Vertical` |
| `MudChipSet<T>` | `SelectedValue`, `SelectedValues`, `CloseIcon`, and `OnClose` (an `EventCallback<MudChip<T>>`) |
| `MudTimelineItem` | Content goes in `ItemContent` and the opposite side in `ItemOpposite` (both `RenderFragment`s). It also has `Color` and `Size` |
| `MudExitPrompt` | Parameters are `Title`, `Text`, `Disabled` and `UseNativePrompt`. With `UseNativePrompt` false (the default), in-app navigation asks through a MudBlazor message box. Closing the tab, reloading or typing a URL always uses the browser's own prompt |
| `ISnackbar.Add` | Every overload (`string`, `MarkupString`, `RenderFragment`, component parameters) takes `(message, Severity, Action<SnackbarOptions>? configure, string? key)`. The same `key` collapses duplicates |

When you check a new fact, add a row. When `Directory.Packages.props` moves
MudBlazor to a new version, re-check every row.

## The catalogue

Every component with a page in the MudBlazor 9.10.0 docs, grouped by what it is
for. Each component name links to its page; each entry in the last column links
to one example section on that page. For the full parameter list, open the
**API** tab on the component page, or go to `https://mudblazor.com/api/<TypeName>`,
for example [`/api/MudButton`](https://mudblazor.com/api/MudButton).

### Layout and structure

| Component | Types | What it is for | Examples on its page |
| --- | --- | --- | --- |
| [Container](https://mudblazor.com/components/container) | `MudContainer` | Centres content and caps its width at a breakpoint. | [Fluid](https://mudblazor.com/components/container#fluid), [Fixed](https://mudblazor.com/components/container#fixed) |
| [Grid](https://mudblazor.com/components/grid) | `MudGrid`, `MudItem` | 12-column responsive grid; column widths per breakpoint. | [Spacing](https://mudblazor.com/components/grid#spacing), [Basic Grid](https://mudblazor.com/components/grid#basic-grid), [Grid With Breakpoints](https://mudblazor.com/components/grid#grid-with-breakpoints), [Line Break](https://mudblazor.com/components/grid#line-break), [Grid Builder](https://mudblazor.com/components/grid#grid-builder) |
| [Stack](https://mudblazor.com/components/stack) | `MudStack` | One-dimensional flex row or column with spacing, justify and align. Reach for it before writing flex CSS. | [Basic Usage](https://mudblazor.com/components/stack#basic-usage), [Direction](https://mudblazor.com/components/stack#direction), [Breakpoint](https://mudblazor.com/components/stack#breakpoint), [Spacing](https://mudblazor.com/components/stack#spacing), [Wrapping](https://mudblazor.com/components/stack#wrapping), [Line Break](https://mudblazor.com/components/stack#line-break), [HTML Tag](https://mudblazor.com/components/stack#html-tag), [Usage Examples](https://mudblazor.com/components/stack#usage-examples), [Item Placement](https://mudblazor.com/components/stack#item-placement), [Stretch Items](https://mudblazor.com/components/stack#stretch-items), [Interactive](https://mudblazor.com/components/stack#interactive) |
| [Spacer](https://mudblazor.com/components/spacer) | `MudSpacer` | Flexible gap that pushes siblings apart in a toolbar, app bar or stack. | [Basic Usage](https://mudblazor.com/components/spacer#basic-usage) |
| [Paper](https://mudblazor.com/components/paper) | `MudPaper` | A surface with elevation or outline: the base of cards, panels and sheets. | [Material Design](https://mudblazor.com/components/paper#material-design), [Component](https://mudblazor.com/components/paper#component), [Variants](https://mudblazor.com/components/paper#variants) |
| [Card](https://mudblazor.com/components/card) | `MudCard`, `MudCardHeader`, `MudCardContent`, `MudCardActions`, `MudCardMedia` | Structured surface with header, media, body and action row. | [Simple Card](https://mudblazor.com/components/card#simple-card), [Outlined](https://mudblazor.com/components/card#outlined), [Header](https://mudblazor.com/components/card#header), [Media](https://mudblazor.com/components/card#media), [Combined](https://mudblazor.com/components/card#combined) |
| [Divider](https://mudblazor.com/components/divider) | `MudDivider` | Horizontal or vertical rule, including inset and middle variants. | [List Dividers](https://mudblazor.com/components/divider#list-dividers), [Inset Dividers](https://mudblazor.com/components/divider#inset-dividers), [Middle Dividers](https://mudblazor.com/components/divider#middle-dividers), [Vertical Dividers](https://mudblazor.com/components/divider#vertical-dividers) |
| [Split Panel](https://mudblazor.com/components/splitpanel) | `MudSplitPanel` | Two panes with a draggable splitter. | [Basic Usage](https://mudblazor.com/components/splitpanel#basic-usage), [Overlay](https://mudblazor.com/components/splitpanel#overlay), [Split panel inside split panel](https://mudblazor.com/components/splitpanel#split-panel-inside-split-panel), [Functions](https://mudblazor.com/components/splitpanel#functions) |
| [Hidden](https://mudblazor.com/components/hidden) | `MudHidden` | Renders content only above or below a breakpoint. | [How it works](https://mudblazor.com/components/hidden#how-it-works), [Listening to browser window resize events](https://mudblazor.com/components/hidden#listening-to-browser-window-resize-events) |
| [Breakpoint Provider](https://mudblazor.com/components/breakpointprovider) | `MudBreakpointProvider` | Cascades the current breakpoint and raises an event when the window crosses one. | [How it works](https://mudblazor.com/components/breakpointprovider#how-it-works), [Listening to browser window breakpoint changes](https://mudblazor.com/components/breakpointprovider#listening-to-browser-window-breakpoint-changes) |
| [Tool Bar](https://mudblazor.com/components/toolbar) | `MudToolBar` | Horizontal strip of actions, usually in a card or app bar. | [ToolBar Example](https://mudblazor.com/components/toolbar#toolbar-example), [Wrap Content](https://mudblazor.com/components/toolbar#wrap-content) |
| [Element](https://mudblazor.com/components/element) | `MudElement` | Renders an HTML tag chosen at run time; the escape hatch for dynamic markup. | [Basic example](https://mudblazor.com/components/element#basic-example), [Interactive example](https://mudblazor.com/components/element#interactive-example), [Obtaining an ElementReference](https://mudblazor.com/components/element#obtaining-an-elementreference) |

### App shell and navigation

| Component | Types | What it is for | Examples on its page |
| --- | --- | --- | --- |
| [App Bar](https://mudblazor.com/components/appbar) | `MudAppBar` | Top or bottom application bar. | [Basic App Bar](https://mudblazor.com/components/appbar#basic-app-bar), [Usage](https://mudblazor.com/components/appbar#usage), [Bottom App Bar](https://mudblazor.com/components/appbar#bottom-app-bar), [App Bar with Menu](https://mudblazor.com/components/appbar#app-bar-with-menu), [Contextual App Bar](https://mudblazor.com/components/appbar#contextual-app-bar) |
| [Drawer](https://mudblazor.com/components/drawer) | `MudDrawer`, `MudDrawerHeader`, `MudDrawerContainer` | Side panel: temporary, persistent, responsive or mini, left or right. | [Usage](https://mudblazor.com/components/drawer#usage), [Variants](https://mudblazor.com/components/drawer#variants), [Hide overlay](https://mudblazor.com/components/drawer#hide-overlay), [Anchor Drawer](https://mudblazor.com/components/drawer#anchor-drawer), [Left/Right Drawer](https://mudblazor.com/components/drawer#left/right-drawer), [Clipping](https://mudblazor.com/components/drawer#clipping), [Custom Breakpoint](https://mudblazor.com/components/drawer#custom-breakpoint), [Mini Drawer Customization](https://mudblazor.com/components/drawer#mini-drawer-customization), [Sizing Drawers](https://mudblazor.com/components/drawer#sizing-drawers) |
| [Navigation Menu](https://mudblazor.com/components/navmenu) | `MudNavMenu`, `MudNavLink`, `MudNavGroup` | Vertical navigation with collapsible groups and active-route highlighting. | [Basic Usage](https://mudblazor.com/components/navmenu#basic-usage), [Two Way Bind](https://mudblazor.com/components/navmenu#two-way-bind), [Sub Groups](https://mudblazor.com/components/navmenu#sub-groups), [Icons](https://mudblazor.com/components/navmenu#icons), [Bordered](https://mudblazor.com/components/navmenu#bordered), [Color](https://mudblazor.com/components/navmenu#color), [Margin](https://mudblazor.com/components/navmenu#margin), [Rounded](https://mudblazor.com/components/navmenu#rounded), [Dense](https://mudblazor.com/components/navmenu#dense), [Customizing the Group Title](https://mudblazor.com/components/navmenu#customizing-the-group-title), [OnClick](https://mudblazor.com/components/navmenu#onclick), [MultiExpansion](https://mudblazor.com/components/navmenu#multiexpansion) |
| [Tabs](https://mudblazor.com/components/tabs) | `MudTabs`, `MudTabPanel`, `MudDynamicTabs` | Tabbed panels, including closable and addable tabs and keep-alive panels. | [Simple Tabs](https://mudblazor.com/components/tabs#simple-tabs), [Icon Tabs](https://mudblazor.com/components/tabs#icon-tabs), [Tabs Position](https://mudblazor.com/components/tabs#tabs-position), [Tab Sorting](https://mudblazor.com/components/tabs#tab-sorting), [Tab Width](https://mudblazor.com/components/tabs#tab-width), [Tooltips](https://mudblazor.com/components/tabs#tooltips), [Badges](https://mudblazor.com/components/tabs#badges), [Drag and Drop Tabs](https://mudblazor.com/components/tabs#drag-and-drop-tabs), [Scrolling Tabs](https://mudblazor.com/components/tabs#scrolling-tabs), [Render Fragments](https://mudblazor.com/components/tabs#render-fragments), [Visibility](https://mudblazor.com/components/tabs#visibility), [Set Active Panel](https://mudblazor.com/components/tabs#set-active-panel), [Dynamic Tabs](https://mudblazor.com/components/tabs#dynamic-tabs), [Keep Panels Alive](https://mudblazor.com/components/tabs#keep-panels-alive) |
| [Breadcrumbs](https://mudblazor.com/components/breadcrumbs) | `MudBreadcrumbs` | Path trail with custom separators and collapsing. | [Basic Breadcrumbs](https://mudblazor.com/components/breadcrumbs#basic-breadcrumbs), [Custom Separator](https://mudblazor.com/components/breadcrumbs#custom-separator), [Render Fragments](https://mudblazor.com/components/breadcrumbs#render-fragments), [Item Icons](https://mudblazor.com/components/breadcrumbs#item-icons), [Item Template](https://mudblazor.com/components/breadcrumbs#item-template), [Collapsed](https://mudblazor.com/components/breadcrumbs#collapsed) |
| [Link](https://mudblazor.com/components/link) | `MudLink` | Themed anchor with underline styles and click handling. | [Simple Links](https://mudblazor.com/components/link#simple-links), [Inline Links](https://mudblazor.com/components/link#inline-links), [Underlines](https://mudblazor.com/components/link#underlines), [Icons](https://mudblazor.com/components/link#icons), [OnClick](https://mudblazor.com/components/link#onclick) |
| [Menu](https://mudblazor.com/components/menu) | `MudMenu`, `MudMenuItem` | Dropdown or context menu off any activator, with nesting. | [Basic Usage](https://mudblazor.com/components/menu#basic-usage), [Dense Menu](https://mudblazor.com/components/menu#dense-menu), [Activator](https://mudblazor.com/components/menu#activator), [Menu Content](https://mudblazor.com/components/menu#menu-content), [Nested Menu (Desktop)](https://mudblazor.com/components/menu#nested-menu-%28desktop%29), [Advanced Usage](https://mudblazor.com/components/menu#advanced-usage) |
| [Pagination](https://mudblazor.com/components/pagination) | `MudPagination` | Page selector, standalone or under a table. | [Usage](https://mudblazor.com/components/pagination#usage), [Variants](https://mudblazor.com/components/pagination#variants), [Disabled](https://mudblazor.com/components/pagination#disabled), [Rectangular](https://mudblazor.com/components/pagination#rectangular), [Sizes](https://mudblazor.com/components/pagination#sizes), [Control buttons](https://mudblazor.com/components/pagination#control-buttons), [Hide pages](https://mudblazor.com/components/pagination#hide-pages), [Item count](https://mudblazor.com/components/pagination#item-count), [Table pagination](https://mudblazor.com/components/pagination#table-pagination) |
| [Stepper](https://mudblazor.com/components/stepper) | `MudStepper`, `MudStep` | Multi-step wizard, linear or not, horizontal or vertical. | [Horizontal stepper](https://mudblazor.com/components/stepper#horizontal-stepper), [Non-linear version](https://mudblazor.com/components/stepper#non-linear-version), [Centered labels](https://mudblazor.com/components/stepper#centered-labels), [Customization](https://mudblazor.com/components/stepper#customization), [Step binding](https://mudblazor.com/components/stepper#step-binding), [Controlling navigation](https://mudblazor.com/components/stepper#controlling-navigation), [Dynamically adding or removing steps](https://mudblazor.com/components/stepper#dynamically-adding-or-removing-steps), [Vertical stepper](https://mudblazor.com/components/stepper#vertical-stepper) |
| [Scroll To Top](https://mudblazor.com/components/scrolltotop) | `MudScrollToTop` | Floating button that appears after scrolling and returns to the top. | [Default value](https://mudblazor.com/components/scrolltotop#default-value), [Setup](https://mudblazor.com/components/scrolltotop#setup), [Custom content](https://mudblazor.com/components/scrolltotop#custom-content) |

### Form and inputs

| Component | Types | What it is for | Examples on its page |
| --- | --- | --- | --- |
| [Form (validation)](https://mudblazor.com/components/form) | `MudForm` | Groups inputs, validates them, tracks dirty and valid. Also works under `EditForm` and FluentValidation. | [Simple Form Validation](https://mudblazor.com/components/form#simple-form-validation), [EditForm Support](https://mudblazor.com/components/form#editform-support), [Using Simple Fluent Validation](https://mudblazor.com/components/form#using-simple-fluent-validation), [Using Fluent Validation](https://mudblazor.com/components/form#using-fluent-validation), [Validate only on user interaction](https://mudblazor.com/components/form#validate-only-on-user-interaction), [Automatically set Labels](https://mudblazor.com/components/form#automatically-set-labels), [ReadOnly and Disabled Forms](https://mudblazor.com/components/form#readonly-and-disabled-forms), [Customization](https://mudblazor.com/components/form#customization) |
| [Text Field](https://mudblazor.com/components/textfield) | `MudTextField<T>` | Single or multi-line text input with adornments, masks, counter and clear button. | [Basic Usage](https://mudblazor.com/components/textfield#basic-usage), [Common Properties](https://mudblazor.com/components/textfield#common-properties), [Appearance & Styling](https://mudblazor.com/components/textfield#appearance-&-styling), [Adornments](https://mudblazor.com/components/textfield#adornments), [Character Counter](https://mudblazor.com/components/textfield#character-counter), [Data Binding](https://mudblazor.com/components/textfield#data-binding), [Input Types](https://mudblazor.com/components/textfield#input-types), [Multiline](https://mudblazor.com/components/textfield#multiline), [Input Masking](https://mudblazor.com/components/textfield#input-masking), [Programmatic Control](https://mudblazor.com/components/textfield#programmatic-control), [Building Blocks](https://mudblazor.com/components/textfield#building-blocks) |
| [Numeric Field](https://mudblazor.com/components/numericfield) | `MudNumericField<T>` | Number input with spin buttons, min/max and culture-aware formatting. | [Basic Usage](https://mudblazor.com/components/numericfield#basic-usage), [Common Properties](https://mudblazor.com/components/numericfield#common-properties), [Data Binding](https://mudblazor.com/components/numericfield#data-binding), [Localization & Formatting](https://mudblazor.com/components/numericfield#localization-&-formatting) |
| [Select](https://mudblazor.com/components/select) | `MudSelect<T>`, `MudSelectItem<T>` | Dropdown picker, single or multi-select, with custom item rendering. | [Visual Playground](https://mudblazor.com/components/select#visual-playground), [Using Select](https://mudblazor.com/components/select#using-select), [Multiselect](https://mudblazor.com/components/select#multiselect), [Dynamic Sizing](https://mudblazor.com/components/select#dynamic-sizing), [Value presentation](https://mudblazor.com/components/select#value-presentation), [Custom converter](https://mudblazor.com/components/select#custom-converter), [Numeric collection](https://mudblazor.com/components/select#numeric-collection), [Placement](https://mudblazor.com/components/select#placement), [Keyboard Navigation](https://mudblazor.com/components/select#keyboard-navigation) |
| [Autocomplete](https://mudblazor.com/components/autocomplete) | `MudAutocomplete<T>` | Type-to-search picker backed by an async search function. | [Visual Playground](https://mudblazor.com/components/autocomplete#visual-playground), [Usage](https://mudblazor.com/components/autocomplete#usage), [Presentation](https://mudblazor.com/components/autocomplete#presentation), [Presentation Extras](https://mudblazor.com/components/autocomplete#presentation-extras), [Validation](https://mudblazor.com/components/autocomplete#validation), [Keyboard Navigation](https://mudblazor.com/components/autocomplete#keyboard-navigation), [Progress](https://mudblazor.com/components/autocomplete#progress), [Strict Mode](https://mudblazor.com/components/autocomplete#strict-mode), [Cancellation Token](https://mudblazor.com/components/autocomplete#cancellation-token) |
| [Check Box](https://mudblazor.com/components/checkbox) | `MudCheckBox<T>` | Checkbox, including tri-state (indeterminate). | [Basic Checkboxes](https://mudblazor.com/components/checkbox#basic-checkboxes), [Color](https://mudblazor.com/components/checkbox#color), [Labels](https://mudblazor.com/components/checkbox#labels), [Icons](https://mudblazor.com/components/checkbox#icons), [Dense](https://mudblazor.com/components/checkbox#dense), [Sizes](https://mudblazor.com/components/checkbox#sizes), [Aria](https://mudblazor.com/components/checkbox#aria), [Indeterminate State](https://mudblazor.com/components/checkbox#indeterminate-state), [Binding Checkbox Against Different Data Types](https://mudblazor.com/components/checkbox#binding-checkbox-against-different-data-types), [Content Placement](https://mudblazor.com/components/checkbox#content-placement), [ReadOnly Mode](https://mudblazor.com/components/checkbox#readonly-mode), [Keyboard Navigation](https://mudblazor.com/components/checkbox#keyboard-navigation) |
| [Radio](https://mudblazor.com/components/radio) | `MudRadio<T>`, `MudRadioGroup<T>` | One choice from a small visible set. | [RadioGroup](https://mudblazor.com/components/radio#radiogroup), [Color](https://mudblazor.com/components/radio#color), [Dense](https://mudblazor.com/components/radio#dense), [Sizes](https://mudblazor.com/components/radio#sizes), [Content Placement](https://mudblazor.com/components/radio#content-placement), [ReadOnly and Disabled mode](https://mudblazor.com/components/radio#readonly-and-disabled-mode), [Accessibility](https://mudblazor.com/components/radio#accessibility) |
| [Switch](https://mudblazor.com/components/switch) | `MudSwitch<T>` | On/off toggle. | [Basic switches](https://mudblazor.com/components/switch#basic-switches), [Color](https://mudblazor.com/components/switch#color), [Switch with label](https://mudblazor.com/components/switch#switch-with-label), [Thumb Icon](https://mudblazor.com/components/switch#thumb-icon), [Different data types](https://mudblazor.com/components/switch#different-data-types), [Content Placement](https://mudblazor.com/components/switch#content-placement), [ReadOnly mode](https://mudblazor.com/components/switch#readonly-mode), [Keyboard Navigation](https://mudblazor.com/components/switch#keyboard-navigation), [Size](https://mudblazor.com/components/switch#size) |
| [Slider](https://mudblazor.com/components/slider) | `MudSlider<T>` | Pick a value on a range, with ticks and labels. | [Basic Sliders](https://mudblazor.com/components/slider#basic-sliders), [Filled](https://mudblazor.com/components/slider#filled), [Step Sliders](https://mudblazor.com/components/slider#step-sliders), [Min and Max Values](https://mudblazor.com/components/slider#min-and-max-values), [Nullable](https://mudblazor.com/components/slider#nullable), [Ticks](https://mudblazor.com/components/slider#ticks), [Value Label](https://mudblazor.com/components/slider#value-label), [Size](https://mudblazor.com/components/slider#size), [Vertical](https://mudblazor.com/components/slider#vertical) |
| [Toggle Group](https://mudblazor.com/components/togglegroup) | `MudToggleGroup<T>`, `MudToggleItem<T>` | Segmented control: single, multi or toggle selection from a row of buttons. | [Usage](https://mudblazor.com/components/togglegroup#usage), [Selection Modes](https://mudblazor.com/components/togglegroup#selection-modes), [Custom Selection Style](https://mudblazor.com/components/togglegroup#custom-selection-style), [Custom Content](https://mudblazor.com/components/togglegroup#custom-content) |
| [File Upload](https://mudblazor.com/components/fileupload) | `MudFileUpload<T>` | File picker with drag-and-drop and form validation. | [Basic Usage (Single Button or Drag-and-Drop)](https://mudblazor.com/components/fileupload#basic-usage-%28single-button-or-drag-and-drop%29), [Custom Content](https://mudblazor.com/components/fileupload#custom-content), [SelectedTemplate](https://mudblazor.com/components/fileupload#selectedtemplate), [Multiple and Accept](https://mudblazor.com/components/fileupload#multiple-and-accept), [Form Validation](https://mudblazor.com/components/fileupload#form-validation), [Event Options](https://mudblazor.com/components/fileupload#event-options), [Append Multiple Files](https://mudblazor.com/components/fileupload#append-multiple-files), [Drag-and-Drop Examples](https://mudblazor.com/components/fileupload#drag-and-drop-examples) |
| [Field](https://mudblazor.com/components/field) | `MudField` | The input chrome (label, outline, helper text) around content that is not an input. | [Basic Usage](https://mudblazor.com/components/field#basic-usage), [Layout & Styling](https://mudblazor.com/components/field#layout-&-styling), [Advanced Usage](https://mudblazor.com/components/field#advanced-usage) |
| [Rating](https://mudblazor.com/components/rating) | `MudRating` | Star-style rating input or display. | [Basic Rating](https://mudblazor.com/components/rating#basic-rating), [Disabled](https://mudblazor.com/components/rating#disabled), [Read only](https://mudblazor.com/components/rating#read-only), [Sizes](https://mudblazor.com/components/rating#sizes), [Max value](https://mudblazor.com/components/rating#max-value), [Icons and color](https://mudblazor.com/components/rating#icons-and-color), [Events and value binding](https://mudblazor.com/components/rating#events-and-value-binding), [Accessibility](https://mudblazor.com/components/rating#accessibility) |

### Pickers

| Component | Types | What it is for | Examples on its page |
| --- | --- | --- | --- |
| [Date Picker](https://mudblazor.com/components/datepicker) | `MudDatePicker` | Date input with calendar popover, dialog or static modes. | [Basic Usage](https://mudblazor.com/components/datepicker#basic-usage), [Input Masking](https://mudblazor.com/components/datepicker#input-masking), [Text Parsing](https://mudblazor.com/components/datepicker#text-parsing), [Read Only](https://mudblazor.com/components/datepicker#read-only), [Disable or customize days](https://mudblazor.com/components/datepicker#disable-or-customize-days), [Action Buttons](https://mudblazor.com/components/datepicker#action-buttons), [Internationalization](https://mudblazor.com/components/datepicker#internationalization), [Dialog Mode](https://mudblazor.com/components/datepicker#dialog-mode), [Static Mode](https://mudblazor.com/components/datepicker#static-mode), [Different views](https://mudblazor.com/components/datepicker#different-views), [Colors](https://mudblazor.com/components/datepicker#colors), [Elevation](https://mudblazor.com/components/datepicker#elevation), [Go To Date](https://mudblazor.com/components/datepicker#go-to-date), [Fixed Values Usage](https://mudblazor.com/components/datepicker#fixed-values-usage), [Range Picker Usage](https://mudblazor.com/components/datepicker#range-picker-usage), [Accessibility](https://mudblazor.com/components/datepicker#accessibility) |
| [Date Range Picker](https://mudblazor.com/components/daterangepicker) | `MudDateRangePicker` | Start and end date input. | [Basic Usage](https://mudblazor.com/components/daterangepicker#basic-usage), [Editable](https://mudblazor.com/components/daterangepicker#editable), [Custom Format](https://mudblazor.com/components/daterangepicker#custom-format), [Min/Max Date](https://mudblazor.com/components/daterangepicker#min/max-date), [Min/Max Days](https://mudblazor.com/components/daterangepicker#min/max-days), [Action Buttons](https://mudblazor.com/components/daterangepicker#action-buttons) |
| [Time Picker](https://mudblazor.com/components/timepicker) | `MudTimePicker` | Time input with clock face. | [Basic Usage](https://mudblazor.com/components/timepicker#basic-usage), [Read Only](https://mudblazor.com/components/timepicker#read-only), [Action Buttons](https://mudblazor.com/components/timepicker#action-buttons), [Dialog Mode](https://mudblazor.com/components/timepicker#dialog-mode), [Static Mode](https://mudblazor.com/components/timepicker#static-mode), [Open to Minutes](https://mudblazor.com/components/timepicker#open-to-minutes), [Edit Mode](https://mudblazor.com/components/timepicker#edit-mode), [Colors](https://mudblazor.com/components/timepicker#colors), [Elevation](https://mudblazor.com/components/timepicker#elevation), [MinuteSelectionStep](https://mudblazor.com/components/timepicker#minuteselectionstep), [Keyboard Navigation](https://mudblazor.com/components/timepicker#keyboard-navigation) |
| [Color Picker](https://mudblazor.com/components/colorpicker) | `MudColorPicker` | Colour input: spectrum, grid or palette, with alpha. | [Basic Usage](https://mudblazor.com/components/colorpicker#basic-usage), [Parts](https://mudblazor.com/components/colorpicker#parts), [Color Picker View](https://mudblazor.com/components/colorpicker#color-picker-view), [Color Picker Mode](https://mudblazor.com/components/colorpicker#color-picker-mode), [Custom Palette](https://mudblazor.com/components/colorpicker#custom-palette), [Alpha](https://mudblazor.com/components/colorpicker#alpha), [Switch Mode](https://mudblazor.com/components/colorpicker#switch-mode), [Dialog Mode](https://mudblazor.com/components/colorpicker#dialog-mode), [Inline Mode](https://mudblazor.com/components/colorpicker#inline-mode), [Static Mode](https://mudblazor.com/components/colorpicker#static-mode), [Elevation](https://mudblazor.com/components/colorpicker#elevation), [Example Usage](https://mudblazor.com/components/colorpicker#example-usage), [Drag Interaction](https://mudblazor.com/components/colorpicker#drag-interaction) |

### Buttons

| Component | Types | What it is for | Examples on its page |
| --- | --- | --- | --- |
| [Button](https://mudblazor.com/components/button) | `MudButton` | Filled, outlined or text button; icons, loading state, link buttons. | [Filled Buttons](https://mudblazor.com/components/button#filled-buttons), [Text Buttons](https://mudblazor.com/components/button#text-buttons), [Outlined Buttons](https://mudblazor.com/components/button#outlined-buttons), [Size](https://mudblazor.com/components/button#size), [Icons](https://mudblazor.com/components/button#icons), [Customized Buttons](https://mudblazor.com/components/button#customized-buttons), [Link Button](https://mudblazor.com/components/button#link-button) |
| [Icon Button](https://mudblazor.com/components/iconbutton) | `MudIconButton` | Icon-only button. | [Simple Icon Buttons](https://mudblazor.com/components/iconbutton#simple-icon-buttons), [Using Font Icons](https://mudblazor.com/components/iconbutton#using-font-icons), [Variant and Size](https://mudblazor.com/components/iconbutton#variant-and-size) |
| [Toggle Icon Button](https://mudblazor.com/components/toggleiconbutton) | `MudToggleIconButton` | Icon button with an on and an off icon, bound to a bool. | [Basic Usage](https://mudblazor.com/components/toggleiconbutton#basic-usage), [Without Binding](https://mudblazor.com/components/toggleiconbutton#without-binding) |
| [Button Group](https://mudblazor.com/components/buttongroup) | `MudButtonGroup` | Joined row or column of buttons, including split buttons. | [Basic Button Group](https://mudblazor.com/components/buttongroup#basic-button-group), [Vertical Orientation](https://mudblazor.com/components/buttongroup#vertical-orientation), [Sizes and Colors](https://mudblazor.com/components/buttongroup#sizes-and-colors), [Icon Buttons](https://mudblazor.com/components/buttongroup#icon-buttons), [Split Button](https://mudblazor.com/components/buttongroup#split-button), [Drop Shadow](https://mudblazor.com/components/buttongroup#drop-shadow), [Custom Styles](https://mudblazor.com/components/buttongroup#custom-styles) |
| [Floating Action Button](https://mudblazor.com/components/buttonfab) | `MudFab` | Round primary-action button. | [Simple FAB](https://mudblazor.com/components/buttonfab#simple-fab), [Filled FAB](https://mudblazor.com/components/buttonfab#filled-fab), [Text FAB](https://mudblazor.com/components/buttonfab#text-fab), [Outlined FAB](https://mudblazor.com/components/buttonfab#outlined-fab), [Size](https://mudblazor.com/components/buttonfab#size) |
| [FAB Menu](https://mudblazor.com/components/buttonfabmenu) | `MudFabMenu` | FAB that fans out into several actions. | [Basic usage](https://mudblazor.com/components/buttonfabmenu#basic-usage), [Styling](https://mudblazor.com/components/buttonfabmenu#styling), [Direction](https://mudblazor.com/components/buttonfabmenu#direction), [Default Variant](https://mudblazor.com/components/buttonfabmenu#default-variant), [Filled Variant](https://mudblazor.com/components/buttonfabmenu#filled-variant), [Outlined Variant](https://mudblazor.com/components/buttonfabmenu#outlined-variant), [Text Variant](https://mudblazor.com/components/buttonfabmenu#text-variant), [Inherited Variant](https://mudblazor.com/components/buttonfabmenu#inherited-variant), [Mixed Variants](https://mudblazor.com/components/buttonfabmenu#mixed-variants) |

### Data display

| Component | Types | What it is for | Examples on its page |
| --- | --- | --- | --- |
| [Typography](https://mudblazor.com/components/typography) | `MudText` | All themed text: headings, body, caption. Use it instead of raw `<h1>` or `<p>`. | [General](https://mudblazor.com/components/typography#general), [Alignment](https://mudblazor.com/components/typography#alignment), [Inline](https://mudblazor.com/components/typography#inline) |
| [Data Grid](https://mudblazor.com/components/datagrid) | `MudDataGrid<T>` | Column-defined grid: paging, sorting, column filters, grouping, inline edit, resizing, reordering, aggregation, sticky columns, virtualization, server data. The default for new tabular work. | [Default Data Grid](https://mudblazor.com/components/datagrid#default-data-grid), [Column Types](https://mudblazor.com/components/datagrid#column-types), [Advanced Data Grid](https://mudblazor.com/components/datagrid#advanced-data-grid), [Visual Styling](https://mudblazor.com/components/datagrid#visual-styling), [Editing](https://mudblazor.com/components/datagrid#editing), [Inline Row Editing](https://mudblazor.com/components/datagrid#inline-row-editing), [Grouping](https://mudblazor.com/components/datagrid#grouping), [Sorting](https://mudblazor.com/components/datagrid#sorting), [Advanced Sorting](https://mudblazor.com/components/datagrid#advanced-sorting), [Filtering](https://mudblazor.com/components/datagrid#filtering), [Row Selection](https://mudblazor.com/components/datagrid#row-selection), [Row Detail View](https://mudblazor.com/components/datagrid#row-detail-view), [Row Detail Renderer](https://mudblazor.com/components/datagrid#row-detail-renderer), [Column Sizing](https://mudblazor.com/components/datagrid#column-sizing), [Resizing](https://mudblazor.com/components/datagrid#resizing), [Data Aggregation](https://mudblazor.com/components/datagrid#data-aggregation), [Sticky Columns](https://mudblazor.com/components/datagrid#sticky-columns), [Server Side Filtering, Sorting and Pagination](https://mudblazor.com/components/datagrid#server-side-filtering,-sorting-and-pagination), [Virtualization](https://mudblazor.com/components/datagrid#virtualization), [Virtualize server data](https://mudblazor.com/components/datagrid#virtualize-server-data), [Observability](https://mudblazor.com/components/datagrid#observability), [CultureInfo](https://mudblazor.com/components/datagrid#cultureinfo), [Column Reordering](https://mudblazor.com/components/datagrid#column-reordering), [Columns Panel](https://mudblazor.com/components/datagrid#columns-panel), [Context Menu](https://mudblazor.com/components/datagrid#context-menu), [Cell Context Menu](https://mudblazor.com/components/datagrid#cell-context-menu), [Validator](https://mudblazor.com/components/datagrid#validator), [Accessibility](https://mudblazor.com/components/datagrid#accessibility), [Other Options](https://mudblazor.com/components/datagrid#other-options) |
| [Table](https://mudblazor.com/components/table) | `MudTable<T>` | Row-template table: paging, sorting, selection, inline edit, grouping, virtualization, server data. Older and less capable than Data Grid. | [Default Table](https://mudblazor.com/components/table#default-table), [Click Event and display for selected Row](https://mudblazor.com/components/table#click-event-and-display-for-selected-row), [Hover Events](https://mudblazor.com/components/table#hover-events), [Table with pagination and filtering](https://mudblazor.com/components/table#table-with-pagination-and-filtering), [TablePager Customization](https://mudblazor.com/components/table#tablepager-customization), [Sorting](https://mudblazor.com/components/table#sorting), [Multi-Selection](https://mudblazor.com/components/table#multi-selection), [Cell Class](https://mudblazor.com/components/table#cell-class), [Fixed header and footer](https://mudblazor.com/components/table#fixed-header-and-footer), [Column Group and Text Alignment](https://mudblazor.com/components/table#column-group-and-text-alignment), [Inline Edit Mode](https://mudblazor.com/components/table#inline-edit-mode), [Server Side Filtering, Sorting and Pagination](https://mudblazor.com/components/table#server-side-filtering,-sorting-and-pagination), [Server Side Data With Cancellation](https://mudblazor.com/components/table#server-side-data-with-cancellation), [Loading Content](https://mudblazor.com/components/table#loading-content), [Record Type Support](https://mudblazor.com/components/table#record-type-support), [Header and Footer](https://mudblazor.com/components/table#header-and-footer), [Table with related data](https://mudblazor.com/components/table#table-with-related-data), [Horizontal Scrolling](https://mudblazor.com/components/table#horizontal-scrolling), [Table Virtualization](https://mudblazor.com/components/table#table-virtualization), [Programmatic Scroll & Focus](https://mudblazor.com/components/table#programmatic-scroll-&-focus), [Grouping (Basic)](https://mudblazor.com/components/table#grouping-%28basic%29), [Grouping (Basic) - Initially collapsed](https://mudblazor.com/components/table#grouping-%28basic%29---initially-collapsed), [Grouping (Multi Levels)](https://mudblazor.com/components/table#grouping-%28multi-levels%29), [Accessibility](https://mudblazor.com/components/table#accessibility) |
| [Simple Table](https://mudblazor.com/components/simpletable) | `MudSimpleTable` | Styled plain HTML table when you write the rows yourself. | [Simple](https://mudblazor.com/components/simpletable#simple), [Hover & Dense](https://mudblazor.com/components/simpletable#hover-&-dense), [Fixed header](https://mudblazor.com/components/simpletable#fixed-header) |
| [List](https://mudblazor.com/components/list) | `MudList<T>`, `MudListItem<T>`, `MudListSubheader` | Vertical list, nested, with single or multi selection. | [Simple List](https://mudblazor.com/components/list#simple-list), [Nested List](https://mudblazor.com/components/list#nested-list), [Single-Selection](https://mudblazor.com/components/list#single-selection), [Multiselection](https://mudblazor.com/components/list#multiselection), [Interactive](https://mudblazor.com/components/list#interactive), [Avatar](https://mudblazor.com/components/list#avatar), [Accessibility](https://mudblazor.com/components/list#accessibility) |
| [Tree View](https://mudblazor.com/components/treeview) | `MudTreeView<T>`, `MudTreeViewItem<T>` | Hierarchy with expand, select, filter and lazy server loading. | [Basic Usage](https://mudblazor.com/components/treeview#basic-usage), [Usage](https://mudblazor.com/components/treeview#usage), [Icons](https://mudblazor.com/components/treeview#icons), [Single Selection](https://mudblazor.com/components/treeview#single-selection), [Multi Selection](https://mudblazor.com/components/treeview#multi-selection), [Binding Items Directly](https://mudblazor.com/components/treeview#binding-items-directly), [Auto-Expand](https://mudblazor.com/components/treeview#auto-expand), [Item Template](https://mudblazor.com/components/treeview#item-template), [Server-Side Data](https://mudblazor.com/components/treeview#server-side-data), [Filtering](https://mudblazor.com/components/treeview#filtering), [Custom Look and Behavior](https://mudblazor.com/components/treeview#custom-look-and-behavior), [Custom Body Content](https://mudblazor.com/components/treeview#custom-body-content) |
| [Expansion Panels](https://mudblazor.com/components/expansionpanels) | `MudExpansionPanels`, `MudExpansionPanel` | Accordion of collapsible sections. | [Simple Usage](https://mudblazor.com/components/expansionpanels#simple-usage), [Multiple Expanded Panels](https://mudblazor.com/components/expansionpanels#multiple-expanded-panels), [Async loading of data](https://mudblazor.com/components/expansionpanels#async-loading-of-data), [Disabled](https://mudblazor.com/components/expansionpanels#disabled), [Padding](https://mudblazor.com/components/expansionpanels#padding), [Borders](https://mudblazor.com/components/expansionpanels#borders), [Customizing the Header and Icon](https://mudblazor.com/components/expansionpanels#customizing-the-header-and-icon) |
| [Collapse](https://mudblazor.com/components/collapse) | `MudCollapse` | Animated show and hide of any content, bound to a bool. | [Simple Usage](https://mudblazor.com/components/collapse#simple-usage) |
| [Chips](https://mudblazor.com/components/chips) | `MudChip<T>` | Compact tag, label or filter token; closable and clickable. | [Filled Chips](https://mudblazor.com/components/chips#filled-chips), [Text Chips](https://mudblazor.com/components/chips#text-chips), [Outlined Chips](https://mudblazor.com/components/chips#outlined-chips), [Closable](https://mudblazor.com/components/chips#closable), [Clickable](https://mudblazor.com/components/chips#clickable), [Icons](https://mudblazor.com/components/chips#icons), [Avatars](https://mudblazor.com/components/chips#avatars), [Label](https://mudblazor.com/components/chips#label), [Sizes](https://mudblazor.com/components/chips#sizes), [Link Chip](https://mudblazor.com/components/chips#link-chip) |
| [Chip Set](https://mudblazor.com/components/chipset) | `MudChipSet<T>` | Group of chips with single or multi selection. | [Single selection](https://mudblazor.com/components/chipset#single-selection), [Multiselection](https://mudblazor.com/components/chipset#multiselection), [Adding and removing chips](https://mudblazor.com/components/chipset#adding-and-removing-chips), [Default chips](https://mudblazor.com/components/chipset#default-chips), [Binding chips in a selection](https://mudblazor.com/components/chipset#binding-chips-in-a-selection), [Variants](https://mudblazor.com/components/chipset#variants), [Selected Color](https://mudblazor.com/components/chipset#selected-color), [Accessibility](https://mudblazor.com/components/chipset#accessibility) |
| [Avatar](https://mudblazor.com/components/avatar) | `MudAvatar`, `MudAvatarGroup` | Initials, icon or image in a circle or square; stacked groups. | [Usage](https://mudblazor.com/components/avatar#usage), [Outlined](https://mudblazor.com/components/avatar#outlined), [Icons](https://mudblazor.com/components/avatar#icons), [Images](https://mudblazor.com/components/avatar#images), [Sizes](https://mudblazor.com/components/avatar#sizes), [Shapes](https://mudblazor.com/components/avatar#shapes), [Groups](https://mudblazor.com/components/avatar#groups), [Badges](https://mudblazor.com/components/avatar#badges) |
| [Badge](https://mudblazor.com/components/badge) | `MudBadge` | Dot, count or icon overlaid on another element. | [Usage](https://mudblazor.com/components/badge#usage), [Playground](https://mudblazor.com/components/badge#playground) |
| [Icons](https://mudblazor.com/components/icons) | `MudIcon` | Material icons (all variants), font icons or custom SVG. Look names up in the [Icon Reference](https://mudblazor.com/features/icons). | [Icons](https://mudblazor.com/components/icons#icons), [Usage](https://mudblazor.com/components/icons#usage), [Color](https://mudblazor.com/components/icons#color), [Size](https://mudblazor.com/components/icons#size), [Material Variants](https://mudblazor.com/components/icons#material-variants), [Font Icons](https://mudblazor.com/components/icons#font-icons), [Custom SVG Icons](https://mudblazor.com/components/icons#custom-svg-icons) |
| [Image](https://mudblazor.com/components/image) | `MudImage` | Image with fit, position and fallback. | [Usage](https://mudblazor.com/components/image#usage), [Fallback Image](https://mudblazor.com/components/image#fallback-image), [Size](https://mudblazor.com/components/image#size), [Responsive Images](https://mudblazor.com/components/image#responsive-images), [Image Fit](https://mudblazor.com/components/image#image-fit), [Image Position](https://mudblazor.com/components/image#image-position), [Playground](https://mudblazor.com/components/image#playground) |
| [Timeline](https://mudblazor.com/components/timeline) | `MudTimeline`, `MudTimelineItem` | Chronological events, vertical or horizontal. | [Basic](https://mudblazor.com/components/timeline#basic), [Item Dots](https://mudblazor.com/components/timeline#item-dots), [Dot Icon](https://mudblazor.com/components/timeline#dot-icon), [Dot class](https://mudblazor.com/components/timeline#dot-class), [Orientation and Position](https://mudblazor.com/components/timeline#orientation-and-position), [Opposite](https://mudblazor.com/components/timeline#opposite), [Item Modifiers](https://mudblazor.com/components/timeline#item-modifiers), [Item Align](https://mudblazor.com/components/timeline#item-align), [Timeline Align](https://mudblazor.com/components/timeline#timeline-align) |
| [Carousel](https://mudblazor.com/components/carousel) | `MudCarousel<T>` | Slide-by-slide content with transitions and data binding. | [Example](https://mudblazor.com/components/carousel#example), [DataBinding](https://mudblazor.com/components/carousel#databinding), [Custom Transition](https://mudblazor.com/components/carousel#custom-transition), [Transitions per page](https://mudblazor.com/components/carousel#transitions-per-page), [Elements Templates](https://mudblazor.com/components/carousel#elements-templates) |
| [Highlighter](https://mudblazor.com/components/highlighter) | `MudHighlighter` | Highlights matching text in a string; pairs with a search box. | [Usage](https://mudblazor.com/components/highlighter#usage), [Search, appearance](https://mudblazor.com/components/highlighter#search,-appearance), [Multiple Highlights](https://mudblazor.com/components/highlighter#multiple-highlights) |

### Feedback and status

| Component | Types | What it is for | Examples on its page |
| --- | --- | --- | --- |
| [Alert](https://mudblazor.com/components/alert) | `MudAlert` | Inline message with a severity (normal, info, success, warning, error). | [Simple alerts](https://mudblazor.com/components/alert#simple-alerts), [Variants](https://mudblazor.com/components/alert#variants), [Dense](https://mudblazor.com/components/alert#dense), [No Icons](https://mudblazor.com/components/alert#no-icons), [Rounded and Square](https://mudblazor.com/components/alert#rounded-and-square), [Elevation](https://mudblazor.com/components/alert#elevation), [Content Alignment](https://mudblazor.com/components/alert#content-alignment), [Close Icon and Event](https://mudblazor.com/components/alert#close-icon-and-event) |
| [Snackbar](https://mudblazor.com/components/snackbar) | `ISnackbar`, `MudSnackbarProvider` | Transient toast raised from code. | [Usage](https://mudblazor.com/components/snackbar#usage), [HTML in messages](https://mudblazor.com/components/snackbar#html-in-messages), [RenderFragment messages](https://mudblazor.com/components/snackbar#renderfragment-messages), [Custom component messages](https://mudblazor.com/components/snackbar#custom-component-messages), [Alerts and Severity](https://mudblazor.com/components/snackbar#alerts-and-severity), [Configuration](https://mudblazor.com/components/snackbar#configuration), [Snackbar Position](https://mudblazor.com/components/snackbar#snackbar-position), [Snackbar Variants](https://mudblazor.com/components/snackbar#snackbar-variants), [Close after navigation](https://mudblazor.com/components/snackbar#close-after-navigation), [Custom Action Handling](https://mudblazor.com/components/snackbar#custom-action-handling), [Require Interaction](https://mudblazor.com/components/snackbar#require-interaction), [No Icon](https://mudblazor.com/components/snackbar#no-icon), [Custom Icon](https://mudblazor.com/components/snackbar#custom-icon), [Programmatically remove](https://mudblazor.com/components/snackbar#programmatically-remove), [Preventing duplication](https://mudblazor.com/components/snackbar#preventing-duplication) |
| [Progress](https://mudblazor.com/components/progress) | `MudProgressCircular`, `MudProgressLinear` | Spinner or bar, determinate or indeterminate. | [Circular Progress](https://mudblazor.com/components/progress#circular-progress), [Linear Progress](https://mudblazor.com/components/progress#linear-progress) |
| [Skeleton](https://mudblazor.com/components/skeleton) | `MudSkeleton` | Placeholder shapes while content loads. | [Variants](https://mudblazor.com/components/skeleton#variants), [Animations](https://mudblazor.com/components/skeleton#animations), [Pulsate Example](https://mudblazor.com/components/skeleton#pulsate-example), [Wave Example](https://mudblazor.com/components/skeleton#wave-example) |
| [Tooltip](https://mudblazor.com/components/tooltip) | `MudTooltip` | Hover or focus hint, with HTML content and arrows. | [Simple Tooltips](https://mudblazor.com/components/tooltip#simple-tooltips), [Arrow Tooltips](https://mudblazor.com/components/tooltip#arrow-tooltips), [Colored Tooltips](https://mudblazor.com/components/tooltip#colored-tooltips), [HTML Tooltips](https://mudblazor.com/components/tooltip#html-tooltips), [Transitions](https://mudblazor.com/components/tooltip#transitions), [Activation Events](https://mudblazor.com/components/tooltip#activation-events) |

### Dialogs, overlays and popovers

| Component | Types | What it is for | Examples on its page |
| --- | --- | --- | --- |
| [Dialog](https://mudblazor.com/components/dialog) | `IDialogService`, `MudDialog` | Modal opened from code or inline, with parameters and a result. | [Usage](https://mudblazor.com/components/dialog#usage), [Configuration](https://mudblazor.com/components/dialog#configuration), [Templating and Passing Simple Data](https://mudblazor.com/components/dialog#templating-and-passing-simple-data), [Passing Data](https://mudblazor.com/components/dialog#passing-data), [Scrollable Dialog](https://mudblazor.com/components/dialog#scrollable-dialog), [Blurry Dialog](https://mudblazor.com/components/dialog#blurry-dialog), [Inlining Dialog](https://mudblazor.com/components/dialog#inlining-dialog), [Nested Inline Dialogs](https://mudblazor.com/components/dialog#nested-inline-dialogs), [Nested Dialogs and Cancel All](https://mudblazor.com/components/dialog#nested-dialogs-and-cancel-all), [Keyboard Accessibility](https://mudblazor.com/components/dialog#keyboard-accessibility), [Focus Trap](https://mudblazor.com/components/dialog#focus-trap), [Custom Styling](https://mudblazor.com/components/dialog#custom-styling) |
| [Message Box](https://mudblazor.com/components/messagebox) | `MudMessageBox`, `IDialogService.ShowMessageBoxAsync` | Ready-made confirm or yes/no/cancel dialog. | [Message Box](https://mudblazor.com/components/messagebox#message-box), [Custom Message Box](https://mudblazor.com/components/messagebox#custom-message-box), [Multiline Message](https://mudblazor.com/components/messagebox#multiline-message), [Message Box With DialogOptions](https://mudblazor.com/components/messagebox#message-box-with-dialogoptions), [Button Order](https://mudblazor.com/components/messagebox#button-order) |
| [Overlay](https://mudblazor.com/components/overlay) | `MudOverlay` | Scrim over the page or a container, optionally holding a loader. | [AutoClose](https://mudblazor.com/components/overlay#autoclose), [Absolute](https://mudblazor.com/components/overlay#absolute), [Color](https://mudblazor.com/components/overlay#color), [Z-Index](https://mudblazor.com/components/overlay#z-index), [ChildContent as a Loader](https://mudblazor.com/components/overlay#childcontent-as-a-loader), [Positioning](https://mudblazor.com/components/overlay#positioning) |
| [Popover](https://mudblazor.com/components/popover) | `MudPopover` | Anchored floating content: the primitive under menus, selects and tooltips. | [Simple Popover](https://mudblazor.com/components/popover#simple-popover), [Direction and Location](https://mudblazor.com/components/popover#direction-and-location), [Overflow Behavior](https://mudblazor.com/components/popover#overflow-behavior), [Complex Content](https://mudblazor.com/components/popover#complex-content), [Popover RelativeWidth](https://mudblazor.com/components/popover#popover-relativewidth), [Popover Inception](https://mudblazor.com/components/popover#popover-inception), [Individual Dropdown Settings](https://mudblazor.com/components/popover#individual-dropdown-settings) |
| [Exit Prompt](https://mudblazor.com/components/exitprompt) | `MudExitPrompt` | Warns before the user navigates away with unsaved work. | [Usage](https://mudblazor.com/components/exitprompt#usage) |

### Interaction utilities

| Component | Types | What it is for | Examples on its page |
| --- | --- | --- | --- |
| [Drop Zone](https://mudblazor.com/components/dropzone) | `MudDropContainer<T>`, `MudDropZone<T>` | Drag-and-drop between zones and reordering: kanban boards, sortable lists. | [Basic Usage](https://mudblazor.com/components/dropzone#basic-usage), [Nested Drop Zones](https://mudblazor.com/components/dropzone#nested-drop-zones), [Transfer items between Drop Zones](https://mudblazor.com/components/dropzone#transfer-items-between-drop-zones), [Drop Rules](https://mudblazor.com/components/dropzone#drop-rules), [Dragging Overrides](https://mudblazor.com/components/dropzone#dragging-overrides), [Disabled items](https://mudblazor.com/components/dropzone#disabled-items), [Miscellaneous](https://mudblazor.com/components/dropzone#miscellaneous) |
| [Focus Trap](https://mudblazor.com/components/focustrap) | `MudFocusTrap` | Keeps keyboard focus inside a region. | [Usage](https://mudblazor.com/components/focustrap#usage) |
| [Hotkey](https://mudblazor.com/components/hotkey) | `MudHotkey` | Binds a keyboard shortcut to a callback. | [Usage](https://mudblazor.com/components/hotkey#usage) |
| [Swipe Area](https://mudblazor.com/components/swipearea) | `MudSwipeArea` | Touch swipe gestures. | [Swipe directions](https://mudblazor.com/components/swipearea#swipe-directions), [Real Time Swipe](https://mudblazor.com/components/swipearea#real-time-swipe), [Prevent default browser behavior](https://mudblazor.com/components/swipearea#prevent-default-browser-behavior), [Drawer Example](https://mudblazor.com/components/swipearea#drawer-example), [DatePicker Example](https://mudblazor.com/components/swipearea#datepicker-example), [Swipe Box's Drag Example](https://mudblazor.com/components/swipearea#swipe-box's-drag-example) |

### Charts

| Component | Types | What it is for | Examples on its page |
| --- | --- | --- | --- |
| [Line Chart](https://mudblazor.com/components/linechart) | `Line<T>` | Series over a category axis. | [Basic Usage](https://mudblazor.com/components/linechart#basic-usage), [Y-Axis Ticks](https://mudblazor.com/components/linechart#y-axis-ticks), [Interpolation](https://mudblazor.com/components/linechart#interpolation), [Hide Chart Series](https://mudblazor.com/components/linechart#hide-chart-series) |
| [Bar Chart](https://mudblazor.com/components/barchart) | `Bar<T>` | Grouped bars. | [Value Labels](https://mudblazor.com/components/barchart#value-labels), [Custom SVG Content](https://mudblazor.com/components/barchart#custom-svg-content) |
| [Stacked Bar Chart](https://mudblazor.com/components/stackedbarchart) | `StackedBar<T>` | Stacked bars. | [Value Labels](https://mudblazor.com/components/stackedbarchart#value-labels), [Custom SVG Content](https://mudblazor.com/components/stackedbarchart#custom-svg-content) |
| [Time Series Chart](https://mudblazor.com/components/timeserieschart) | `TimeSeries<T>` | Values over a real time axis. | [Basic Usage](https://mudblazor.com/components/timeserieschart#basic-usage) |
| [Pie Chart](https://mudblazor.com/components/piechart) | `Pie<T>` | Parts of a whole. | [Basic Pie](https://mudblazor.com/components/piechart#basic-pie) |
| [Donut Chart](https://mudblazor.com/components/donutchart) | `Donut<T>` | Parts of a whole, with room in the middle for a total. | [Basic Donut](https://mudblazor.com/components/donutchart#basic-donut), [Legend Position](https://mudblazor.com/components/donutchart#legend-position), [Custom SVG Content](https://mudblazor.com/components/donutchart#custom-svg-content) |
| [Heat Map Chart](https://mudblazor.com/components/heatmapchart) | `HeatMap<T>` | Grid of cells coloured by value. | [Basic HeatMap](https://mudblazor.com/components/heatmapchart#basic-heatmap), [X and Y Axis Label Positioning](https://mudblazor.com/components/heatmapchart#x-and-y-axis-label-positioning), [Enable Smooth Gradient](https://mudblazor.com/components/heatmapchart#enable-smooth-gradient), [Display Values](https://mudblazor.com/components/heatmapchart#display-values), [MudHeatMapCell](https://mudblazor.com/components/heatmapchart#mudheatmapcell), [Setting Minimum and Maximum Value](https://mudblazor.com/components/heatmapchart#setting-minimum-and-maximum-value), [Legend Display](https://mudblazor.com/components/heatmapchart#legend-display) |
| [Radar Chart](https://mudblazor.com/components/radarchart) | `Radar<T>` | Several measures on radial axes. | [Basic Radar Chart](https://mudblazor.com/components/radarchart#basic-radar-chart), [Customized Radar Chart](https://mudblazor.com/components/radarchart#customized-radar-chart) |
| [Rose Chart](https://mudblazor.com/components/rosechart) | `Rose<T>` | Polar-area chart. | [Basic Rose Chart](https://mudblazor.com/components/rosechart#basic-rose-chart), [Customized Rose Chart](https://mudblazor.com/components/rosechart#customized-rose-chart) |
| [Sankey Chart](https://mudblazor.com/components/sankeychart) | `Sankey<T>` | Flows between stages. | [Basic Usage](https://mudblazor.com/components/sankeychart#basic-usage), [Customization](https://mudblazor.com/components/sankeychart#customization), [Events](https://mudblazor.com/components/sankeychart#events) |
| [Scatter Plot Chart](https://mudblazor.com/components/scatterplotchart) | `ScatterPlot<T>` | Points on two numeric axes, with a regression line. | [Basic Usage](https://mudblazor.com/components/scatterplotchart#basic-usage), [Regression Line Overlay](https://mudblazor.com/components/scatterplotchart#regression-line-overlay) |
| [Universal chart](https://mudblazor.com/components/charts) | `MudChart<T>` | Chart type chosen at run time; mixed (combo) charts. | [Dynamic Chart Type](https://mudblazor.com/components/charts#dynamic-chart-type), [Mixed Chart (Combo Chart)](https://mudblazor.com/components/charts#mixed-chart-%28combo-chart%29) |
| [Chart Options](https://mudblazor.com/components/options) | `ChartOptions` | Options every chart shares. |  |

## Services, theming and CSS utilities

Not components, but the next place to look before writing code.

| Area | What is there |
| --- | --- |
| [Services](https://mudblazor.com/features/services) | `IBrowserViewportService` (react to window size), `IResizeObserver`, `IScrollManager`, `IScrollListener`. `IDialogService` and `ISnackbar` are documented on the Dialog and Snackbar pages |
| [Breakpoints](https://mudblazor.com/features/breakpoints) | The breakpoint names every responsive parameter uses |
| [Colors](https://mudblazor.com/features/colors) and [Elevation](https://mudblazor.com/features/elevation) | The `Color` and `Elevation` values components take |
| [Converters](https://mudblazor.com/features/converters) and [Masking](https://mudblazor.com/features/masking) | Typed binding and input masks for text fields |
| [Localization](https://mudblazor.com/features/localization) | Translating MudBlazor's own strings |
| [Theme customization](https://mudblazor.com/customization/overview) | Palette, typography and z-index. In Huddle, change a Theme in `Themes/`, not per component |
| CSS utility classes | [Spacing](https://mudblazor.com/utilities/spacing) (`pa-4`, `mt-2`), [Flex](https://mudblazor.com/utilities/flex), [Gap](https://mudblazor.com/utilities/gap), [Display](https://mudblazor.com/utilities/display), [Border radius](https://mudblazor.com/utilities/border-radius), [Overflow](https://mudblazor.com/utilities/overflow). Use these before adding a rule to a `.razor.css` |

## Keeping this page current

The catalogue was generated from MudBlazor's own docs source at the tag that
matches our package,
[`v9.10.0/src/MudBlazor.Docs`](https://github.com/MudBlazor/MudBlazor/tree/v9.10.0/src/MudBlazor.Docs):
component routes from each `Pages/Components/*/*Page.razor`, section anchors from
their `SectionHeader Title` values. The docs build an anchor as the title
lower-cased with spaces turned into hyphens, prefixed by the parent section's
title for nested sections. All 558 section links were checked against the live
site on 2026-09-24.

> [!NOTE]
> mudblazor.com documents the **latest** release, not 9.10.0. When they
> disagree, the tagged source above is the truth for this repo. When you bump
> MudBlazor in `Directory.Packages.props`, re-check this page: new components
> appear in the docs' `Services/Menu/MenuService.cs`.
