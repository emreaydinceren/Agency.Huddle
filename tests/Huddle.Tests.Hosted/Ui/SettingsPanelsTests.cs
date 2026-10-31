using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.Tests.Css;

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
        MudBunitContext ctx = UiTestHost.Create(services => services.CopyFrom(factory.Services, ServicePresets.Settings));
        ProfileTestServices.AddProfile(ctx.Services, factory);
        SkillsTestServices.AddFromFactory(ctx.Services, factory);
        return ctx;
    }
    /// <summary>
    /// The page has one visible <c>h1</c> reading "Settings" (UX-001, DS "A page"): every page shows its h1, and Settings
    /// has no ancestor, so it has no breadcrumb.
    /// </summary>
    [Fact]
    public async Task Settings_HasOneVisibleH1ReadingSettings()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        var heading = Assert.Single(cut.FindAll("h1"));
        Assert.Equal("Settings", heading.TextContent.Trim());
        Assert.DoesNotContain("visually-hidden", heading.ClassList);
    }

    /// <summary>Every row in the section menu carries an icon, as the Team page rail does (UX-008).</summary>
    [Fact]
    public async Task SectionMenu_EveryRowHasAnIcon()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        var rows = cut.FindAll(".settings-nav a.mud-nav-link");
        Assert.Equal(9, rows.Count);
        Assert.All(rows, static row => Assert.NotEmpty(row.QuerySelectorAll("svg")));
    }

    /// <summary>With no Broken Pack, catalog or Skill and no Skill store issue, the Skills row carries no badge (G2a).</summary>
    [Fact]
    public async Task SectionMenu_NoSkillProblems_NoBadge()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        Assert.Empty(cut.FindAll(".settings-nav-badge"));
    }

    /// <summary>"Reset all to defaults" sits in the Prompts panel's action row beside Save, inside the section and not above the menu (UX-010, G1a).</summary>
    [Fact]
    public async Task Prompts_ResetAllSitsBesideSave_InsideTheSection()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "prompts"));

        var actions = cut.Find(".settings-section .settings-prompts-actions");
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
        string text = CssText.AppCss(CssNewlines.AsIs);

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
