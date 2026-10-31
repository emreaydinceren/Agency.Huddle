using System.Reflection;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.Tests.Waiting;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins <see cref="SkillsPanel"/> (Skill Packs spec §6.9.1, §6.9.2): the three tabs and <c>?view=</c>, repainting on
/// a snapshot raised off the UI thread, unsubscribing, the New badges, and the section badge's count.</summary>
public sealed class SkillsPanelTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>The three tabs render in order Skills, Packs, Sources.</summary>
    [Fact]
    public async Task Tabs_RenderInOrder()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<SkillsPanel> cut = ctx.Render<SkillsPanel>();

        Assert.Equal(["Skills", "Packs", "Sources"], cut.FindAll(".mud-tab").Select(static tab => tab.TextContent.Trim()));
    }

    /// <summary><c>?view=packs</c> selects Packs; an unknown view selects Skills.</summary>
    [Theory]
    [InlineData("packs", "Packs")]
    [InlineData("nonsense", "Skills")]
    [InlineData(null, "Skills")]
    public async Task View_SelectsTheTab(string? view, string expected)
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(view is null ? "/settings/skills" : $"/settings/skills?view={view}");

        IRenderedComponent<SkillsPanel> cut = ctx.Render<SkillsPanel>();

        Assert.Equal(expected, cut.Find(".mud-tab-active").TextContent.Trim());
    }

    /// <summary>A snapshot published on a thread-pool thread repaints the open tab.</summary>
    [Fact]
    public async Task RegistryChanged_OffTheUiThread_Repaints()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<SkillsPanel> cut = ctx.Render<SkillsPanel>();
        Assert.Empty(cut.FindAll("[data-skill='late']"));

        string catalog = this.fixture.Host.Catalog("late", ["late"]);
        this.fixture.Host.SkillsMd(["packs/documents.skillpack.json"], [catalog]);
        await Task.Run(this.fixture.Host.ReadAsync, Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-skill='late']")));
    }

    /// <summary><c>Dispose</c> unsubscribes from all three events.</summary>
    [Fact]
    public async Task Dispose_UnsubscribesAllThree()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        int registry = Subscribers(this.fixture.Host.Registry, "Changed");
        int store = Subscribers(this.fixture.Host.Skills, "SkillsChanged");
        int settings = Subscribers(this.fixture.Host.Settings, "Changed");
        _ = ctx.Render<SkillsPanel>();
        Assert.Equal(registry + 1, Subscribers(this.fixture.Host.Registry, "Changed"));

        await ctx.DisposeAsync();

        Assert.Equal(registry, Subscribers(this.fixture.Host.Registry, "Changed"));
        Assert.Equal(store, Subscribers(this.fixture.Host.Skills, "SkillsChanged"));
        Assert.Equal(settings, Subscribers(this.fixture.Host.Settings, "Changed"));
    }

    /// <summary>A tab header's badge counts that tab's New items (a downloaded Skill on Skills; Pack and catalog rows on
    /// Packs), and Sources has none.</summary>
    [Fact]
    public async Task TabBadges_CountNewItems()
    {
        this.fixture.Host.Settings.SetSeen([]);
        await this.fixture.SeedAsync("alpha", "beta");
        await using MudBunitContext ctx = this.fixture.NewContext();
        int newSkills = this.fixture.Host.Registry.Current.NewKeys.Count(static key => key.StartsWith("skill:", StringComparison.Ordinal));
        int newRows = this.fixture.Host.Registry.Current.NewKeys.Count - newSkills;

        IRenderedComponent<SkillsPanel> cut = ctx.Render<SkillsPanel>();

        Assert.Equal(1, newSkills);
        Assert.Equal(2, newRows);
        Assert.Equal(newSkills.ToString(System.Globalization.CultureInfo.InvariantCulture), cut.FindAll(".mud-tab")[0].QuerySelector(".mud-badge")?.TextContent.Trim());
        Assert.Equal(newRows.ToString(System.Globalization.CultureInfo.InvariantCulture), cut.FindAll(".mud-tab")[1].QuerySelector(".mud-badge")?.TextContent.Trim());
        Assert.Null(cut.FindAll(".mud-tab")[2].QuerySelector(".mud-badge"));
    }

    /// <summary>The section badge counts Broken rows plus SkillStore issues; with neither it is zero.</summary>
    [Fact]
    public async Task SectionBadge_CountsBrokenPlusStoreIssues()
    {
        await this.fixture.SeedAsync();
        Assert.Equal(0, SkillProblemCount.Of(this.fixture.Host.Registry, this.fixture.Host.Skills));

        string broken = Path.Combine(this.fixture.Host.Clone, "skills", "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "README.md"), "No SKILL.md.");
        this.fixture.Host.Pack("bad", "broken", broken);
        this.fixture.Host.SkillsMd(["packs/documents.skillpack.json", "packs/bad.skillpack.json"]);
        await this.fixture.Host.ReadAsync();
        int broke = SkillProblemCount.Of(this.fixture.Host.Registry, this.fixture.Host.Skills);
        Assert.True(broke >= 1);

        string invalid = Path.Combine(this.fixture.Host.Settings.SkillsDir, "not-a-skill");
        Directory.CreateDirectory(invalid);
        File.WriteAllText(Path.Combine(invalid, "SKILL.md"), "No frontmatter here.");
        await Eventually.UntilAsync(
            () => this.fixture.Host.Skills.Issues.Count != 0,
            Xunit.TestContext.Current.CancellationToken,
            timeout: TestBudgets.Slow,
            interval: TimeSpan.FromMilliseconds(50));

        Assert.Equal(broke + this.fixture.Host.Skills.Issues.Count, SkillProblemCount.Of(this.fixture.Host.Registry, this.fixture.Host.Skills));
        Assert.NotEmpty(this.fixture.Host.Skills.Issues);
    }

    /// <summary>How many handlers an event's backing field holds.</summary>
    private static int Subscribers(object owner, string eventName)
    {
        FieldInfo field = owner.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{owner.GetType().Name}.{eventName} has no backing field.");
        return (field.GetValue(owner) as Delegate)?.GetInvocationList().Length ?? 0;
    }
}
