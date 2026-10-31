using Agency.Huddle.App.Components.Settings.Skills;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.Tests.Waiting;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins <see cref="PackBar"/> (Skill Packs spec §6.9.5): the three buttons, the status line and Reload.</summary>
public sealed class PackBarTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>The bar has Add with your AI tool, Check for updates and Reload.</summary>
    [Fact]
    public async Task Bar_HasTheThreeButtons()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("Add with your AI tool", cut.Find(".pack-bar-add").TextContent.Trim());
        Assert.Equal("Check for updates", cut.Find(".pack-bar-updates").TextContent.Trim());
        Assert.Equal("Reload", cut.Find(".pack-bar-reload").TextContent.Trim());
    }

    /// <summary>The status line joins Sources, new, problems and the read time by "·" and leaves a zero count out.</summary>
    [Fact]
    public async Task Status_JoinsCounts_LeavesOutZero()
    {
        this.fixture.Host.Settings.SetSeen([]);
        this.fixture.Host.Source();
        string broken = Path.Combine(this.fixture.Host.Clone, "skills", "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "README.md"), "No SKILL.md.");
        this.fixture.Host.Pack("bad", "broken", broken);
        this.fixture.Host.SkillsMd(["packs/bad.skillpack.json"]);
        await this.fixture.Host.ReadAsync();
        this.fixture.Host.Clock.SetUtcNow(this.fixture.Host.Registry.Current.Resolved.ReadAt.AddSeconds(70));
        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.Services.AddSingleton<TimeProvider>(this.fixture.Host.Clock);

        IRenderedComponent<PackBar> cut = ctx.Render<PackBar>();

        Assert.Equal("1 Source · 1 new · 1 problem · SKILLS.md read 1 minute ago", cut.Find(".pack-bar-status").TextContent.Trim());

        await this.fixture.Host.Registry.MarkSeenAsync(this.fixture.Host.Registry.Current.NewKeys, Xunit.TestContext.Current.CancellationToken);
        cut.Render();
        Assert.Equal("1 Source · 1 problem · SKILLS.md read 1 minute ago", cut.Find(".pack-bar-status").TextContent.Trim());
    }

    /// <summary>Reload re-reads and ends with the keyed snackbar.</summary>
    [Fact]
    public async Task Reload_ReReads_AndShowsTheSnackbar()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        long before = this.fixture.Host.Registry.Current.Version;

        await cut.InvokeAsync(() => cut.Find(".pack-bar-reload").ClickAsync());

        Assert.True(this.fixture.Host.Registry.Current.Version > before);
        Assert.Equal("Reloaded. No problems.", Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars).Message);
        Assert.False(cut.Find(".pack-bar-reload").HasAttribute("disabled"));
    }

    /// <summary>
    /// Reload reports every problem it re-read, as the Skills tab's Reload does: a Skill store issue, which the Packs-scoped
    /// status line leaves out, still makes the snackbar say a problem was found.
    /// </summary>
    [Fact]
    public async Task Reload_StoreIssue_ReportsTheSameCountAsTheSkillsTab()
    {
        await this.fixture.SeedAsync();
        string invalid = Path.Combine(this.fixture.Host.Settings.SkillsDir, "not-a-skill");
        Directory.CreateDirectory(invalid);
        File.WriteAllText(Path.Combine(invalid, "SKILL.md"), "No frontmatter here.");
        await Eventually.UntilAsync(
            () => this.fixture.Host.Skills.Issues.Count != 0,
            Xunit.TestContext.Current.CancellationToken,
            timeout: TestBudgets.Slow,
            interval: TimeSpan.FromMilliseconds(50));
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.Find(".pack-bar-reload").ClickAsync());

        int problems = SkillProblemCount.Of(this.fixture.Host.Registry, this.fixture.Host.Skills);
        Assert.NotEqual(0, problems);
        Assert.Equal($"Reloaded. {Plural.Of(problems, "problem", "problems")} found.", Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars).Message);
        Assert.DoesNotContain("problem", cut.Find(".pack-bar-status").TextContent, StringComparison.Ordinal);
    }

    /// <summary>Renders the bar.</summary>
    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx)
    {
        return ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<PackBar>(0);
            builder.CloseComponent();
        });
    }
}
