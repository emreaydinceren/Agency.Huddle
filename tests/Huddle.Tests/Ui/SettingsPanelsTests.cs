using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Skills;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for the Settings page frame and the shared panel anatomy (U23): the page header, the rail icons,
/// "Reset all to defaults" and Save living inside the Prompts panel (UX-010, UX-011), and the intro and
/// "stored at" line every panel carries (UX-012, UX-R17).
/// </summary>
public sealed partial class SettingsPanelsTests
{
    /// <summary>A context with the stores the Settings page injects, taken from a live factory.</summary>
    /// <param name="factory">The factory whose services back the stores.</param>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PromptStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AppearanceStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<SkillStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<LibraryRootStore>());
        ProfileTestServices.AddProfile(ctx.Services, factory);
        return ctx;
    }

    /// <summary>The page opens with one <c>h1</c> reading "Settings" (UX-001, DS "A page").</summary>
    [Fact]
    public async Task Settings_HasOneH1ReadingSettings()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        Assert.Equal("Settings", Assert.Single(cut.FindAll("h1")).TextContent.Trim());
    }

    /// <summary>Every tab in the rail carries an icon, as the Team page rail does (UX-008).</summary>
    [Fact]
    public async Task Rail_EveryTabHasAnIcon()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        var tabs = cut.FindAll(".mud-tab");
        Assert.Equal(7, tabs.Count);
        Assert.All(tabs, tab => Assert.NotEmpty(tab.QuerySelectorAll("svg")));
    }

    /// <summary>"Reset all to defaults" sits in the Prompts panel's action row beside Save, inside the tab content and not above the rail (UX-010, G1a).</summary>
    [Fact]
    public async Task Prompts_ResetAllSitsBesideSave_InsideTheTabs()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "prompts"));

        var actions = cut.Find(".mud-tabs .settings-prompts-actions");
        Assert.Single(actions.QuerySelectorAll("button[type='submit']"));
        Assert.Single(actions.QuerySelectorAll("button"), button => button.TextContent.Contains("Reset all to defaults", StringComparison.Ordinal));
    }

    /// <summary>Only Prompts has Reset all: the other tabs do not render it (UX-010).</summary>
    [Theory]
    [InlineData("/settings/appearance")]
    [InlineData("/settings/skills")]
    [InlineData("/settings/library")]
    public async Task OtherTabs_DoNotShowResetAll(string route)
    {
        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        string html = await client.GetStringAsync(route, Xunit.TestContext.Current.CancellationToken);

        Assert.DoesNotContain("Reset all to defaults", html, StringComparison.Ordinal);
    }

    /// <summary>The Prompts action row is sticky, so Save stays in reach of a long form (UX-011, G3a). A source assertion: no test runs layout.</summary>
    [Fact]
    public void AppCss_PromptsActionRowIsSticky()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        Assert.Matches(StickyActionRow(), text);
    }

    /// <summary>Every panel opens with exactly one settings intro and carries the expected number of "stored at" lines (UX-012, UX-R17).</summary>
    [Theory]
    [InlineData("/settings/prompts", 1)]
    [InlineData("/settings/appearance", 1)]
    [InlineData("/settings/personas", 2)]
    [InlineData("/settings/skills", 1)]
    [InlineData("/settings/teamtypes", 1)]
    [InlineData("/settings/library", 1)]
    public async Task EveryPanel_HasOneIntroAndItsStoredAtLines(string route, int storedAtLines)
    {
        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        string html = await client.GetStringAsync(route, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(1, Regex.Count(html, "class=\"settings-intro\""));
        Assert.Equal(storedAtLines, Regex.Count(html, "class=\"settings-stored-at\""));
    }

    [GeneratedRegex(@"\.settings-prompts-actions\s*\{[^}]*position:\s*sticky", RegexOptions.CultureInvariant)]
    private static partial Regex StickyActionRow();
}
