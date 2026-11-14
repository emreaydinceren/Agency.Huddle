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

**Modules, not globals (owner, 2026-11-13).** Every piece of the app's JS is an ES module in
`src/Huddle.App/wwwroot/`; there is no `app.js` and no `<script>` tag for app code. Shared modules (`composer.js`,
`scroll.js`, `storage.js`, `clipboard.js` and the rest) each have a `JsModule` instance
(`Components/Shared/JsModule.cs`), which imports them once per circuit and caches the reference; `Circuits/JsModulePreloader` imports them all when
the circuit opens, before the first render. Without that, a first-render call would wait for its import, and a
component disposed meanwhile hands the module a disposed `DotNetObjectReference` or a removed element: on a first
load that killed the circuit. The preload is what closes that window. It waits at most 5 s, so a module whose fetch
stalls past that is imported on first use and the window reopens for it: `BrowserCall`'s module overloads drop a
disposed `DotNetObjectReference`, but a removed element still reaches the module as `null` and throws `JSException`.
A module that one component owns (`library-editor.js`, `theme-lab.js`) may be imported by that
component directly.

```csharp
try
{
    IJSObjectReference feed = await JsModule.Feed.ImportAsync(this.Js, ct);   // cached: one round trip per call
    return await feed.InvokeAsync<bool>("isScrolled", ct, this.root);
}
catch (JSDisconnectedException)
{
    return false;
}
```

- **New JS** is an `export` in a module, never a `window.*` object. A new shared module gets a `JsModule` entry
  (and lands in `JsModule.All`, so it is preloaded and registered in `MudBunitContext`).
- **Keep the call inside your own try/catch.** `BL0016` checks the `IJSObjectReference` call at the call site, which
  is why `JsModule` hands out the reference instead of wrapping the call. `BrowserCall`'s module overloads do the
  catch for the "gone circuit is nothing to do" case.
- **Never dispose the reference** `ImportAsync` returns: every caller on the circuit shares it.
- **bUnit:** `ctx.Module(JsModule.X).Setup<T>("fn")`, never a second `SetupModule` for the same path.

Rules that hold either way:

- **No JS before the circuit.** Call JS from `OnAfterRenderAsync(firstRender)` or an event handler, never from
  `OnInitialized*`, `OnParametersSet*` or a constructor. For a parameter change, set a flag in `OnParametersSet`
  and act on it in `OnAfterRenderAsync`.
- **Wrap, don't scatter.** A component does not call `storage.js` itself; it calls `BrowserStorage`
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

`agents/BlazorTesting.md` covers asserting JS calls in bUnit (`ctx.Module(...)`), and why a server-rendered HTML
test sees the prerender rather than the interactive page.
