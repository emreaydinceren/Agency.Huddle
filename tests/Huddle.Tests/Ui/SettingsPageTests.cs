using System.Text.Encodings.Web;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for the Settings page's tab rail and its Prompts panel. Split two ways, per
/// <c>docs/agencyteam/testing.md</c>: routing, HTTP status, the prompts/appearance file paths and
/// <c>FakeModelCatalog.ProbeCount</c> are facts about the server, so they stay on a plain HTTP GET
/// against <see cref="TeamWebApplicationFactory"/>; everything that asserts on the tab rail, the Save
/// button or the Reset-all button now renders <see cref="Settings"/> directly through
/// <see cref="MudBunitContext"/>, because the MudBlazor migration turned all three into components
/// whose actual disabled-state markup a raw-HTML tag slice can no longer be trusted to parse.
/// </summary>
public sealed class SettingsPageTests
{
    /// <summary>Renders the real <see cref="Settings"/> page against a <see cref="TeamWebApplicationFactory"/>'s live <see cref="PromptStore"/> and <see cref="AppearanceStore"/>, the same pattern <c>TeammatesPageTests</c> uses for <c>Teammates</c>.</summary>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PromptStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AppearanceStore>());
        return ctx;
    }

    /// <summary>MudTabs renders both tab labels and, on the default (Prompts) panel, every group heading the Prompts tab shows.</summary>
    [Fact]
    public async Task SettingsPage_Renders_TabRailAndGroupHeadings()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        Assert.Contains("mud-tabs", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Prompts<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Appearance<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">System prompt<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Turn<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Get help<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Tool descriptions<", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The default (Prompts) panel renders the Prompts panel's MudPaper-wrapped groups.</summary>
    [Fact]
    public async Task SettingsPromptsPage_Renders_PromptsPanel()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        Assert.NotEmpty(cut.FindAll(".mud-paper"));
        Assert.Contains(">System prompt<", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>An unrecognised {Tab} value falls back to the Prompts panel rather than 404ing or throwing - a fact about routing, so it stays on HTTP.</summary>
    [Fact]
    public async Task SettingsPage_UnknownTab_FallsBackToPromptsPanel()
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

    /// <summary>Every one of the catalog's prompt labels reaches the rendered page - unaffected by the MudBlazor conversion, so it stays on HTTP.</summary>
    [Fact]
    public async Task SettingsPromptsPage_ListsEveryPromptLabel()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/prompts", ct);

        foreach (var definition in PromptCatalog.All)
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
    public async Task SettingsPromptsPage_WithNothingPending_SaveButtonIsDisabled()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        var save = cut.Find("button[type='submit']");
        Assert.True(save.HasAttribute("disabled"));
    }

    /// <summary>
    /// The page tells a user exactly where <c>prompts.json</c> lives - matched against the factory's
    /// own configured path (see <see cref="TeamWebApplicationFactory.PromptsJsonPath"/>) rather than a
    /// hardcoded guess, since <c>Team:DataDir</c> is redirected to a fresh temp directory per factory.
    /// This is a fact about the server's configuration, so it stays on HTTP.
    /// </summary>
    [Fact]
    public async Task SettingsPromptsPage_ShowsTheOverrideFilePath()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/prompts", ct);

        Assert.Contains(factory.PromptsJsonPath, html, StringComparison.Ordinal);
    }

    /// <summary>With no override configured, the "Reset all to defaults" button renders disabled.</summary>
    [Fact]
    public async Task SettingsPromptsPage_WithNothingModified_ResetAllButtonIsDisabled()
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

    /// <summary>The tab rail includes an Appearance tab alongside Prompts.</summary>
    [Fact]
    public async Task SettingsPage_Renders_AppearanceTabInTheRail()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        Assert.Contains(">Appearance<", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Appearance tab shows one labelled theme picker - not two controls - carrying the catalog's
    /// default theme's label. The second control it used to show, a light/dark preference, is gone:
    /// a theme now carries its own mode, so there is nothing left for a second control to contradict
    /// (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>). Stays on HTTP: it is a fact about
    /// the prerender.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_RendersOneThemePickerAndNoDarkModeControl()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        Assert.Contains("aria-label=\"Theme\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Appearance\"", html, StringComparison.Ordinal);
        Assert.Contains($">{HtmlEncoder.Default.Encode(ThemeCatalog.BuiltIn[0].Label)}<", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every theme in the catalog is offered, under its group's heading. No click is simulated to get
    /// there: the picker is a <c>MudList</c>, so every option is in the markup from the first render -
    /// which is the practical reason to prefer it over the <c>MudSelect</c> it replaced, whose options
    /// only reached a popover after a real <c>MouseDown</c>. Labels are compared through
    /// <see cref="HtmlEncoder.Default"/> because that is the encoder the response was written with,
    /// and it escapes more than the five XML entities - "Dark+" reaches the browser as
    /// <c>Dark&amp;#x2B;</c>.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_OffersEveryThemeUnderItsGroupHeading()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        foreach (var theme in ThemeCatalog.BuiltIn)
        {
            Assert.Contains(HtmlEncoder.Default.Encode(theme.Label), html, StringComparison.Ordinal);
        }

        foreach (var grouping in ThemeCatalog.Grouped)
        {
            Assert.Contains($">{grouping.Heading}<", html, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The paragraph move in T4.1 must not regress: the Appearance tab never shows the Prompts intro's
    /// file-path paragraph, which talked about <c>prompts.json</c> and would make no sense here.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_DoesNotRenderThePromptsIntro()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        Assert.DoesNotContain(factory.PromptsJsonPath, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Appearance tab tells a user exactly where <c>appearance.json</c> lives, the same guarantee
    /// <see cref="SettingsPromptsPage_ShowsTheOverrideFilePath"/> gives the Prompts tab.
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
