using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// A <see cref="BunitContext"/> pre-wired for MudBlazor components, shared by every bUnit suite in
/// this project that renders one. Three things are needed to make a MudBlazor component behave under
/// bUnit rather than throw or render blank, and this class exists so the four migration stages that
/// need it (this one and the three still to come) do not each re-derive them:
/// <list type="number">
/// <item>
/// <description>
/// <c>MudBlazor.Services.ServiceCollectionExtensions.AddMudServices</c> registers
/// <c>IPopoverService</c>, <c>IDialogService</c>, <c>ISnackbar</c>, <c>IKeyInterceptorService</c>
/// and friends. Without these, resolving almost any MudBlazor component throws at construction -
/// they are constructor-injected, not optional.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="Bunit.BunitContext.JSInterop"/> defaults to <see cref="JSRuntimeMode.Strict"/>, which
/// throws on any unconfigured <c>IJSRuntime</c> call. MudBlazor calls JavaScript on nearly every
/// render (resize observers, popover positioning, ripple effects, key interception) purely as
/// progressive enhancement - the DOM it produces is already correct without it. Setting
/// <see cref="JSRuntimeMode.Loose"/> lets every one of those calls no-op instead of failing a test
/// that has nothing to do with JavaScript.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="RenderWithPopovers"/> renders a <see cref="MudPopoverProvider"/> as a sibling of the
/// content under test, both inside one synthetic root <c>bUnit</c> renders in a single pass. This
/// matters because MudBlazor components that show a popover (<c>MudSelect</c>, <c>MudMenu</c>,
/// <c>MudAutocomplete</c>) do not render their open content inline - they hand it to whatever
/// <see cref="MudPopoverProvider"/> is registered, which paints it as a *sibling* of the component
/// that opened it, not a descendant. <see cref="Bunit.BunitContext.Render{TComponent}(System.Action{Bunit.ComponentParameterCollectionBuilder{TComponent}})"/>
/// alone only ever sees the one component's own subtree, so a popover opened that way is invisible
/// to <c>Find</c>/<c>FindAll</c> even though bUnit rendered it. Wrapping both in one
/// <see cref="RenderFragment"/> and rendering that (the non-generic
/// <see cref="Bunit.BunitContext.Render(RenderFragment)"/> overload, which wraps its argument in a
/// single container component and returns *that* as the rendered fragment) puts the popover and the
/// component that owns it in the same subtree, so a query against the returned fragment sees both.
/// A plain component with no popover (this stage's <c>HooksPanel</c>, <c>ResetAllControl</c>,
/// <c>Settings</c>) needs none of this and can use the ordinary generic <c>Render&lt;T&gt;</c>
/// directly against this context.
/// </description>
/// </item>
/// </list>
/// </summary>
internal sealed class MudBunitContext : BunitContext
{
    /// <summary>
    /// Creates the context: registers MudBlazor's services and switches bUnit's JSInterop to loose
    /// mode. See the type-level remarks for why each is needed.
    /// </summary>
    public MudBunitContext()
    {
        this.Services.AddMudServices();
        this.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// Renders <paramref name="content"/> alongside a <see cref="MudPopoverProvider"/> in one
    /// synthetic root, so a component under test that opens a popover (<c>MudSelect</c>,
    /// <c>MudMenu</c>, <c>MudAutocomplete</c>) has somewhere to paint it, and the popover's content
    /// is reachable from the returned fragment's own <c>Find</c>/<c>FindAll</c> - see the type-level
    /// remarks for why a plain <c>Render&lt;T&gt;</c> call cannot see it. Use this only for a
    /// component that shows a popover; everything else can call <c>Render&lt;T&gt;</c> directly.
    /// </summary>
    /// <param name="content">A render fragment that opens the component under test, e.g. a lambda building it with <c>RenderTreeBuilder</c> calls.</param>
    /// <returns>The rendered fragment containing both the popover provider and <paramref name="content"/>, ready for <c>Find</c>/<c>FindAll</c>/<c>FindComponent&lt;T&gt;</c>.</returns>
    public IRenderedComponent<ContainerFragment> RenderWithPopovers(RenderFragment content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return this.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.AddContent(1, content);
        });
    }
}
