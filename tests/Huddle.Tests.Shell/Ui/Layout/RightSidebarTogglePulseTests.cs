using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.Tests.Css;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>
/// Pins the Right sidebar toggle's one aura pulse: while the sidebar is closed, a panel becoming available for the active page starts one
/// pulse (<c>.is-pulsing</c>); opening the sidebar clears it; and nothing pulses on first render, for a panel already seen, or while the sidebar is open.
/// </summary>
public sealed class RightSidebarTogglePulseTests
{
    private const string Pulsing = ".right-sidebar-toggle-aura.is-pulsing";

    private static readonly SidebarPanel RoomPanel = new("pulse-room", "Room panel", "icon", 10, static subject => subject is RoomSubject, typeof(Agency.Huddle.App.Components.Sidebar.MembersPanel));

    /// <summary>The toggle with a catalog that offers <see cref="RoomPanel"/> for a Room, and one open Tab.</summary>
    private static MudBunitContext NewContext(out Tab tab)
    {
        MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton(new SidebarPanelCatalog([RoomPanel], TestOptions.Create()))).WithTabs();
        tab = ctx.Services.GetRequiredService<TabService>().Open("/rooms/ab12");
        return ctx;
    }

    /// <summary>Publishes a Subject for the Tab, as a page's <c>PageSubjectSource</c> does, on the renderer's thread.</summary>
    private static Task PublishAsync(IRenderedComponent<RightSidebarToggle> toggle, MudBunitContext ctx, Tab tab, PageSubject subject, long handle = 1) =>
        toggle.InvokeAsync(() => ctx.Services.GetRequiredService<RightSidebarService>().Set(handle, tab.Id, subject));

    /// <summary>Nothing pulses when the toggle first appears, even on a page that already has panels.</summary>
    [Fact]
    public async Task FirstRender_DoesNotPulse()
    {
        await using MudBunitContext ctx = NewContext(out Tab tab);
        ctx.Services.GetRequiredService<RightSidebarService>().Set(1, tab.Id, new RoomSubject("ab12"));

        IRenderedComponent<RightSidebarToggle> toggle = ctx.Render<RightSidebarToggle>();

        Assert.Empty(toggle.FindAll(Pulsing));
    }

    /// <summary>A panel becoming available while the sidebar is closed starts one pulse.</summary>
    [Fact]
    public async Task PanelBecomesAvailable_WhileClosed_Pulses()
    {
        await using MudBunitContext ctx = NewContext(out Tab tab);
        IRenderedComponent<RightSidebarToggle> toggle = ctx.Render<RightSidebarToggle>();
        Assert.Empty(toggle.FindAll(Pulsing));

        await PublishAsync(toggle, ctx, tab, new RoomSubject("ab12"));

        toggle.WaitForAssertion(() => Assert.Single(toggle.FindAll(Pulsing)));
    }

    /// <summary>Opening the sidebar clears the pulse.</summary>
    [Fact]
    public async Task OpeningTheSidebar_ClearsThePulse()
    {
        await using MudBunitContext ctx = NewContext(out Tab tab);
        IRenderedComponent<RightSidebarToggle> toggle = ctx.Render<RightSidebarToggle>();
        await PublishAsync(toggle, ctx, tab, new RoomSubject("ab12"));
        toggle.WaitForAssertion(() => Assert.Single(toggle.FindAll(Pulsing)));

        await toggle.Find("button.right-sidebar-toggle").ClickAsync(new());

        toggle.WaitForAssertion(() => Assert.Empty(toggle.FindAll(Pulsing)));
    }

    /// <summary>The same panel for another Room is not new, so it does not pulse again.</summary>
    [Fact]
    public async Task SamePanelsForAnotherSubject_DoNotPulseAgain()
    {
        await using MudBunitContext ctx = NewContext(out Tab tab);
        IRenderedComponent<RightSidebarToggle> toggle = ctx.Render<RightSidebarToggle>();
        await PublishAsync(toggle, ctx, tab, new RoomSubject("ab12"));
        toggle.WaitForAssertion(() => Assert.Single(toggle.FindAll(Pulsing)));
        await toggle.Find("button.right-sidebar-toggle").ClickAsync(new());
        await toggle.Find("button.right-sidebar-toggle").ClickAsync(new());
        toggle.WaitForAssertion(() => Assert.Empty(toggle.FindAll(Pulsing)));

        await PublishAsync(toggle, ctx, tab, new RoomSubject("cd34"));

        Assert.Empty(toggle.FindAll(Pulsing));
    }

    /// <summary>With the sidebar already open its panels are visible, so nothing pulses.</summary>
    [Fact]
    public async Task PanelBecomesAvailable_WhileOpen_DoesNotPulse()
    {
        await using MudBunitContext ctx = NewContext(out Tab tab);
        IRenderedComponent<RightSidebarToggle> toggle = ctx.Render<RightSidebarToggle>();
        await toggle.Find("button.right-sidebar-toggle").ClickAsync(new());

        await PublishAsync(toggle, ctx, tab, new RoomSubject("ab12"));

        Assert.Empty(toggle.FindAll(Pulsing));
    }

    /// <summary>The pulse is one iteration and is switched off for people who ask for reduced motion.</summary>
    [Fact]
    public void Css_PulseIsOneIteration_AndOffUnderReducedMotion()
    {
        string css = CssText.AppCss();

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("animation: right-sidebar-aura 1600ms ease-out 1;", CssText.RuleBody(css, ".right-sidebar-toggle-aura.is-pulsing::after"), StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("prefers-reduced-motion: reduce) {\n    .right-sidebar-toggle-aura.is-pulsing::after {\n        animation: none;", css.Replace("\r\n", "\n"), StringComparison.Ordinal);
    }
}
