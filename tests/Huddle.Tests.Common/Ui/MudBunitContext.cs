using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;

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
/// 4's <c>AgentProfileCard</c>) and there is no cost to registering a provider nothing in a given test
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
    /// Raises bUnit's default wait budget from one second to <see cref="WaitBudget"/>. Every
    /// <c>WaitForAssertion</c> / <c>WaitForState</c> / <c>WaitForElement</c> in this project is already
    /// waiting on the right signal (an async load or a debounced re-render); under the shared CI
    /// desktop's CPU load a one-second budget expires before the first check even runs ("Check count:
    /// 0"). A wait that passes returns on the first render that satisfies it, so the larger budget
    /// costs nothing on a green run and only delays a genuine failure.
    /// </summary>
    static MudBunitContext()
    {
        DefaultWaitTimeout = WaitBudget;
    }

    /// <summary>The wait budget every bUnit wait in this project gets unless a call passes its own.</summary>
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(10);

    private readonly Bunit.TestDoubles.BunitAuthorizationContext authorization;

    /// <summary>
    /// Creates the context: registers MudBlazor's services and switches bUnit's JSInterop to loose
    /// mode. See the type-level remarks for why each is needed.
    /// </summary>
    /// <remarks>Also registers <see cref="RightSidebarService"/> and <see cref="PageCommandBus"/>: any page that publishes a Subject (<c>PageSubjectSource</c>) or handles page commands needs them to render. And <see cref="TeamShortcutStore"/>, which the Team page's header and the Activity bar's shortcuts read.</remarks>
    public MudBunitContext()
    {
        this.Services.AddMudServices();
        this.Services.AddSingleton<RightSidebarService>();
        this.Services.AddSingleton<PageCommandBus>();
        this.Services.AddSingleton<TeamShortcutStore>();

        // The host actions (Agent card Open, folder picker) ask whether remote access is configured. Tests start with it off,
        // as a stock install is; a test of the configured state registers its own answer.
        Agency.Huddle.Tests.Gateway.FakeRemoteAccessState remoteAccess = new(configured: false);
        this.Services.AddSingleton<Agency.Huddle.App.Gateway.IRemoteAccessState>(remoteAccess);
        this.Services.AddSingleton(new Agency.Huddle.App.Gateway.HostActions(remoteAccess));
        this.JSInterop.Mode = JSRuntimeMode.Loose;

        // Settings reads the viewer's scopes from the cascading authentication state (C-13). Every context starts as the paired admin,
        // so a test that is not about access keeps rendering the page as it always did; SignInWithScopes narrows that.
        this.Services.AddCascadingAuthenticationState();
        this.authorization = this.AddAuthorization();
        this.SignInWithScopes("operator.read", "operator.write", "operator.pairing", "operator.admin");

        // The Right sidebar opens by itself unless the viewer last closed it. Tests start from "closed" so each opens it on purpose;
        // a test of the remembered state replaces this answer.
        this.RememberSidebarOpen(false);
    }

    /// <summary>Signs the viewer in as a paired device holding exactly <paramref name="scopes"/> (<c>huddle:scope</c> claims, C-4).</summary>
    /// <param name="scopes">The scopes; none makes the viewer a signed-in device with no scope.</param>
    public void SignInWithScopes(params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        this.authorization.SetAuthorized("test-device");
        this.authorization.SetClaims(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "test-device"),
             .. scopes.Select(scope => new System.Security.Claims.Claim("huddle:scope", scope))]);
    }

    /// <summary>Signs the viewer in as the paired device <c>test-device</c> holding exactly <paramref name="scopes"/>, whose session came in over <paramref name="ingress"/> (<c>huddle:ingress</c>, for example <c>Loopback</c> or <c>Direct</c>).</summary>
    /// <param name="ingress">The ingress kind as <c>IngressKindText</c> writes it.</param>
    /// <param name="scopes">The scopes.</param>
    public void SignInWithIngress(string ingress, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(ingress);
        ArgumentNullException.ThrowIfNull(scopes);

        this.authorization.SetAuthorized("test-device");
        this.authorization.SetClaims(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "test-device"),
             new System.Security.Claims.Claim("huddle:ingress", ingress),
             .. scopes.Select(scope => new System.Security.Claims.Claim("huddle:scope", scope))]);
    }

    /// <summary>Sets what local storage answers when the Right sidebar asks whether the viewer left it open; <see langword="null"/> is nothing remembered.</summary>
    /// <param name="open">The remembered state, or <see langword="null"/> for none.</param>
    public void RememberSidebarOpen(bool? open) =>
        this.JSInterop.Setup<string?>("huddleStorage.get", "shell:rightSidebarOpen").SetResult(open?.ToString().ToLowerInvariant());

    /// <summary>
    /// Disposes every rendered component, as <see cref="BunitContext.DisposeComponentsAsync"/> does, but from
    /// the renderer's dispatcher, so it disposes them even when that dispatcher is busy.
    /// </summary>
    /// <remarks>
    /// bUnit 2.11.3's <c>DisposeComponents</c> posts "detach every root" to the dispatcher and then clears
    /// its root list. When the dispatcher is idle the post runs inline, before the clear. When it is busy
    /// - a SQLite lookup's continuation re-rendering a component, which under full-suite load is often - the
    /// post is queued, the clear runs first, and the queued work then detaches an empty list: nothing is
    /// disposed and the returned task still succeeds. Called on the dispatcher, the post always runs
    /// inline, so the roots are detached before they are forgotten. A test that asserts what disposal
    /// did (an unsubscribe) must use this.
    /// </remarks>
    /// <returns>A task that completes when the components are disposed.</returns>
    public Task DisposeComponentsOnDispatcherAsync() => this.Renderer.Dispatcher.InvokeAsync(this.DisposeComponentsAsync);

    /// <summary>
    /// Copies the JS calls recorded for <paramref name="identifier"/>, reading them on the renderer's
    /// dispatcher.
    /// </summary>
    /// <remarks>
    /// bUnit records a component's JS call into a plain list on the dispatcher thread. A test reading
    /// <c>JSInterop.Invocations</c> from its own thread while the component is still rendering, retrying
    /// or saving can see a stale count or throw "Collection was modified". Read here instead whenever
    /// the component may still be calling JS - inside a wait, a poll, or after work the test did not
    /// await.
    /// </remarks>
    /// <param name="identifier">The JS function's identifier, e.g. <c>huddleStorage.set</c>.</param>
    /// <returns>A snapshot of the calls, oldest first, that later calls do not change.</returns>
    public Task<IReadOnlyList<JSRuntimeInvocation>> InvocationsOnDispatcherAsync(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return this.Renderer.Dispatcher.InvokeAsync<IReadOnlyList<JSRuntimeInvocation>>(() => [.. this.JSInterop.Invocations[identifier]]);
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
