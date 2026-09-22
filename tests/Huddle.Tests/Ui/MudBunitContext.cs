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
/// <see cref="RenderWithPopovers"/> renders a <see cref="MudPopoverProvider"/> AND a
/// <see cref="MudDialogProvider"/> as siblings of the content under test, all three inside one
/// synthetic root <c>bUnit</c> renders in a single pass. This matters because MudBlazor components
/// that show floating content - <c>MudSelect</c>, <c>MudMenu</c> and <c>MudAutocomplete</c> via the
/// popover provider, and anything shown through <c>IDialogService.ShowAsync</c> via the dialog
/// provider - do not render that content inline: they hand it to whichever provider is registered,
/// which paints it as a *sibling* of the component that opened it, not a descendant.
/// <see cref="Bunit.BunitContext.Render{TComponent}(System.Action{Bunit.ComponentParameterCollectionBuilder{TComponent}})"/>
/// alone only ever sees the one component's own subtree, so a popover or a dialog opened that way is
/// invisible to <c>Find</c>/<c>FindAll</c> even though bUnit rendered it. Wrapping all three in one
/// <see cref="RenderFragment"/> and rendering that (the non-generic
/// <see cref="Bunit.BunitContext.Render(RenderFragment)"/> overload, which wraps its argument in a
/// single container component and returns *that* as the rendered fragment) puts the floating content
/// and the component that owns it in the same subtree, so a query against the returned fragment sees
/// both. Both providers are included together, rather than as two separate methods, because a real
/// page tends to need both at once (a dialog whose own form has a <c>MudSelect</c> is exactly Stage
/// 4's <c>TeammateCard</c>) and there is no cost to registering a provider nothing in a given test
/// happens to use. A plain component with no popover and no dialog (this stage's <c>PromptsPanel</c>,
/// <c>ResetAllControl</c>, <c>Settings</c>) needs none of this and can use the ordinary generic
/// <c>Render&lt;T&gt;</c> directly against this context.
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
    /// Renders <paramref name="content"/> alongside a <see cref="MudPopoverProvider"/> and a
    /// <see cref="MudDialogProvider"/> in one synthetic root, so a component under test that opens a
    /// popover (<c>MudSelect</c>, <c>MudMenu</c>, <c>MudAutocomplete</c>) or a dialog (anything shown
    /// through <c>IDialogService.ShowAsync</c>) has somewhere to paint it, and that content is
    /// reachable from the returned fragment's own <c>Find</c>/<c>FindAll</c> - see the type-level
    /// remarks for why a plain <c>Render&lt;T&gt;</c> call cannot see it. Use this only for a
    /// component that shows a popover or a dialog; everything else can call <c>Render&lt;T&gt;</c>
    /// directly.
    /// </summary>
    /// <param name="content">A render fragment that opens the component under test, e.g. a lambda building it with <c>RenderTreeBuilder</c> calls.</param>
    /// <returns>The rendered fragment containing both providers and <paramref name="content"/>, ready for <c>Find</c>/<c>FindAll</c>/<c>FindComponent&lt;T&gt;</c>.</returns>
    public IRenderedComponent<ContainerFragment> RenderWithPopovers(RenderFragment content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return this.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.AddContent(2, content);
        });
    }
}
