using System.Text.RegularExpressions;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Components.Settings.Skills;
using Agency.Huddle.App.Skills.Ui;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins <see cref="SkillsHelpDialog"/> and <see cref="SkillGuide.HelpFor"/> (Skill Packs spec §6.9.12): Learn more on
/// each tab opens that tab's help with Close and navigates nowhere, and every help paragraph is in the User Guide word for word.</summary>
public sealed partial class SkillsHelpTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>Learn more on each tab opens the help titled with the tab, showing its text and a Close button, and the
    /// address does not change.</summary>
    [Theory]
    [InlineData(0, "skills", "Skills")]
    [InlineData(1, "packs", "Packs")]
    [InlineData(2, "sources", "Sources")]
    public async Task LearnMore_OpensThatTabsHelp_NavigatesNowhere(int tab, string view, string title)
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/settings/skills?view={view}");
        string address = navigation.Uri;
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<SkillsPanel>(0);
            builder.CloseComponent();
        });
        Assert.Equal(title, cut.FindAll(".mud-tab")[tab].TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".skills-learn-more").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(title, cut.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.Equal(Normalise(StripMarkdown(SkillGuide.HelpFor(view))), Normalise(cut.Find(".skills-help-text").TextContent));
        Assert.NotEmpty(cut.FindAll(".skills-help-close"));
        Assert.Equal(address, navigation.Uri);
    }

    /// <summary>The empty state's Learn more opens the Packs help.</summary>
    [Fact]
    public async Task EmptyState_LearnMore_OpensPacksHelp()
    {
        this.fixture.Host.SkillsMd([]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<PacksTab>(0);
            builder.CloseComponent();
        });

        await cut.InvokeAsync(() => cut.Find(".empty-state .skills-learn-more").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("Packs", cut.Find(".mud-dialog-title").TextContent.Trim()));
    }

    /// <summary>Every paragraph of each help text occurs word for word in the matching User Guide subsection, and no help
    /// text exceeds 250 words.</summary>
    [Theory]
    [InlineData("skills", "The Skills tab")]
    [InlineData("packs", "The Packs tab")]
    [InlineData("sources", "The Sources tab")]
    public void SkillsHelpText_IsInTheUserGuide(string view, string heading)
    {
        string guide = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "Huddle.UserGuide.md")).Replace("\r\n", "\n", StringComparison.Ordinal);
        int start = guide.IndexOf($"### {heading}\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The User Guide has no '{heading}' subsection.");
        int end = guide.IndexOf("\n#", start + 4, StringComparison.Ordinal);
        string subsection = guide[start..(end < 0 ? guide.Length : end)];
        string help = SkillGuide.HelpFor(view).Replace("\r\n", "\n", StringComparison.Ordinal);

        IReadOnlyList<string> paragraphs = [.. help.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        Assert.NotEmpty(paragraphs);
        Assert.All(paragraphs, paragraph => Assert.Contains(paragraph, subsection, StringComparison.Ordinal)); // contains-ok: each paragraph must occur in the guide's subsection, which holds more
        Assert.InRange(Words().Count(help), 1, 250);
    }

    /// <summary>The repository root, found from the test output folder.</summary>
    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Huddle.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Huddle.slnx was not found above the test output.");
    }

    /// <summary>The text with Markdown emphasis removed, as the rendered page reads.</summary>
    private static string StripMarkdown(string markdown) => markdown.Replace("**", string.Empty, StringComparison.Ordinal);

    /// <summary>Runs of white space collapsed to one space.</summary>
    private static string Normalise(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    [GeneratedRegex(@"\S+", RegexOptions.CultureInvariant)]
    private static partial Regex Words();
}
