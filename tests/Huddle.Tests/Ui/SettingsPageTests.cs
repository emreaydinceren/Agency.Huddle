using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Hooks;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for the Settings page's tab rail and its Hooks panel. Split two ways, per
/// <c>docs/agencyteam/testing.md</c>: routing, HTTP status, the hooks/appearance file paths and
/// <c>FakeModelCatalog.ProbeCount</c> are facts about the server, so they stay on a plain HTTP GET
/// against <see cref="TeamWebApplicationFactory"/>; everything that asserts on the tab rail, the Save
/// button or the Reset-all button now renders <see cref="Settings"/> directly through
/// <see cref="MudBunitContext"/>, because the MudBlazor migration turned all three into components
/// whose actual disabled-state markup a raw-HTML tag slice can no longer be trusted to parse.
/// </summary>
public sealed class SettingsPageTests
{
    /// <summary>Renders the real <see cref="Settings"/> page against a <see cref="TeamWebApplicationFactory"/>'s live <see cref="HookStore"/> and <see cref="AppearanceStore"/>, the same pattern <c>TeammatesPageTests</c> uses for <c>Teammates</c>.</summary>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<HookStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AppearanceStore>());
        return ctx;
    }

    /// <summary>MudTabs renders both tab labels and, on the default (Hooks) panel, every group heading the Hooks tab shows.</summary>
    [Fact]
    public async Task SettingsPage_Renders_TabRailAndGroupHeadings()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        Assert.Contains("mud-tabs", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Hooks<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Appearance<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">System prompt<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Turn<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Get help<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Tool descriptions<", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The default (Hooks) panel renders the Hooks panel's MudPaper-wrapped groups.</summary>
    [Fact]
    public async Task SettingsHooksPage_Renders_HooksPanel()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        Assert.NotEmpty(cut.FindAll(".mud-paper"));
        Assert.Contains(">System prompt<", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>An unrecognised {Tab} value falls back to the Hooks panel rather than 404ing or throwing - a fact about routing, so it stays on HTTP.</summary>
    [Fact]
    public async Task SettingsPage_UnknownTab_FallsBackToHooksPanel()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/settings/nonsense", ct);
        var html = await response.Content.ReadAsStringAsync(ct);

        response.EnsureSuccessStatusCode();
        Assert.Contains(">System prompt<", html, StringComparison.Ordinal);
    }

    /// <summary>Every one of the catalog's hook labels reaches the rendered page - unaffected by the MudBlazor conversion, so it stays on HTTP.</summary>
    [Fact]
    public async Task SettingsHooksPage_ListsEveryHookLabel()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/hooks", ct);

        foreach (var definition in HookCatalog.All)
        {
            Assert.Contains(definition.Label, html, StringComparison.Ordinal);
        }
    }

    /// <summary>Loading the Settings page must never spawn an adapter process - a fact about the server, so it stays on HTTP.</summary>
    [Fact]
    public async Task SettingsPage_DoesNotProbeForModelsOnAPlainLoad()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/settings", ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal(0, factory.FakeModelCatalog.ProbeCount);
    }

    /// <summary>With nothing pending, the Save button renders disabled - there is nothing yet to commit.</summary>
    [Fact]
    public async Task SettingsHooksPage_WithNothingPending_SaveButtonIsDisabled()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        var save = cut.Find("button[type='submit']");
        Assert.True(save.HasAttribute("disabled"));
    }

    /// <summary>
    /// The page tells a user exactly where <c>hooks.json</c> lives - matched against the factory's
    /// own configured path (see <see cref="TeamWebApplicationFactory.HooksJsonPath"/>) rather than a
    /// hardcoded guess, since <c>Team:DataDir</c> is redirected to a fresh temp directory per factory.
    /// This is a fact about the server's configuration, so it stays on HTTP.
    /// </summary>
    [Fact]
    public async Task SettingsHooksPage_ShowsTheOverrideFilePath()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/hooks", ct);

        Assert.Contains(factory.HooksJsonPath, html, StringComparison.Ordinal);
    }

    /// <summary>With no override configured, the "Reset all to defaults" button renders disabled.</summary>
    [Fact]
    public async Task SettingsHooksPage_WithNothingModified_ResetAllButtonIsDisabled()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        var buttons = cut.FindAll("button");
        var resetAll = Assert.Single(buttons, button => button.TextContent.Contains("Reset all to defaults", StringComparison.Ordinal));
        Assert.True(resetAll.HasAttribute("disabled"));
    }

    /// <summary>The sidebar link to Settings appears on every page, alongside the Teammates link.</summary>
    [Fact]
    public async Task Sidebar_LinksToSettingsPage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains("href=\"/settings\"", html, StringComparison.Ordinal);
    }

    /// <summary>The tab rail includes an Appearance tab alongside Hooks.</summary>
    [Fact]
    public async Task SettingsPage_Renders_AppearanceTabInTheRail()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        Assert.Contains(">Appearance<", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Appearance tab shows a Theme select and an Appearance (dark-mode) select, each already
    /// showing its resolved current value - the catalog's default theme's label and "System" - and
    /// renders none of the Hooks tab's own content. Stays on HTTP for that half: it is a fact about
    /// the prerender, unaffected by this stage's conversion (<c>Appearance.razor</c> is out of scope).
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_RendersTheSelectedThemeAndDarkModeLabels()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        Assert.Contains("aria-label=\"Theme\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Appearance\"", html, StringComparison.Ordinal);
        Assert.Contains($">{ThemeCatalog.BuiltIn[0].Label}<", html, StringComparison.Ordinal);
        Assert.Contains(">System<", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The other half of <see cref="SettingsAppearancePage_RendersTheSelectedThemeAndDarkModeLabels"/>,
    /// restored now that <see cref="MudBunitContext.RenderWithPopovers"/> exists: a <c>MudSelect</c>
    /// only paints its options into a popover once opened by a real click, which a plain GET can never
    /// trigger, so the HTTP version of this test could only ever see the two selects' pre-rendered
    /// current values. Opening both here proves every <see cref="ThemeCatalog.BuiltIn"/> label and
    /// every <see cref="DarkModePreference"/> member is actually offered, not just the one each select
    /// happens to start on.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_OffersEveryThemeAndDarkModeOption()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Settings>(0);
            builder.AddComponentParameter(1, nameof(Settings.Tab), "appearance");
            builder.CloseComponent();
        });

        var selects = cut.FindAll("div.mud-input-control");
        Assert.Equal(2, selects.Count);

        foreach (var select in selects)
        {
            await select.MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        }

        foreach (var theme in ThemeCatalog.BuiltIn)
        {
            Assert.Contains(theme.Label, cut.Markup, StringComparison.Ordinal);
        }

        foreach (var preference in Enum.GetValues<DarkModePreference>())
        {
            Assert.Contains(preference.ToString(), cut.Markup, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The paragraph move in T4.1 must not regress: the Appearance tab never shows the Hooks intro's
    /// file-path paragraph, which talked about <c>hooks.json</c> and would make no sense here.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_DoesNotRenderTheHooksIntro()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        Assert.DoesNotContain(factory.HooksJsonPath, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Appearance tab tells a user exactly where <c>appearance.json</c> lives, the same guarantee
    /// <see cref="SettingsHooksPage_ShowsTheOverrideFilePath"/> gives the Hooks tab.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_NamesTheOverrideFile()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        Assert.Contains(factory.AppearanceJsonPath, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Appearance tab credits Visual Studio Code and the ported themes' original authors under
    /// the Theme select - an ordinary prerendered paragraph, so this stays on HTTP rather than bUnit.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_CreditsVisualStudioCode()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        Assert.Contains("bundled with Visual Studio Code", html, StringComparison.Ordinal);
    }
}
