using Agency.Huddle.App.Components.Settings.Skills;
using Agency.Huddle.App.SkillSources;
using Bunit;
using Bunit.Rendering;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins Hide (Skill Packs spec §6.12.4) from the Skills tab: the row leaves, the snackbar says so with Undo, Undo
/// brings it back, a malformed <c>hidden.json</c> is refused, and the Source's log records it.</summary>
public sealed class HideTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>Hide removes the row and shows "alpha hidden" with Undo; Undo restores it and hidden.json no longer lists it.</summary>
    [Fact]
    public async Task Hide_ThenUndo_RestoresTheRow()
    {
        await this.fixture.SeedAsync("alpha");
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.Find("tr:has([data-skill='alpha']) .skills-row-menu button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".skills-menu-hide")));
        await cut.InvokeAsync(() => cut.Find(".skills-menu-hide").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-skill='alpha']")));
        Assert.Equal("alpha hidden", cut.Find(".mud-snackbar-content-message").TextContent.Trim());
        Assert.Contains("alpha", this.fixture.Host.Hidden.Read().Skills);
        using (System.Text.Json.JsonDocument line = System.Text.Json.JsonDocument.Parse(File.ReadAllLines(Path.Combine(this.fixture.Host.Dir, "sources", "local", "test", SourceLog.FileName))[^1]))
        {
            Assert.Equal("hidden", line.RootElement.GetProperty("event").GetString());
            Assert.Equal("skill:alpha", line.RootElement.GetProperty("subject").GetString());
        }

        await cut.InvokeAsync(() => cut.Find(".mud-snackbar-content-action button").ClickAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-skill='alpha']")));
        Assert.DoesNotContain("alpha", this.fixture.Host.Hidden.Read().Skills);
    }

    /// <summary>A malformed hidden.json is not overwritten and Hide says so in an Error snackbar.</summary>
    [Fact]
    public async Task Hide_MalformedFile_Refused()
    {
        await this.fixture.SeedAsync("alpha");
        File.WriteAllText(this.fixture.Host.Hidden.FilePath, "{ broken");
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.Find("tr:has([data-skill='alpha']) .skills-row-menu button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".skills-menu-hide")));
        await cut.InvokeAsync(() => cut.Find(".skills-menu-hide").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(HideFlow.RefusedMessage, cut.Find(".mud-snackbar-content-message").TextContent.Trim()));
        Assert.Equal("{ broken", File.ReadAllText(this.fixture.Host.Hidden.FilePath));
        Assert.NotEmpty(cut.FindAll("[data-skill='alpha']"));
    }

    /// <summary>Renders the snackbar provider and the Skills section, which repaints on a new registry snapshot.</summary>
    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx)
    {
        return ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<MudSnackbarProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<Agency.Huddle.App.Components.Settings.SkillsPanel>(1);
            builder.CloseComponent();
        });
    }
}
