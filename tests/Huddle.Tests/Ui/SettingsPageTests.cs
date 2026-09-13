using Agency.Huddle.App.Hooks;

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
}
