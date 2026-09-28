using Agency.Huddle.App.Components.Library;
using Bunit;
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
        Assert.Equal(false, invocation.Arguments[2]);
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
        Assert.Equal(true, invocation.Arguments[2]);
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
}
