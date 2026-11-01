using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// The bUnit helpers every <c>LibraryDocument</c> test class needs: render the document, stub the
/// <c>library-editor.js</c> module and switch the Read/Edit/Split mode.
/// </summary>
internal static class LibraryDocumentTestSupport
{
    private const string EditorModulePath = "./library-editor.js";

    /// <summary>Renders a <see cref="LibraryDocument"/> for <paramref name="path"/> inside the popover provider.</summary>
    /// <param name="ctx">The test context to render in.</param>
    /// <param name="path">The document to show.</param>
    /// <param name="scope">The optional Scope the document is shown under.</param>
    /// <param name="onNavigate">The optional navigate callback.</param>
    /// <param name="onOpenInLibrary">The optional open-in-Library callback.</param>
    /// <param name="onAction">The optional tree-action callback.</param>
    /// <param name="onClose">The optional close callback; only passed to the component when it has a delegate.</param>
    /// <param name="tabPage">When set, the document is rendered inside a cascading <see cref="TabPageContext"/> carrying this value.</param>
    /// <returns>The rendered container.</returns>
    public static IRenderedComponent<ContainerFragment> RenderDoc(
        MudBunitContext ctx,
        LibraryPath path,
        LibraryPath? scope = null,
        EventCallback<LibraryPath> onNavigate = default,
        EventCallback<LibraryPath> onOpenInLibrary = default,
        EventCallback<LibraryTreeAction> onAction = default,
        EventCallback onClose = default,
        TabPageContext? tabPage = null) => ctx.RenderWithPopovers(builder =>
    {
        if (tabPage is null)
        {
            AddDocument(builder, path, scope, onNavigate, onOpenInLibrary, onAction, onClose);
            return;
        }

        builder.OpenComponent<CascadingValue<TabPageContext>>(0);
        builder.AddAttribute(1, nameof(CascadingValue<TabPageContext>.Value), tabPage);
        builder.AddAttribute(2, nameof(CascadingValue<TabPageContext>.ChildContent), (RenderFragment)(content =>
            AddDocument(content, path, scope, onNavigate, onOpenInLibrary, onAction, onClose)));
        builder.CloseComponent();
    });

    /// <summary>Stubs the <c>library-editor.js</c> module and its <c>create</c> handle, with <c>getText</c> defaulting to <paramref name="text"/>.</summary>
    /// <param name="ctx">The test context whose JS interop is set up.</param>
    /// <param name="text">What <c>getText</c> returns.</param>
    /// <returns>The module and handle stubs, for callers that set up more calls on them.</returns>
    public static (BunitJSModuleInterop Module, BunitJSModuleInterop Handle) SetupEditorModule(MudBunitContext ctx, string text)
    {
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule(EditorModulePath);
        BunitJSModuleInterop handle = module.SetupModule("create", _ => true);
        handle.Setup<string>("getText", _ => true).SetResult(text);
        handle.SetupVoid("setText", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return (module, handle);
    }

    /// <summary>Switches the document's Read/Edit/Split mode through the toggle group's <c>ValueChanged</c>.</summary>
    /// <param name="cut">The rendered document.</param>
    /// <param name="mode">The mode to switch to.</param>
    /// <returns>A task that completes once the toggle group has handled the change.</returns>
    public static async Task SwitchModeAsync(IRenderedComponent<ContainerFragment> cut, LibraryMode mode)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudToggleGroup<LibraryMode>>()));
        MudToggleGroup<LibraryMode> toggle = cut.FindComponent<MudToggleGroup<LibraryMode>>().Instance;
        await cut.InvokeAsync(() => toggle.ValueChanged.InvokeAsync(mode));
    }

    /// <summary>Emits one <see cref="LibraryDocument"/> with the given parameters.</summary>
    private static void AddDocument(
        RenderTreeBuilder builder,
        LibraryPath path,
        LibraryPath? scope,
        EventCallback<LibraryPath> onNavigate,
        EventCallback<LibraryPath> onOpenInLibrary,
        EventCallback<LibraryTreeAction> onAction,
        EventCallback onClose)
    {
        builder.OpenComponent<LibraryDocument>(0);
        builder.AddAttribute(1, nameof(LibraryDocument.Path), path);
        builder.AddAttribute(2, nameof(LibraryDocument.Scope), scope);
        builder.AddAttribute(3, nameof(LibraryDocument.OnNavigate), onNavigate);
        builder.AddAttribute(4, nameof(LibraryDocument.OnOpenInLibrary), onOpenInLibrary);
        builder.AddAttribute(5, nameof(LibraryDocument.OnAction), onAction);
        if (onClose.HasDelegate)
        {
            builder.AddAttribute(6, nameof(LibraryDocument.OnClose), onClose);
        }

        builder.CloseComponent();
    }
}
