using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Hooks;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Tests for the Settings page's tab rail and its read-only Hooks panel.</summary>
public sealed class SettingsPageTests
{
    /// <summary>A plain GET renders the tab rail and every group heading the Hooks tab shows.</summary>
    [Fact]
    public async Task SettingsPage_Renders_TabRailAndGroupHeadings()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings", ct);

        Assert.Contains("settings-tab-rail", html, StringComparison.Ordinal);
        Assert.Contains(">Hooks<", html, StringComparison.Ordinal);
        Assert.Contains(">System prompt<", html, StringComparison.Ordinal);
        Assert.Contains(">Turn<", html, StringComparison.Ordinal);
        Assert.Contains(">Get help<", html, StringComparison.Ordinal);
        Assert.Contains(">Tool descriptions<", html, StringComparison.Ordinal);
    }

    /// <summary>The explicit "/settings/hooks" route renders the Hooks panel.</summary>
    [Fact]
    public async Task SettingsHooksPage_Renders_HooksPanel()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/hooks", ct);

        Assert.Contains("hooks-group", html, StringComparison.Ordinal);
    }

    /// <summary>An unrecognised {Tab} value falls back to the Hooks panel rather than 404ing or throwing.</summary>
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
        Assert.Contains("hooks-group", html, StringComparison.Ordinal);
    }

    /// <summary>Every one of the catalog's 22 hook labels reaches the rendered page.</summary>
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

    /// <summary>Loading the Settings page must never spawn an adapter process, the same guard <c>TeammatesPageTests</c> gives its own page.</summary>
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/hooks", ct);

        var buttonStart = html.IndexOf("type=\"submit\"", StringComparison.Ordinal);
        Assert.True(buttonStart >= 0, "Could not find the submit button in the rendered page.");
        var tagStart = html.LastIndexOf('<', buttonStart);
        var tagEnd = html.IndexOf('>', buttonStart);
        var tag = html[tagStart..(tagEnd + 1)];

        Assert.Contains("disabled", tag, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page tells a user exactly where <c>hooks.json</c> lives - matched against the factory's
    /// own configured path (see <see cref="TeamWebApplicationFactory.HooksJsonPath"/>) rather than a
    /// hardcoded guess, since <c>Team:DataDir</c> is redirected to a fresh temp directory per factory.
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/hooks", ct);

        var buttonStart = html.IndexOf("Reset all to defaults", StringComparison.Ordinal);
        Assert.True(buttonStart >= 0, "Could not find the Reset all button in the rendered page.");
        var tagStart = html.LastIndexOf("<button", buttonStart, StringComparison.Ordinal);
        var tagEnd = html.IndexOf('>', tagStart);
        var tag = html[tagStart..(tagEnd + 1)];

        Assert.Contains("disabled", tag, StringComparison.Ordinal);
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

    /// <summary>The tab rail includes an Appearance button alongside Hooks.</summary>
    [Fact]
    public async Task SettingsPage_Renders_AppearanceTabInTheRail()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings", ct);

        Assert.Contains(">Appearance<", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Appearance tab shows a Theme select and an Appearance (dark-mode) select, each already
    /// showing its resolved current value - the catalog's default theme's label and "System" - and
    /// renders none of the Hooks tab's own content. <c>MudSelect</c> only renders its list of items
    /// into a popover once opened by a real click, which a plain GET can never trigger - the same
    /// "anything behind a click is absent from that HTML" limitation <c>testing.md</c> documents for
    /// <c>TeammateCard</c>'s own controls - so this cannot also assert that every catalog theme
    /// label or every <see cref="DarkModePreference"/> option appears; only the two selects'
    /// pre-rendered current values can be checked here.
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

        Assert.DoesNotContain("hooks-group", html, StringComparison.Ordinal);
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

}
