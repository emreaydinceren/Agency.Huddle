using Agency.Huddle.App.Components.Shared;
using Microsoft.JSInterop;

namespace Agency.Huddle.Tests.Ui.Helpers;

/// <summary>
/// Pins <see cref="JsModule"/>'s per-runtime import cache: a module is imported once per circuit and the reference
/// reused, a failed or cancelled import is forgotten so the next call retries, and <see cref="JsModule.PreloadAsync"/>
/// warms every module without letting a gone circuit fail the layout. Also pins what <see cref="BrowserCall"/>'s module
/// overloads drop as "nobody left to serve".
/// </summary>
public sealed class JsModuleTests
{
    /// <summary>Two imports of the same module on one runtime make one <c>import</c> call and share the reference.</summary>
    [Fact]
    public async Task ImportAsync_SameRuntimeTwice_ImportsOnceAndReusesTheReference()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime js = new();

        IJSObjectReference first = await JsModule.Feed.ImportAsync(js, ct);
        IJSObjectReference second = await JsModule.Feed.ImportAsync(js, ct);

        Assert.Same(first, second);
        Assert.Equal(["./feed.js"], js.Imports);
    }

    /// <summary>Each runtime (each circuit) gets its own import, so one circuit never hands another its reference.</summary>
    [Fact]
    public async Task ImportAsync_TwoRuntimes_ImportsOnEach()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime one = new();
        FakeModuleRuntime two = new();

        IJSObjectReference fromOne = await JsModule.Clipboard.ImportAsync(one, ct);
        IJSObjectReference fromTwo = await JsModule.Clipboard.ImportAsync(two, ct);

        Assert.NotSame(fromOne, fromTwo);
        Assert.Single(one.Imports);
        Assert.Single(two.Imports);
    }

    /// <summary>An import that fails because the circuit dropped reaches the caller, and is forgotten: the next call imports again.</summary>
    [Fact]
    public async Task ImportAsync_FirstImportDisconnected_ThrowsThenRetries()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime js = new() { FailNext = new JSDisconnectedException("gone") };

        await Assert.ThrowsAsync<JSDisconnectedException>(() => JsModule.Storage.ImportAsync(js, ct).AsTask());
        IJSObjectReference retried = await JsModule.Storage.ImportAsync(js, ct);

        Assert.NotNull(retried);
        Assert.Equal(["./storage.js", "./storage.js"], js.Imports);
    }

    /// <summary>A cancelled import is forgotten too, so a later caller is not handed a cancelled task.</summary>
    [Fact]
    public async Task ImportAsync_FirstImportCancelled_ThrowsThenRetries()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime js = new() { FailNext = new TaskCanceledException() };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => JsModule.Tabs.ImportAsync(js, ct).AsTask());
        _ = await JsModule.Tabs.ImportAsync(js, ct);

        Assert.Equal(["./tabs.js", "./tabs.js"], js.Imports);
    }

    /// <summary>Any failed import is forgotten, not only the interop exceptions: an unexpected fault must not stick to the circuit.</summary>
    [Fact]
    public async Task ImportAsync_FirstImportFailsUnexpectedly_ThrowsThenRetries()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime js = new() { FailNext = new FormatException("bad reply") };

        await Assert.ThrowsAsync<FormatException>(() => JsModule.Search.ImportAsync(js, ct).AsTask());
        _ = await JsModule.Search.ImportAsync(js, ct);

        Assert.Equal(["./search.js", "./search.js"], js.Imports);
    }

    /// <summary>
    /// One caller giving up (its component was disposed) stops only that caller waiting: another caller on the same
    /// circuit still gets the module from the one shared import, which is kept.
    /// </summary>
    [Fact]
    public async Task ImportAsync_OneCallerCancelsWhilePending_OtherCallerStillGetsTheSharedImport()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime.ImportGate gate = new();
        FakeModuleRuntime js = new() { Gate = gate };
        using CancellationTokenSource disposed = new();

        Task<IJSObjectReference> leaving = JsModule.Clipboard.ImportAsync(js, disposed.Token).AsTask();
        Task<IJSObjectReference> staying = JsModule.Clipboard.ImportAsync(js, ct).AsTask();
        await disposed.CancelAsync();
        // Bounded: if cancellation stopped reaching the caller, this fails with a TimeoutException instead of hanging.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leaving.WaitAsync(TimeSpan.FromSeconds(10), ct));
        gate.Open();
        IJSObjectReference reference = await staying;

        Assert.Same(reference, await JsModule.Clipboard.ImportAsync(js, ct));
        Assert.Equal(["./clipboard.js"], js.Imports);
    }

    /// <summary>Preloading imports every module once, the clipboard first, so a later call costs only its own round trip.</summary>
    [Fact]
    public async Task PreloadAsync_LiveCircuit_ImportsEveryModuleOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime js = new();

        await JsModule.PreloadAsync(js, ct);
        _ = await JsModule.Composer.ImportAsync(js, ct);

        Assert.Equal(JsModule.All.Select(static module => module.Path), js.Imports);
        Assert.Equal("./clipboard.js", js.Imports[0]);
    }

    /// <summary>
    /// A failed import during preloading is swallowed and does not stop the others; the failed one is not cached, so
    /// its first real call imports it again, while the rest are reused.
    /// </summary>
    [Fact]
    public async Task PreloadAsync_OneImportFails_WarmsTheRestAndRetriesOnlyTheFailedOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime js = new() { FailNext = new JSDisconnectedException("gone") };

        await JsModule.PreloadAsync(js, ct);
        _ = await JsModule.Clipboard.ImportAsync(js, ct);
        _ = await JsModule.Storage.ImportAsync(js, ct);

        Assert.Equal(JsModule.All.Count + 1, js.Imports.Count);
        Assert.Equal(2, js.Imports.Count(static path => string.Equals(path, "./clipboard.js", StringComparison.Ordinal)));
        Assert.Single(js.Imports, static path => string.Equals(path, "./storage.js", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every module path is distinct and names a real file in <c>wwwroot/</c>: a mistyped path would only fail in the
    /// browser, on the first call, with nothing on screen to say so.
    /// </summary>
    [Fact]
    public void All_EveryModule_IsADistinctFileInWwwroot()
    {
        Assert.Equal(JsModule.All.Count, JsModule.All.Select(static module => module.Path).Distinct(StringComparer.Ordinal).Count());
        Assert.All(JsModule.All, static module =>
            Assert.True(File.Exists(CssSource.RepoPath("src", "Huddle.App", "wwwroot", module.Path["./".Length..])), $"{module.Path} is not a file in wwwroot/."));
    }

    /// <summary>
    /// A component disposed while its module was first imported has disposed its <see cref="DotNetObjectReference{TValue}"/>
    /// when the call serializes it: the call is dropped, not left to end the circuit (the Composer did on a first load).
    /// </summary>
    [Fact]
    public async Task BrowserCallInvokeVoidAsync_DotNetReferenceDisposedDuringImport_ReturnsFalse()
    {
        FakeModuleRuntime js = new() { CallFailure = DisposedReference() };

        bool ok = await BrowserCall.InvokeVoidAsync(js, JsModule.Composer, "attach", "element");

        Assert.False(ok);
    }

    /// <summary>The value-returning overload drops the same case and returns no value.</summary>
    [Fact]
    public async Task BrowserCallInvokeAsync_DotNetReferenceDisposedDuringImport_ReturnsNotOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime js = new() { CallFailure = DisposedReference() };

        (bool ok, bool value) = await BrowserCall.InvokeAsync<bool>(js, JsModule.Clipboard, "copy", ct, "text");

        Assert.False(ok);
        Assert.False(value);
    }

    /// <summary>Any other disposed object is a real fault, so it still reaches the caller.</summary>
    [Fact]
    public async Task BrowserCallInvokeVoidAsync_OtherObjectDisposed_Propagates()
    {
        FakeModuleRuntime js = new() { CallFailure = new ObjectDisposedException("System.IO.MemoryStream") };

        await Assert.ThrowsAsync<ObjectDisposedException>(() => BrowserCall.InvokeVoidAsync(js, JsModule.Feed, "scrollToTop").AsTask());
    }

    /// <summary>A gone circuit during the call is dropped too, as for a call to a global.</summary>
    [Fact]
    public async Task BrowserCallInvokeVoidAsync_CircuitGone_ReturnsFalse()
    {
        FakeModuleRuntime js = new() { CallFailure = new JSDisconnectedException("gone") };

        bool ok = await BrowserCall.InvokeVoidAsync(js, JsModule.Scroll, "toBottom", "element");

        Assert.False(ok);
    }

    /// <summary>What <c>JSRuntime</c> throws when it serializes a disposed reference: a real one, disposed, named as the runtime names it.</summary>
    private static ObjectDisposedException DisposedReference()
    {
        DotNetObjectReference<object> reference = DotNetObjectReference.Create(new object());
        reference.Dispose();

        return Assert.Throws<ObjectDisposedException>(() => _ = reference.Value);
    }
}
