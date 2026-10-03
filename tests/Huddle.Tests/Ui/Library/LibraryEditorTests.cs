using Agency.Huddle.App.Components.Library;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins <see cref="LibraryEditor"/>'s four-call interop boundary (Spec §6.7): <c>create</c>,
/// <c>getText</c>, <c>setText</c> and <c>dispose</c>, plus the <c>OnDirtyChanged</c> and
/// <c>OnSaveRequested</c> <see cref="DotNetObjectReference{TValue}"/> callbacks. The JS behaviour
/// itself (Mod-s, dirty flips, a lone CR) goes to UAT per corrections-B6 D11 item 17; these tests
/// prove only the .NET side of the boundary. <c>create</c> returns its own
/// <see cref="IJSObjectReference"/> handle (so several editors can share a page), which bUnit
/// models as a nested <see cref="BunitJSModuleInterop"/> via a second <c>SetupModule</c> call.
/// </summary>
public sealed class LibraryEditorTests
{
    private const string ModulePath = "./library-editor.js";

    /// <summary>Renders <see cref="LibraryEditor"/> with the module and its <c>create</c> handle stubbed, returning both for assertions.</summary>
    private static (IRenderedComponent<LibraryEditor> Rendered, BunitJSModuleInterop Module, BunitJSModuleInterop Handle) RenderEditor(
        MudBunitContext context, string text = "hello", bool readOnly = false, string languageId = "markdown")
    {
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("create", _ => true);
        handle.Setup<string>("getText", _ => true).SetResult("");
        handle.SetupVoid("setText", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();

        IRenderedComponent<LibraryEditor> rendered = context.Render<LibraryEditor>(parameters => parameters
            .Add(p => p.Text, text)
            .Add(p => p.ReadOnly, readOnly)
            .Add(p => p.LanguageId, languageId));

        return (rendered, module, handle);
    }

    /// <summary>The component imports the module and calls <c>create</c> with the element, the text, <c>readOnly</c> and the language id.</summary>
    [Fact]
    public void Render_ImportsModuleAndCreates()
    {
        using MudBunitContext context = new();
        (_, BunitJSModuleInterop module, _) = RenderEditor(context, text: "# Title", readOnly: false, languageId: "markdown");

        JSRuntimeInvocation invocation = Assert.Single(module.Invocations, i => i.Identifier == "create");
        Assert.Equal("# Title", invocation.Arguments[1]);
        Assert.False(Assert.IsType<bool>(invocation.Arguments[2]));
        Assert.Equal("markdown", invocation.Arguments[3]);
        Assert.IsType<DotNetObjectReference<LibraryEditor>>(invocation.Arguments[4]);
    }

    /// <summary>A read-only document passes <c>readOnly: true</c> to <c>create</c>.</summary>
    [Fact]
    public void Render_ReadOnly_PassesReadOnlyTrue()
    {
        using MudBunitContext context = new();
        (_, BunitJSModuleInterop module, _) = RenderEditor(context, readOnly: true);

        JSRuntimeInvocation invocation = Assert.Single(module.Invocations, i => i.Identifier == "create");
        Assert.True(Assert.IsType<bool>(invocation.Arguments[2]));
    }

    /// <summary><see cref="LibraryEditor.GetTextAsync"/> calls the handle's <c>getText</c> and returns its result.</summary>
    [Fact]
    public async Task GetTextAsync_CallsGetText()
    {
        using MudBunitContext context = new();
        (IRenderedComponent<LibraryEditor> rendered, _, BunitJSModuleInterop handle) = RenderEditor(context);
        handle.Setup<string>("getText", _ => true).SetResult("current text");

        string text = await rendered.Instance.GetTextAsync();

        Assert.Equal("current text", text);
        Assert.Single(handle.Invocations, i => i.Identifier == "getText");
    }

    /// <summary><see cref="LibraryEditor.SetTextAsync"/> calls the handle's <c>setText</c> with the given text.</summary>
    [Fact]
    public async Task SetTextAsync_CallsSetText()
    {
        using MudBunitContext context = new();
        (IRenderedComponent<LibraryEditor> rendered, _, BunitJSModuleInterop handle) = RenderEditor(context);

        await rendered.Instance.SetTextAsync("new text");

        JSRuntimeInvocation call = Assert.Single(handle.Invocations, i => i.Identifier == "setText");
        Assert.Equal("new text", call.Arguments[0]);
    }

    /// <summary>Calling <see cref="LibraryEditor.SetTextAsync"/> before the editor handle exists (no render has happened yet, so <c>create</c> was never called) throws <see cref="InvalidOperationException"/> rather than reaching for a null handle.</summary>
    [Fact]
    public async Task SetTextAsync_BeforeCreate_ThrowsInvalidOperationException()
    {
        LibraryEditor editor = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.SetTextAsync("x"));
    }

    /// <summary>Invoking the <c>[JSInvokable]</c> <c>OnDirtyChanged</c> directly raises <see cref="LibraryEditor.DirtyChanged"/> once with the same value.</summary>
    [Fact]
    public async Task OnDirtyChanged_RaisesDirtyChanged()
    {
        using MudBunitContext context = new();
        List<bool> raised = [];
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        module.SetupModule("create", _ => true);
        IRenderedComponent<LibraryEditor> rendered = context.Render<LibraryEditor>(parameters => parameters
            .Add(p => p.Text, "hello")
            .Add(p => p.LanguageId, "markdown")
            .Add(p => p.DirtyChanged, dirty => raised.Add(dirty)));

        await rendered.Instance.OnDirtyChanged(true);

        Assert.Equal([true], raised);
    }

    /// <summary>The JS side already reports only flips (Spec §6.7's <c>updateListener</c>), so the component forwards each <c>OnDirtyChanged</c> call rather than re-deriving a dedupe of its own.</summary>
    [Fact]
    public async Task OnDirtyChanged_SameValueTwice_ForwardsBothCalls()
    {
        using MudBunitContext context = new();
        List<bool> raised = [];
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        module.SetupModule("create", _ => true);
        IRenderedComponent<LibraryEditor> rendered = context.Render<LibraryEditor>(parameters => parameters
            .Add(p => p.Text, "hello")
            .Add(p => p.LanguageId, "markdown")
            .Add(p => p.DirtyChanged, dirty => raised.Add(dirty)));

        await rendered.Instance.OnDirtyChanged(true);
        await rendered.Instance.OnDirtyChanged(true);

        Assert.Equal([true, true], raised);
    }

    /// <summary>Invoking the <c>[JSInvokable]</c> <c>OnSaveRequested</c> directly raises <see cref="LibraryEditor.SaveRequested"/> once (the <c>Mod-s</c> keymap's JS-to-.NET callback).</summary>
    [Fact]
    public async Task OnSaveRequested_RaisesSaveRequested()
    {
        using MudBunitContext context = new();
        int raised = 0;
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        module.SetupModule("create", _ => true);
        IRenderedComponent<LibraryEditor> rendered = context.Render<LibraryEditor>(parameters => parameters
            .Add(p => p.Text, "hello")
            .Add(p => p.LanguageId, "markdown")
            .Add(p => p.SaveRequested, () => raised++));

        await rendered.Instance.OnSaveRequested();

        Assert.Equal(1, raised);
    }

    /// <summary>Disposing the component calls the handle's <c>dispose</c>.</summary>
    [Fact]
    public async Task Dispose_CallsDispose()
    {
        using MudBunitContext context = new();
        (IRenderedComponent<LibraryEditor> rendered, _, BunitJSModuleInterop handle) = RenderEditor(context);

        await rendered.Instance.DisposeAsync();

        Assert.Single(handle.Invocations, i => i.Identifier == "dispose");
    }

    /// <summary>The module is the first <see cref="IJSObjectReference"/> in this repo (corrections-B6 D11 item 13): a disposed circuit makes the handle's calls throw <see cref="JSDisconnectedException"/>, and <see cref="LibraryEditor.DisposeAsync"/> must not let it propagate.</summary>
    [Fact]
    public async Task DisposeAsync_WhenCircuitGone_DoesNotThrow()
    {
        using MudBunitContext context = new();
        (IRenderedComponent<LibraryEditor> rendered, _, BunitJSModuleInterop handle) = RenderEditor(context);
        handle.SetupVoid("dispose", _ => true).SetException(new JSDisconnectedException("The circuit is gone."));

        Exception? thrown = await Record.ExceptionAsync(() => rendered.Instance.DisposeAsync().AsTask());

        Assert.Null(thrown);
    }

    /// <summary>After <c>create</c>, no further interop happens until a .NET method is explicitly called - text crosses the circuit only on save and on mode switch, never per keystroke (Spec §6.7).</summary>
    [Fact]
    public void Render_NoPerKeystrokeInterop()
    {
        using MudBunitContext context = new();
        (_, BunitJSModuleInterop module, BunitJSModuleInterop handle) = RenderEditor(context);

        Assert.Single(module.Invocations);
        Assert.Empty(handle.Invocations);
    }

    /// <summary>
    /// Reproduces the race behind the production crash (an <c>Unhandled exception in circuit</c>
    /// from <c>JSException: The value 'dispose' is not a function</c>): Microsoft's own
    /// component-disposal guidance is that <see cref="LibraryEditor.DisposeAsync"/> can run while
    /// <c>OnAfterRenderAsync</c>'s own <c>create</c> call is still awaiting a result. The handle that
    /// arrives afterwards must be released on its own - not assigned to the already-disposed
    /// instance, where nothing would ever dispose it.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhileCreateIsPending_ReleasesLateArrivingHandle()
    {
        // bUnit's own JSInterop mock resolves every IJSObjectReference-returning call eagerly (it
        // refuses a deferred Setup<IJSObjectReference> outright), so the one call whose timing this
        // test controls - `create` - is served by a hand-written IJSRuntime instead, replacing
        // bUnit's default registration for this test only.
        DeferredCreateModule module = new();
        using MudBunitContext context = new();
        context.Services.AddSingleton<IJSRuntime>(new SingleModuleJSRuntime(module));

        IRenderedComponent<LibraryEditor> rendered = context.Render<LibraryEditor>(parameters => parameters
            .Add(p => p.Text, "hello")
            .Add(p => p.LanguageId, "markdown"));

        // The component is disposed before its own `create` call has returned.
        Exception? disposedWhilePending = await Record.ExceptionAsync(() => rendered.Instance.DisposeAsync().AsTask());
        Assert.Null(disposedWhilePending);

        // `create` now resolves, handing the component a handle after it was already disposed. The
        // continuation that reacts to it runs on the renderer's own dispatcher, not this thread, so
        // the test awaits the handle's own dispose signal rather than assuming it has run by now.
        RecordingJSObjectReference lateHandle = new();
        module.CompleteCreate(lateHandle);
        await lateHandle.Disposed.WaitAsync(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(1, lateHandle.DisposeInvocationCount);
    }

    /// <summary>A minimal <see cref="IJSRuntime"/> whose only handled call is <c>import</c>, returning the given module.</summary>
    private sealed class SingleModuleJSRuntime(IJSObjectReference module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            this.InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            string.Equals(identifier, "import", StringComparison.Ordinal)
                ? new ValueTask<TValue>((TValue)module)
                : throw new NotSupportedException($"Unexpected interop call '{identifier}'.");
    }

    /// <summary>
    /// Stands in for the imported <c>library-editor.js</c> module. Its <c>create</c> call does not
    /// complete until <see cref="CompleteCreate"/> is called, so a test can dispose the component
    /// while that call is still in flight.
    /// </summary>
    private sealed class DeferredCreateModule : IJSObjectReference
    {
        private readonly TaskCompletionSource<IJSObjectReference> createResult = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void CompleteCreate(IJSObjectReference handle) => this.createResult.SetResult(handle);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            this.InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (!string.Equals(identifier, "create", StringComparison.Ordinal))
            {
                throw new NotSupportedException($"Unexpected interop call '{identifier}'.");
            }

            IJSObjectReference handle = await this.createResult.Task.WaitAsync(cancellationToken);
            return (TValue)handle;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A minimal <see cref="IJSObjectReference"/> the test completes and inspects directly, standing in for the CodeMirror handle a late-arriving <c>create</c> call would hand back.</summary>
    private sealed class RecordingJSObjectReference : IJSObjectReference
    {
        private readonly TaskCompletionSource disposedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisposeInvocationCount { get; private set; }

        /// <summary>Completes once <c>dispose</c> has been invoked, so a test can await the effect of a fire-and-forget continuation instead of racing it.</summary>
        public Task Disposed => this.disposedSignal.Task;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            this.InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (string.Equals(identifier, "dispose", StringComparison.Ordinal))
            {
                this.DisposeInvocationCount++;
                this.disposedSignal.TrySetResult();
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// A second <see cref="LibraryEditor.DisposeAsync"/> call - Microsoft's own component-disposal
    /// docs note disposal timing isn't guaranteed exactly-once - must find nothing left to release
    /// rather than re-invoking <c>dispose</c> on a handle the client has already dropped, which is
    /// what actually threw <c>JSException: The value 'dispose' is not a function</c> in production.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_CalledTwice_SecondCallDoesNotReinvokeDispose()
    {
        using MudBunitContext context = new();
        (IRenderedComponent<LibraryEditor> rendered, _, BunitJSModuleInterop handle) = RenderEditor(context);

        await rendered.Instance.DisposeAsync();

        // A second `dispose` call on a handle the client has already released throws client-side -
        // exactly the production symptom - so the mock is set to fail if it is ever reached again.
        handle.SetupVoid("dispose", _ => true).SetException(new JSException("The value 'dispose' is not a function."));
        Exception? thrown = await Record.ExceptionAsync(() => rendered.Instance.DisposeAsync().AsTask());

        Assert.Null(thrown);
        Assert.Single(handle.Invocations, i => i.Identifier == "dispose");
    }

    /// <summary>
    /// The real cause of <c>JSException: The value 'dispose' is not a function</c> (which the two
    /// tests above only ever guarded the edges of): the .NET side asks <c>create</c> for an
    /// <see cref="IJSObjectReference"/>, so Blazor wraps whatever it returns. Returning
    /// <c>DotNet.createJSObjectReference(handle)</c> wrapped it a second time, so the .NET reference
    /// pointed at the <c>{ __jsObjectId }</c> wrapper - which has no <c>getText</c>, <c>setText</c> or
    /// <c>dispose</c> - and navigating away from an open editor killed the circuit. bUnit mocks the
    /// handle, so only the module's own source can pin this; the repo has no JavaScript test runner.
    /// </summary>
    [Fact]
    public void LibraryEditorJs_CreateReturnsTheHandleItself_NotAWrappedReference()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "library-editor.js"));

        // contains-ok: source-fact test, no JS runner - library-editor.js's own text is what's pinned.
        Assert.Contains("return handle;", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - library-editor.js's own text is what's pinned.
        Assert.DoesNotContain("createJSObjectReference(handle)", text, StringComparison.Ordinal);
    }
}
