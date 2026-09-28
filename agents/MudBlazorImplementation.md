# MudBlazor for implementation

Use this page while writing or testing a component in `src/Huddle.App`: the house patterns to
copy, the Huddle rules that MudBlazor's own examples lead you to break, and how to check an API
against the pinned package, with the facts already checked. Choosing components belongs to the
spec and the plan — see [MudBlazorDesign.md](MudBlazorDesign.md). For bUnit technique, see
[BlazorTesting.md](BlazorTesting.md).

Applies to **MudBlazor 9.10.0**, the version pinned in
[`Directory.Packages.props`](../Directory.Packages.props).

## Components Huddle already uses

These have a house pattern. Read the named file first; it shows how Huddle binds,
styles and tests the component, which the MudBlazor example does not.

| Component | Read this first |
| --- | --- |
| Layout shell: `MudLayout`, `MudDrawer`, `MudMainContent`, providers | [`MainLayout.razor`](../src/Huddle.App/Components/Layout/MainLayout.razor) |
| `MudNavMenu`, `MudNavLink`, `MudMenu` | [`RoomList.razor`](../src/Huddle.App/Components/Shared/RoomList.razor) |
| `MudTextField`, `MudButton`, `MudIconButton`, `MudAlert`, `MudStack` | [`Chat.razor`](../src/Huddle.App/Components/Pages/Chat.razor) |
| `MudTabs`, `MudTabPanel` | [`Settings.razor`](../src/Huddle.App/Components/Pages/Settings.razor) |
| `MudSelect`, `MudRadioGroup`, `MudColorPicker`, `MudFileUpload` | [`TeammateCard.razor`](../src/Huddle.App/Components/Shared/TeammateCard.razor) |
| `MudList`, `MudPaper` | [`Appearance.razor`](../src/Huddle.App/Components/Settings/Appearance.razor) |
| `MudDialog` opened through `IDialogService` | [`ArchivedChatsDialog.razor`](../src/Huddle.App/Components/Shared/ArchivedChatsDialog.razor), opened from [`ArchivedChats.razor`](../src/Huddle.App/Components/Shared/ArchivedChats.razor) |
| Confirm through `ShowMessageBoxAsync`, report through `ISnackbar` | [`SkillsPanel.razor`](../src/Huddle.App/Components/Settings/SkillsPanel.razor) |
| `MudExpansionPanels`, `MudTable` | [`ProposalCard.razor`](../src/Huddle.App/Components/Shared/ProposalCard.razor) |
| `MudSimpleTable`, `MudChip` | [`SkillsPanel.razor`](../src/Huddle.App/Components/Settings/SkillsPanel.razor) |
| `MudCollapse`, `MudCheckBox` | [`NewChat.razor`](../src/Huddle.App/Components/Shared/NewChat.razor) |
| `MudAvatar` | [`TeammateAvatar.razor`](../src/Huddle.App/Components/Shared/TeammateAvatar.razor) |
| `MudDataGrid` grouped, with hidden columns | [`TaskListView.razor`](../src/Huddle.App/Components/Tasks/TaskListView.razor) |
| `MudDropContainer`, `MudDropZone` | [`TaskBoard.razor`](../src/Huddle.App/Components/Tasks/TaskBoard.razor) |
| `MudToggleGroup`, `MudTimeline`, `MudExitPrompt` | [`TaskDetail.razor`](../src/Huddle.App/Components/Tasks/TaskDetail.razor) |
| `MudBadge` | [`TaskCard.razor`](../src/Huddle.App/Components/Tasks/TaskCard.razor) |
| `MudNavGroup` | [`TaskViewNav.razor`](../src/Huddle.App/Components/Tasks/TaskViewNav.razor) |
| `MudSplitPanel`, divider saved through `GetDividerPositionAsync` | [`LibraryPaneHost.razor`](../src/Huddle.App/Components/Library/LibraryPaneHost.razor), [`LibraryExplorer.razor`](../src/Huddle.App/Components/Library/LibraryExplorer.razor) |
| `MudTreeView<T>` with `ServerData`, item text in `BodyContent` | [`LibraryTree.razor`](../src/Huddle.App/Components/Library/LibraryTree.razor) |
| `MudExitPrompt`, `MudBreadcrumbs`, `MudToolBar` | [`LibraryDocument.razor`](../src/Huddle.App/Components/Library/LibraryDocument.razor) |

To refresh this list, search `src/Huddle.App` for `<Mud`.

## Huddle rules that bite MudBlazor code

[Rules](../docs/agencyteam/rules.md) is binding for `src/Huddle.App`. These rows are the ones a
MudBlazor example will lead you to break:

- **No colour or font literals.** Stylesheets read `var(--mud-palette-*)` and
  `var(--mud-typography-*)` only, and inline `Style` carries no colour literal.
  Tests fail the build otherwise. Pass `Color="Color.Primary"` rather than a hex.
  A Theme is a `MudTheme` — see [ADR-0010](../docs/adr/0010-a-theme-is-a-mudblazor-theme.md).
- **`Disabled`, not `ReadOnly`, on an input that must look inert.** MudBlazor
  9.10.0 styles only the disabled state.
- **A `string` parameter needs a leading `@`.** `Label="this.name"` passes the
  literal text; the compiler cannot catch it.
- **No `MaxLength` on a `MudTextField` whose limit is counted in characters
  a human sees** (the avatar label is the case in the rules). It truncates by
  UTF-16 unit and can split a surrogate pair.

[Traps](../docs/agencyteam/traps.md) and the house components record the rest. These are the ones
a MudBlazor example won't warn you about:

- **`MudAlert` has no `role`.** It emits no role and no aria. Add `role="alert"`
  for an interruption and `role="status"` for a consequence, yourself
  ([traps.md](../docs/agencyteam/traps.md), the `MudAlert` entry).
- **An open dialog's parameters are frozen.** MudBlazor ignores every
  `SetParametersAsync` after `IDialogService.ShowAsync` opens it. Pass an id or
  a snapshot, and let the dialog load and subscribe for itself. The reasoning is
  in the header comment of [`TeammateCard.razor`](../src/Huddle.App/Components/Shared/TeammateCard.razor).
- **Scoped CSS can't reach inside a MudBlazor component.** `::deep` only
  matches under an HTML element written in your own markup, and a `MudDrawer` or
  `MudLayout` is a component, not one. Such rules go in `app.css`; see the
  comment above `.mud-drawer-content` there.
- **bUnit needs the real popover and dialog hosts.** `MudSelect` items appear
  only after a real click, and a `MudDialog` renders nothing on its own. Render
  through `MudBunitContext.RenderWithPopovers` and drive dialogs through the
  real `IDialogService` ([testing.md](../docs/agencyteam/testing.md)).

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
| `MudDataGrid<T>` | A sortable header's clickable element is `span.sortable-column-header` inside the `<th>` — clicking the `<th>` itself throws `MissingEventHandlerException` |
| `MudMenu` | Its content portals into `MudPopoverProvider` from `RenderWithPopovers` — search the outer render fragment, not a `FindComponent<Owner>()` handle. Open it by clicking its activator button |
| `MudTooltip` | Its text is not in bUnit's static markup without a hover — assert via `aria-label` instead |
| `MudBunitContext` | Must be disposed with `await using` — its services are `IAsyncDisposable` only |
| MUD0012 / `GetState` | A bindable parameter (`MudSelect.SelectedValues`, `MudTextField.Value`) is analyzer-blocked from a direct `.Instance.Value` read — use `component.Instance.GetState(x => x.Value)` (`using MudBlazor.Extensions;`), or assert the rendered markup |
| `MudSelect<T>` | Its popover toggles on `mousedown`, not `click` — a plain `click` throws `MissingEventHandlerException` |
| `MudExpansionPanel` | Keeps its collapsed content in the DOM — `MudCollapse` only hides it with CSS. Don't assert `Empty` before expanding |
| `MudChip<T>.Icon` | Renders as `svg.mud-chip-icon`. AngleSharp re-serialises the path tags, so a literal icon-string `Contains` assertion fails |
| `IDialogService.ShowMessageBoxAsync` | Its DOM is `.mud-dialog-title` / `.mud-dialog-content` / `.mud-dialog-actions button` |
| `MudDropContainer<T>` | `StartTransaction` (void) raises `TransactionStarted` synchronously; `CancelTransaction`/`CommitTransaction` return `Task` and raise `TransactionEnded`; `Commit` does not re-check `CanDrop`. A parent's `StateHasChanged` does not re-render its cards — call `container.Refresh()` instead |
| `MudDialog` with `CloseButton` | Renders `button.mud-button-close`; the title renders in `.mud-dialog-title` |
| bUnit `Find`/`Click` | A `Find(...)` then `.Click()` can race a fire-and-forget re-render under load (`UnknownEventHandlerIdException`) — click via `await cut.InvokeAsync(() => cut.Find(sel).Click())` instead |
| `ISnackbar.Add` | Every overload (`string`, `MarkupString`, `RenderFragment`, component parameters) takes `(message, Severity, Action<SnackbarOptions>? configure, string? key)`. The same `key` collapses duplicates |
| `MudDrawer` | **No resize.** `Width` is a fixed string, and there is no drag handle or width callback. For two resizable panes use `MudSplitPanel`. *Checked 2026-09-24 for the Library* |
| `MudSplitPanel.Horizontal` | **Names the divider's orientation, not the panels' layout direction** - the opposite of what the name suggests. `Horizontal="true"` draws a horizontal dividing *line*, which separates panels stacked top/bottom and sizes the first panel by height; `Horizontal="false"` draws a vertical line, separating panels left/right, sized by width (`_setPanelSizes` in the package's own `MudBlazor.min.js` sets `firstPanel.style.height` only when `this.horizontal`, `.width` otherwise). Side-by-side panels want `Horizontal="false"`; stacked panels want `Horizontal="true"`. Without a `FirstPanelInitialSize`, `resetSizes()` never calls `_setPanelSizes` at all, so the size axis falls back to an even flex-grow split. *Checked 2026-09-28 after the Library's own `LibraryExplorer.razor` had this inverted since it was first written - undetected because bUnit runs no real CSS layout and the one layout-orientation test asserted a `mud-split-panel-vertical` class this version of MudBlazor never emits.* |
| `MudSplitPanel` | `FirstPanel`/`SecondPanel` are `RenderFragment`s. `FirstPanelInitialSize` is `int?` and `MinPanelSize` is `int`, **both in pixels**. `Horizontal` picks the direction. There is **no change callback**: read the divider with `GetDividerPositionAsync()` (`Task<int>`) and restore it with `SetDividerPositionAsync(int offset)` through `@ref`. *Checked for the Library* |
| `MudTreeView<T>` | `ServerData` is `Func<T, Task<IReadOnlyCollection<TreeItemData<T>>>>`, for lazy child loading. `ItemTemplate`, `FilterFunc`, `SelectedValue`/`SelectedValueChanged` exist. *Checked for the Library* |
| `MudTreeViewItem<T>` | Has `OnClick` and `OnDoubleClick` but **no right-click event**. For a context menu, wrap the item content in a `MudMenu` (next row). *Checked for the Library* |
| `MudMenu` | `ActivationEvent` is a `MouseEvent` (`LeftClick`, `RightClick`, `MouseOver`). `PositionAtCursor` opens it at the pointer. `OpenMenuAsync(EventArgs, bool)` opens it from code. *Checked for the Library* |
| `MudHotkey` | `Key` is a `JsKey` and `KeyModifiers` is an `IEnumerable<JsKeyModifier>` whose values are **side-specific** (`ControlLeft`, `ControlRight`, `ShiftLeft`…), with no plain `Control`. *Checked for the Library* |

When you check a new fact, add a row. When `Directory.Packages.props` moves
MudBlazor to a new version, re-check every row.

## Keeping this page current

Add a row to [Facts already checked](#facts-already-checked) whenever you confirm a parameter,
event or rendering detail against the package, one fact per row, saying how it was confirmed.
Add a row to [Components Huddle already uses](#components-huddle-already-uses) when a component
first ships in `src/Huddle.App`. When you bump MudBlazor in `Directory.Packages.props`, re-check
every fact.
