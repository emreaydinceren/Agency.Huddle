using System.Runtime.CompilerServices;
using Microsoft.JSInterop;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// One of the app's own ES modules under <c>wwwroot/</c>, and the one way C# calls a function it exports.
/// Each module is imported once per circuit and the reference is reused, so a call costs one round trip, as
/// a call to a global function did. <see cref="PreloadAsync"/> imports them all when the circuit opens
/// (<c>Circuits/JsModulePreloader</c>), before the first render, so a first-render call does not wait for an import while
/// its component may be disposed, and the first Copy click does not wait and lose the browser's user activation.
/// </summary>
/// <remarks>
/// The cache is keyed by the <see cref="IJSRuntime"/>, which Blazor Server creates per circuit (and bUnit per
/// context), and holds it weakly, so the references go with the circuit; the circuit's own teardown releases them on
/// the JavaScript side. A failed or cancelled import is forgotten, so a prerender (no JavaScript yet) or a dropped
/// circuit never leaves a broken entry behind. Exceptions reach the caller exactly as a direct
/// <see cref="IJSRuntime"/> call would raise them: <see cref="JSDisconnectedException"/>, <see cref="JSException"/>, or
/// <see cref="InvalidOperationException"/> while prerendering. Lives under <c>Components/</c>, so no
/// <c>ConfigureAwait(false)</c>.
/// </remarks>
internal sealed class JsModule
{
    /// <summary>The chat Composer: drafts, the reference picker, paste and drop of files (<c>composer.js</c>).</summary>
    internal static readonly JsModule Composer = new("./composer.js");

    /// <summary>Message list and in-page scrolling (<c>scroll.js</c>).</summary>
    internal static readonly JsModule Scroll = new("./scroll.js");

    /// <summary>The Feed list's scroll awareness and chip focus (<c>feed.js</c>).</summary>
    internal static readonly JsModule Feed = new("./feed.js");

    /// <summary>Right sidebar panel collapse and the Outline's heading watch (<c>sidebar.js</c>).</summary>
    internal static readonly JsModule Sidebar = new("./sidebar.js");

    /// <summary>The Tab keyboard shortcuts (<c>tabs.js</c>).</summary>
    internal static readonly JsModule Tabs = new("./tabs.js");

    /// <summary>Global search's keyboard shortcuts and focus restore (<c>search.js</c>).</summary>
    internal static readonly JsModule Search = new("./search.js");

    /// <summary>Guarded <c>localStorage</c> reads and writes (<c>storage.js</c>); call it through <see cref="BrowserStorage"/>.</summary>
    internal static readonly JsModule Storage = new("./storage.js");

    /// <summary>Drag-to-resize for the left and right sidebars (<c>resize.js</c>).</summary>
    internal static readonly JsModule Resize = new("./resize.js");

    /// <summary>Copy to the clipboard (<c>clipboard.js</c>); call it through <see cref="ClipboardCopier"/>.</summary>
    internal static readonly JsModule Clipboard = new("./clipboard.js");

    /// <summary>A Library document's visibility refresh and heading scroll (<c>library-document.js</c>).</summary>
    internal static readonly JsModule LibraryDocument = new("./library-document.js");

    /// <summary>The Avatar Library picker's search-box Escape guard (<c>avatar-library.js</c>).</summary>
    internal static readonly JsModule AvatarLibrary = new("./avatar-library.js");

    /// <summary>Column resizing for the Tasks List (<c>task-list-resize.js</c>).</summary>
    internal static readonly JsModule TaskListResize = new("./task-list-resize.js");

    /// <summary>Per runtime (so per circuit), the import of each module that has been asked for.</summary>
    private static readonly ConditionalWeakTable<IJSRuntime, ImportCache> Caches = new();

    private JsModule(string path) => this.Path = path;

    /// <summary>Gets every module, in the order <see cref="PreloadAsync"/> imports them.</summary>
    internal static IReadOnlyList<JsModule> All { get; } =
        [Clipboard, Storage, Composer, Scroll, Feed, Sidebar, Tabs, Search, Resize, LibraryDocument, AvatarLibrary, TaskListResize];

    /// <summary>Gets the path the module is imported from, relative to the app's base URL (the import map fingerprints it).</summary>
    internal string Path { get; }

    /// <summary>
    /// Imports every module on <paramref name="js"/> so later calls skip the import. A circuit that is already gone, or a
    /// render with no JavaScript yet, has nothing to warm: those failures are swallowed, and the next call imports on demand.
    /// </summary>
    /// <param name="js">The circuit's runtime.</param>
    /// <param name="cancellationToken">Cancels the imports.</param>
    /// <returns>A task that completes when every import has finished or failed.</returns>
    internal static async Task PreloadAsync(IJSRuntime js, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(js);

        // Started together and awaited together, so warming costs one round trip, not one per module.
        Task[] imports = [.. All.Select(module => WarmAsync(module, js, cancellationToken))];
        await Task.WhenAll(imports);
    }

    /// <summary>Imports one module for <see cref="PreloadAsync"/>, swallowing the failures a warm-up may meet.</summary>
    private static async Task WarmAsync(JsModule module, IJSRuntime js, CancellationToken cancellationToken)
    {
        try
        {
            _ = await module.ImportAsync(js, cancellationToken);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or InvalidOperationException or OperationCanceledException)
        {
            // Warming is an optimisation: a failed import was forgotten, and the first real call imports again.
        }
    }

    /// <summary>
    /// The module's reference on <paramref name="js"/>, imported on first use and reused after. Call its exports inside
    /// the caller's own try/catch, as for any interop call (<c>BL0016</c> checks that there, not here), or use the
    /// <see cref="BrowserCall"/> overloads that take a module.
    /// </summary>
    /// <param name="js">The circuit's runtime.</param>
    /// <param name="cancellationToken">Stops this caller waiting. It does not cancel the shared import, which other callers may be waiting on.</param>
    /// <returns>The module reference. Do not dispose it: it is shared by every caller on the circuit.</returns>
    internal async ValueTask<IJSObjectReference> ImportAsync(IJSRuntime js, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        ImportCache cache = Caches.GetValue(js, static _ => new ImportCache());
        Task<IJSObjectReference>? import = null;
        try
        {
            if (!cache.TryGet(this.Path, out import))
            {
                // The import is shared by every caller on the circuit, so it is not tied to this caller's token: one
                // caller being disposed must not fail another's Copy click. The runtime's default interop timeout bounds it.
#pragma warning disable CA2016 // Deliberately not forwarded: the import outlives this caller (see above); each caller waits with WaitAsync below.
                Task<IJSObjectReference> started = js.InvokeAsync<IJSObjectReference>("import", this.Path).AsTask();
#pragma warning restore CA2016
                import = cache.Keep(this.Path, started);
                if (!ReferenceEquals(import, started))
                {
                    // Lost a rare race to another caller: nobody awaits this one, so observe a fault rather than leave it unobserved.
                    _ = started.ContinueWith(static task => task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                }
            }

            return await import.WaitAsync(cancellationToken);
        }
        catch (Exception) when (import is { IsCompleted: true, IsCompletedSuccessfully: false })
        {
            // The import itself failed or was cancelled, whatever the exception: forget it so the next call imports again.
            // A caller that only stopped waiting (its own token) leaves the shared import running and cached.
            cache.Forget(this.Path, import);
            throw;
        }
    }

    /// <inheritdoc />
    public override string ToString() => this.Path;

    /// <summary>The imports started on one runtime, by module path.</summary>
    private sealed class ImportCache
    {
        private readonly Lock gate = new();
        private readonly Dictionary<string, Task<IJSObjectReference>> imports = new(StringComparer.Ordinal);

        /// <summary>Finds the import already started for <paramref name="path"/>.</summary>
        internal bool TryGet(string path, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Task<IJSObjectReference>? import)
        {
            lock (this.gate)
            {
                return this.imports.TryGetValue(path, out import);
            }
        }

        /// <summary>
        /// Keeps <paramref name="started"/> for <paramref name="path"/> unless another caller kept one first, and returns
        /// the import that is kept. A circuit renders on one thread, so the race this settles is rare; the losing import
        /// still resolves to the same module, which the browser loaded once.
        /// </summary>
        internal Task<IJSObjectReference> Keep(string path, Task<IJSObjectReference> started)
        {
            lock (this.gate)
            {
                if (this.imports.TryGetValue(path, out Task<IJSObjectReference>? kept))
                {
                    return kept;
                }

                this.imports[path] = started;
                return started;
            }
        }

        /// <summary>Drops <paramref name="failed"/> so the next call imports again; a newer import for the path is kept.</summary>
        internal void Forget(string path, Task<IJSObjectReference> failed)
        {
            lock (this.gate)
            {
                if (this.imports.TryGetValue(path, out Task<IJSObjectReference>? current) && ReferenceEquals(current, failed))
                {
                    _ = this.imports.Remove(path);
                }
            }
        }
    }
}
