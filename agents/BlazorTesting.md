# Testing Blazor components with bUnit

How to test Razor components in `tests/Huddle.Tests/Ui` with bUnit 2.11.3 and MudBlazor 9.10, and
the Razor authoring traps those tests keep catching. Read it before writing a component or its
tests. General test practice is in [Testing.md](Testing.md); per-component MudBlazor facts (which
parameter exists, how a component renders) are in
[MudBlazorImplementation.md](MudBlazorImplementation.md) → *Facts already checked* — check
there before guessing an API.

Distilled from the Tasks delivery (September 2026), which added ~40 component test classes.

## Setting up a test

```csharp
/// <summary>Clicking a card's open button raises OnOpen once (modelled on TaskCardTests).</summary>
[Fact]
public async Task OpenButtonClick_RaisesOnOpen()
{
    TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T");
    int openCount = 0;
    using TempDataDir dir = new();
    await using MudBunitContext ctx = new();
    ctx.Services.AddSingleton<TimeProvider>(new ManualTimeProvider());
    ctx.Services.AddSingleton(new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance));

    IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<TaskCard>(0);
        builder.AddAttribute(1, nameof(TaskCard.Task), task);
        builder.AddAttribute(2, nameof(TaskCard.Fields), Array.Empty<string>());
        builder.AddAttribute(3, nameof(TaskCard.OnOpen), EventCallback.Factory.Create(this, () => openCount++));
        builder.CloseComponent();
    });

    await cut.InvokeAsync(() => cut.Find("div.task-card > button.task-card-open").Click());

    Assert.Equal(1, openCount);
}
```

For a component that needs the whole Tasks stack (stores, services, presence), register it with
`TaskToolHarness.AddTo(ctx.Services)` instead of adding services one by one.

- **Dispose `MudBunitContext` with `await using`.** Its services are `IAsyncDisposable` only.
- **Write `Xunit.TestContext.Current`.** With `using Bunit;` in scope, a bare `TestContext`
  resolves to bUnit's obsolete type (`CS0117`/`CS0618`).
- **Use `RenderWithPopovers`** for anything that opens a menu, select, popover or dialog. Their
  content portals into the popover provider, so search the returned root, not a
  `FindComponent<Owner>()` handle.
- bUnit 2.11.3 has no `SetParametersAndRender`; re-render with `cut.Render(p => …)`.
  `BunitContext.DisposeComponentsAsync()` disposes rendered components, so a test can then assert
  they unsubscribed.

## Clicking without racing a re-render

A component that re-renders on its own (an event handler that requeries with `_ = DispatchAsync(…)`,
a watcher reload) can land a render between `Find` and `Click`, so the click hits a stale handler
and throws `UnknownEventHandlerIdException` — only under load. Make the pair atomic:

```csharp
await cut.InvokeAsync(() => cut.Find("button.save").Click());
```

Use it for every click that follows a concurrent change or a fresh render. If a click still races
**inside** `InvokeAsync`, the component is re-rendering when nothing changed — look for spurious
events in the product (this found three real store bugs), don't add a wait.

- A `MudSelect` opens on **`mousedown`**, not `click` (a click throws `MissingEventHandlerException`).
- A `MudMenu` opens by clicking its activator **button**; read items as `div.mud-menu-item`
  (the `RoomListTests` pattern). Clicking a `MudMenuItem` completes asynchronously even when no
  dialog opens — bound "nothing happened" asserts with `WaitAsync(timeout, ct)`.
- A `MudRadioGroup` created inside `@foreach` and re-created by an async render silently ignores a
  DOM click — drive it with `Instance.ValueChanged.InvokeAsync(value)`.
- To test an action that opens a dialog: start it without awaiting, answer the dialog through the
  rendered dialog provider, then await the task.

## Finding elements and asserting text

- **Assert text on the element, exactly.** Find the element by a stable class hook and compare its
  trimmed `TextContent` with `Assert.Equal` — never `Assert.Contains` on `cut.Markup`. Add a class
  hook to the component if none exists.
- **Element handles aren't stable across queries.** Two `Find`s of the same node are neither
  `Same` nor `Equal`. Prove containment with the DOM: `Assert.True(card.Contains(button))`.
- **Assert placement as a whole list** — the zone's card ids, the menu's item labels — not by
  membership.
- Raw HTTP output encodes `'` as `&#x27;`. Parse HTML outside a render with
  `new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html)`.
- `app.js` and `app.css` have no runner: pin their rules as source facts (`AppJsSourceTests`,
  `AppCssTasksTests`), using ordered `IndexOf` checks where order matters.

## Reading component state

- **Bindable parameters** (`Value`, `SelectedValues`) are blocked from tests by analyzer
  `MUD0012`: read them with `component.Instance.GetState(x => x.Value)` (`using MudBlazor.Extensions;`),
  or read the rendered input's `value` attribute.
- **Plain parameters** (`Lines`, `MaxLines`, `Size`, `DateFormat`) read directly from
  `.Instance`; `GetState` throws for them.
- `MudSelectItem<T>` components exist even while the select is closed (`FindComponents`); their
  DOM exists only when open.
- `MudBadge.BadgeAriaLabel` is the assertable "says it in words" surface (tooltip text isn't in
  static markup without hover).
- `ISnackbar.ShownSnackbars` exposes only `Message` and `Severity` — no key; assert de-duplication
  by count. A message built from a `RenderFragment` (for a link) must be asserted through markup.
- To prove "Dispose unsubscribes", reflect the event's backing field and count
  `GetInvocationList()`; filter on `Target is YourComponent` when framework parts (drop zones)
  subscribe too.

## Writing a red for a component that doesn't exist yet

- Render it with the builder form and a fully qualified name, and omit the `using`:
  `builder.OpenComponent<Agency.Huddle.App.Components.Tasks.TaskCard>(0); builder.AddAttribute(1, nameof(TaskCard.Task), task);`.
  The lambda form `parameters.Add(p => p.Task, task)` pollutes the red with `CS9174`/`CS1929`.
- Object-initialising a MudBlazor component (`new MudChip<T> { Value = … }`) is blocked by `BL0005` —
  drive the rendered component instead (for a chip, `chip.Instance.OnClose.InvokeAsync(chip.Instance)`).
- Rendering a `RenderFragment<T>` item template a second time with `RenderWithPopovers` in the same
  context throws "already a subscriber" — use plain `ctx.Render(fragment)`.

## Avoiding Razor authoring traps

| Trap | Fix |
| --- | --- |
| `Search="this.search"` passes the **literal text** to a string parameter | `Search="@this.search"` (found twice on one page) |
| `@for` whose body captures `i` inside a child's `ChildContent` throws at render | `@foreach (var (value, index) in list.Select((v, i) => (v, i)))` |
| An `internal` type as a `[Parameter]` type | `CS0053` — the parameter's type must be public |
| Injecting an internal service into a public component | `@inject` is fine; `[Inject] required` gives `CS9032` |
| A component injecting `IOptions<T>` | Add `@using Microsoft.Extensions.Options` |
| `OnClick="this.Callback.InvokeAsync"` with `MouseEventArgs` | `CS0121` ambiguous — wrap it in a private method |
| `@onclick:stopPropagation` on a MudBlazor component with its own `OnClick` | `RZ10010` — wrap the component in a plain `<span @onclick:stopPropagation="true">` |
| An injected `ILogger` never called | `S4487` — call it or remove it |
| A parent's `StateHasChanged` doesn't re-render `MudDropContainer` items | Call `container.Refresh()` after the data changes |
| A parent reads a child component's state (for example "has unsaved edits") | Polling on the parent's render goes stale. Have the child raise an `EventCallback<bool>` when the state flips, and store it in the parent |
| A record holding an `IReadOnlyList<T>` compared for "changed" | Record equality compares the list by reference — write structural `Equals`/`GetHashCode` |
