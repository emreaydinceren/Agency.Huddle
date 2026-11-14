---
name: blazor-prerender-interop
description: >
  Prerendering and JS interop in Huddle.App's Blazor Server app (globally InteractiveServer with prerender on).
  USE FOR: a component that loads data twice, flickers or null-refs on a full page load; deciding between
  [PersistentState], a `??=` guard, RendererInfo.IsInteractive or prerender: false; adding or reviewing a JS call,
  a [JSInvokable] callback, DotNetObjectReference or JS disposal; JSDisconnectedException after a closed tab.
  DO NOT USE FOR: styling or MudBlazor component choice (agents/MudBlazorDesign.md,
  agents/MudBlazorImplementation.md), bUnit tests (agents/BlazorTesting.md), or navigation triggered by a server
  event (docs/engineering/rules.md: ServerNavigator).
license: MIT
---

# Prerendering and JS interop in Huddle.App

Drawn from `dotnet/skills` (`plugins/dotnet-blazor/skills/support-prerendering` and `use-js-interop`, MIT, see
`../LICENSE-dotnet-skills`), cut to what holds in this app. Where upstream and the house conventions disagree,
this file says so and the house wins.

## How this app renders

`Components/App.razor` puts `@rendermode="InteractiveServer"` on both `HeadOutlet` and `Routes`, with prerendering
left on. So on every **full page load** (first visit, reload, a new tab):

1. The server prerenders static HTML. `OnInitialized[Async]` runs; `OnAfterRender[Async]` does not; there is no JS.
2. The circuit attaches and renders the component again. `OnInitialized[Async]` runs a **second** time.

Navigation inside the app (interactive routing) skips step 1. A reconnected circuit behaves like step 2.
Existing code already reasons this way. Read the comments in `Chat.razor`, `BuildTeam.razor`,
`MainLayout.razor`, `TabPersistence.razor` and `TeamShortcutStore.cs` before changing a page's lifecycle.

## Stop a double load

Pick the first row that fits:

| Situation | Do |
| --- | --- |
| The data comes from an in-process store (most of Huddle) and is cheap | Nothing: loading twice is correct and harmless. Do not add machinery |
| The load is expensive, or the second load visibly changes the page | `[PersistentState]` on a public property, load with `??=` (below) |
| The work must only happen once the circuit is live (subscriptions, timers, anything that holds a resource) | Guard with `RendererInfo.IsInteractive`, or start it in `OnAfterRenderAsync(firstRender)` |
| The component cannot produce sensible static HTML at all | `@rendermode @(new InteractiveServerRenderMode(prerender: false))` on that component, last resort |

```razor
@code {
    [PersistentState]
    public IReadOnlyList<ReportRow>? Rows { get; set; }

    protected override async Task OnInitializedAsync()
    {
        this.Rows ??= await this.ReportSource.LoadAsync();   // illustrative names
    }
}
```

- `[PersistentState]` serialises the value into the prerendered page with System.Text.Json, so the type must
  round-trip. Records with init-only properties do; a type holding a service or a live object does not.
- The same component rendered several times needs `@key` on each instance so the states do not collide.
- `RendererInfo.IsInteractive` is `false` during prerender; `RendererInfo.Name` is `"Static"` then `"Server"`.
- Subscribing to a singleton event (`RoomEvents.RoomsChanged`, the Team catalog) during prerender leaks a
  handler on a component that is about to be thrown away. Subscribe where `IsInteractive` is true, and
  unsubscribe in `Dispose`.
- Neither Huddle nor its tests use `[PersistentState]` or `RendererInfo` yet. The first use is a new pattern:
  pin it with a test that fails without it, and add it to `docs/engineering/UXDesignGuidelines.md`'s catalog.

## JS interop: the house way

**Direction (owner, 2026-11-13): modules, not globals.** Today Huddle's JS lives in
`src/Huddle.App/wwwroot/app.js` (plus feature files such as `task-list-resize.js`, all loaded by `<script>` tags in
`App.razor`) as named global objects: `window.huddleSidebar`, `huddleSearch`, `huddleStorage`, `huddleTabs`,
`huddleFeed` and so on. The owner has agreed to move to ES modules (a collocated `Component.razor.js`, or a shared
module under `wwwroot/` for code several components use), loaded with
`IJSRuntime.InvokeAsync<IJSObjectReference>("import", path)` inside a typed wrapper.

- **New JS** is a module with `export`ed functions, never a new `window.huddleX` object or `<script>` tag.
- **Existing globals** move in their own change, one namespace at a time, together with their C# callers and the
  bUnit `JSInterop.Setup` calls that name them. Do not half-move a namespace inside a feature change.
- Until a namespace has moved, a component that uses it keeps calling it as it does today.

Rules that hold either way:

- **No JS before the circuit.** Call JS from `OnAfterRenderAsync(firstRender)` or an event handler, never from
  `OnInitialized*`, `OnParametersSet*` or a constructor. For a parameter change, set a flag in `OnParametersSet`
  and act on it in `OnAfterRenderAsync`.
- **Wrap, don't scatter.** A component does not call `huddleStorage.get` itself; it calls `BrowserStorage`
  (`Components/Shared/BrowserStorage.cs`), which already swallows a gone circuit, blocked storage and prerender.
  Check `UXDesignGuidelines.md`'s helper catalog for a wrapper before adding one. A new wrapper keeps
  the JS identifiers in `const` fields, not string literals at call sites.
- **A closed tab is normal.** The server keeps a disconnected circuit for minutes. Every JS call that can run then,
  including disposal, catches `JSDisconnectedException` (29 files already do). A navigation from a server event
  goes through `ServerNavigator`, not `NavigationManager` (`docs/engineering/rules.md`).
- **Dispose asynchronously.** `@implements IAsyncDisposable`: call the JS cleanup, then dispose the
  `IJSObjectReference`, then the `DotNetObjectReference`. Never a sync `Dispose` that fires a `ValueTask` and
  drops it (`CA2012`).
- **Callbacks into .NET.** `[JSInvokable]` methods must be `public`, or they fail silently at runtime. They marshal
  state changes with `await this.InvokeAsync(this.StateHasChanged)`. The JS side wraps
  `invokeMethodAsync` in `try/catch`, because a lost circuit throws, and names the .NET method in a `const`.
- **Batch round-trips.** Each call crosses SignalR. Two calls that always happen together become one JS function;
  a value produced and consumed in JS never crosses to .NET and back.
- **`ElementReference` over element ids.** Pass `@ref` values, not `document.getElementById` strings.
- **CSS before JS.** Anything a custom property, `data-` attribute or pseudo-class can do stays in CSS.

`agents/BlazorTesting.md` covers asserting JS calls in bUnit (`ctx.JSInterop`), and why a server-rendered HTML
test sees the prerender rather than the interactive page.
