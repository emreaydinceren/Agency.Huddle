using Agency.Huddle.App.Components.Settings.Skills;
using Agency.Huddle.App.Skills.Ui;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins <see cref="SkillsTab"/> (Skill Packs spec §6.9.3): a row per Skill, the Not downloaded row and its Copy prompt,
/// Built in, Hide and Show hidden, the State filter, and the virtualised grid for a very large registry.</summary>
public sealed class SkillsTabTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>There is one row per Skill: the built-ins, the downloaded Pack Skill and each catalog Skill.</summary>
    [Fact]
    public async Task Rows_OnePerSkill()
    {
        await this.fixture.SeedAsync("alpha", "beta");
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IReadOnlyList<string> names = [.. cut.FindAll("[data-skill]").Select(static cell => cell.GetAttribute("data-skill") ?? string.Empty)];
        Assert.Contains("alpha", names);
        Assert.Contains("beta", names);
        Assert.Contains("docx", names);
        Assert.Contains("team-building", names);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>A catalog Skill with no copy shows Not downloaded, and its Copy prompt copies the fetch prompt with the
    /// keyed snackbar.</summary>
    [Fact]
    public async Task NotDownloaded_ShowsChip_AndCopyPromptCopies()
    {
        await this.fixture.SeedAsync("alpha");
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("Not downloaded", Row(cut, "alpha").QuerySelector(".skill-state-chip-notdownloaded .status-chip")?.TextContent.Trim());
        await OpenMenuAsync(cut, "alpha");
        await cut.InvokeAsync(() => cut.Find(".skills-menu-copy-prompt").ClickAsync());

        JSRuntimeInvocation copy = Assert.Single(ctx.Module(JsModule.Clipboard).Invocations["copy"]);
        Assert.Contains("alpha", Assert.IsType<string>(copy.Arguments[0]), StringComparison.Ordinal); // contains-ok: the prompt text itself is pinned by SkillPromptBuilderTests
        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal(PromptClipboard.CopiedMessage, shown.Message);
    }

    /// <summary>A Skill's description carries its full text as the cell's tooltip, because the grid clamps it to two lines (#474).</summary>
    [Fact]
    public async Task Description_IsClampedByCssAndCarriesItsFullTextAsATooltip()
    {
        await this.fixture.SeedAsync("alpha");
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IElement description = Assert.IsAssignableFrom<IElement>(Row(cut, "alpha").QuerySelector(".skills-description"));
        Assert.False(string.IsNullOrWhiteSpace(description.TextContent));
        Assert.Equal(description.TextContent.Trim(), description.GetAttribute("title")?.Trim());
    }

    /// <summary>A built-in Skill shows Built in and no registry state.</summary>
    [Fact]
    public async Task BuiltIn_ShowsBuiltIn()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IElement row = Row(cut, "team-building");
        Assert.NotNull(row.QuerySelector(".skills-builtin-chip"));
        Assert.Null(row.QuerySelector(".skill-state-chip-available"));
    }

    /// <summary>The Source cell is one short name, never the joined repository, commit and licence line, and a Skill nobody holds reads "None" in Used by (#739).</summary>
    [Fact]
    public async Task SourceCell_IsAShortName_AndAnEmptyUsedByReadsNone()
    {
        await this.fixture.SeedAsync("alpha");
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IElement row = Row(cut, "alpha");
        string source = Assert.IsAssignableFrom<IElement>(row.QuerySelector(".skills-origin")).TextContent.Trim();
        Assert.DoesNotContain(" · ", source, StringComparison.Ordinal);
        Assert.Equal("None", Assert.IsAssignableFrom<IElement>(row.QuerySelector(".skills-used-by-none")).TextContent.Trim());
    }

    /// <summary>The Skills intro names "your AI tool" as what fetches Skills, never "your Agents": Agents do not fetch Skills (#739).</summary>
    [Fact]
    public async Task Intro_NamesYourAiTool_NotYourAgents()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        string intro = cut.Find(".settings-intro").TextContent.Trim();
        Assert.Equal(
            "A Skill is a folder of instructions that an Agent reads when its work calls for it. "
            + "This list shows every Skill Huddle knows about: built in, fetched by your AI tool, or listed in a catalog but not on this computer yet. "
            + "A Skill marked Not downloaded has no folder here; Copy prompt gives your AI tool what it needs to fetch it. "
            + "Huddle never downloads Skills itself. "
            + "Giving an Agent a Skill copies it to that Agent, so later changes reach it only when you apply an update.",
            intro);
    }

    /// <summary>Hide removes the row; Show hidden brings it back with the Hidden chip.</summary>
    [Fact]
    public async Task Hide_RemovesRow_ShowHiddenBringsItBack()
    {
        await this.fixture.SeedAsync("alpha");
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await OpenMenuAsync(cut, "alpha");
        await cut.InvokeAsync(() => cut.Find(".skills-menu-hide").ClickAsync());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-skill='alpha']")));

        await cut.InvokeAsync(() => cut.Find(".skills-tab-show-hidden input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true }));

        cut.WaitForAssertion(() => Assert.NotNull(Row(cut, "alpha").QuerySelector(".skill-state-chip-hidden")));
    }

    /// <summary>The State filter narrows the rows to that state.</summary>
    [Fact]
    public async Task StateFilter_NarrowsRows()
    {
        await this.fixture.SeedAsync("alpha", "beta");
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.Find(".skills-tab-state-filter button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-menu-item")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-menu-item").First(static item => item.TextContent.Trim() == "Not downloaded").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["alpha", "beta"], cut.FindAll("[data-skill]").Select(static cell => cell.GetAttribute("data-skill"))));
    }

    /// <summary>Import skill opens the prompt dialog on Add from a link or folder (Skill Packs spec §6.9.3).</summary>
    [Fact]
    public async Task ImportSkill_OpensThePromptDialogOnAdd()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.Find(".skills-import-button").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("Add with your AI tool", cut.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.NotEmpty(cut.FindAll(".add-with-ai-where"));
    }

    /// <summary>With five thousand catalog Skills the grid is virtualised and renders only a window of rows.</summary>
    [Fact]
    public async Task SkillsTab_FiveThousandRows_RendersWindowOnly()
    {
        this.fixture.Host.Source();
        string catalog = this.fixture.Host.Catalog("huge", [.. Enumerable.Range(0, 5000).Select(static index => $"skill-{index:D4}")]);
        this.fixture.Host.SkillsMd([], [catalog]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.True(this.fixture.Host.Registry.Current.Resolved.Skills.Count >= 5000);
        Assert.InRange(cut.FindAll("[data-skill]").Count, 0, SkillsTab.VirtualizeAbove);
    }

    /// <summary>Renders the tab with its popovers.</summary>
    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx)
    {
        return ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<SkillsTab>(0);
            builder.AddAttribute(1, nameof(SkillsTab.Revision), 1L);
            builder.CloseComponent();
        });
    }

    /// <summary>The grid row of a Skill.</summary>
    private static IElement Row(IRenderedComponent<ContainerFragment> cut, string name)
    {
        return cut.Find($"tr:has([data-skill='{name}'])");
    }

    /// <summary>Opens a row's ⋯ menu.</summary>
    private static async Task OpenMenuAsync(IRenderedComponent<ContainerFragment> cut, string name)
    {
        await cut.InvokeAsync(() => cut.Find($"tr:has([data-skill='{name}']) .skills-row-menu button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-menu-item")));
    }
}
