using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins the one-Task-at-a-time control of ADR-0103: the Team's switch and the Project's Inherit, One at a time and Parallel choice write the
/// right value to <c>_team.md</c> and <c>_project.md</c>, and the hint under Inherit says what Inherit resolves to.
/// </summary>
public sealed class OneInProgressControlTests
{
    private const string Team = "Dev";
    private const string Project = "Site";

    private static readonly VersionedRef Type = new("development", "1.0.0");

    private static FakeTeamFolders Arrange(MudBunitContext ctx, RuleSetting team)
    {
        FakeTeamFolders folders = new()
        {
            UpdateResult = true,
            ReadResult = new TeamFile(Type, null, null, null, string.Empty) { OneInProgressPerProject = team },
        };
        ctx.Services.AddSingleton<ITeamFolders>(folders);
        return folders;
    }

    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx, string? project) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<OneInProgressControl>(0);
            builder.AddAttribute(1, nameof(OneInProgressControl.Team), Team);
            if (project is not null)
            {
                builder.AddAttribute(2, nameof(OneInProgressControl.Project), project);
            }

            builder.CloseComponent();
        });

    private static Task ChooseAsync(IRenderedComponent<ContainerFragment> cut, string label)
    {
        IElement item = cut.FindAll(".one-in-progress .segmented-group .mud-toggle-item").First(item => string.Equals(item.TextContent.Trim(), label, StringComparison.Ordinal));
        return cut.InvokeAsync(() => item.ClickAsync(new()));
    }

    private static string Hint(IRenderedComponent<ContainerFragment> cut) => cut.Find(".one-in-progress-hint").TextContent.Trim();

    /// <summary>Switching the Team's switch on writes <c>on</c> to the Team's file.</summary>
    [Fact]
    public async Task TeamSwitch_On_WritesOn()
    {
        await using MudBunitContext ctx = new();
        FakeTeamFolders folders = Arrange(ctx, RuleSetting.Absent);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, project: null);

        await cut.InvokeAsync(() => cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = true }));

        Assert.Equal(RuleSetting.On, folders.ReadResult?.OneInProgressPerProject);
    }

    /// <summary>Switching the Team's switch off removes the key (absent means off) and keeps the other Team keys.</summary>
    [Fact]
    public async Task TeamSwitch_Off_RemovesTheKey()
    {
        await using MudBunitContext ctx = new();
        FakeTeamFolders folders = Arrange(ctx, RuleSetting.On);
        folders.ReadResult = Assert.IsType<TeamFile>(folders.ReadResult) with { Manager = "Nova" };
        IRenderedComponent<ContainerFragment> cut = Render(ctx, project: null);

        await cut.InvokeAsync(() => cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = false }));

        TeamFile written = Assert.IsType<TeamFile>(folders.ReadResult);
        Assert.Equal(RuleSetting.Absent, written.OneInProgressPerProject);
        Assert.Equal("Nova", written.Manager);
        Assert.DoesNotContain(OneInProgressRule.Key, written.Compose(), StringComparison.Ordinal);
    }

    /// <summary>A Team file that cannot be written shows an error under the switch.</summary>
    [Fact]
    public async Task TeamSwitch_SaveRefused_ShowsAnError()
    {
        await using MudBunitContext ctx = new();
        FakeTeamFolders folders = Arrange(ctx, RuleSetting.Absent);
        folders.UpdateResult = false;
        IRenderedComponent<ContainerFragment> cut = Render(ctx, project: null);

        await cut.InvokeAsync(() => cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = true }));

        Assert.NotEmpty(cut.FindAll(".one-in-progress-error"));
    }

    /// <summary>A Project that sets nothing shows Inherit, and its hint says what the Team's value makes of it.</summary>
    [Theory]
    [InlineData("on", "Inherit (Team: One at a time)")]
    [InlineData("", "Inherit (Team: Parallel)")]
    [InlineData("off", "Inherit (Team: Parallel)")]
    public async Task Project_SetsNothing_HintResolvesInherit(string team, string expected)
    {
        await using MudBunitContext ctx = new();
        _ = Arrange(ctx, OneInProgressRule.Parse(team));

        IRenderedComponent<ContainerFragment> cut = Render(ctx, Project);

        Assert.Equal(expected, Hint(cut));
        Assert.Equal("Inherit", cut.Find(".one-in-progress .segmented-active-item").TextContent.Trim());
    }

    /// <summary>Choosing One at a time writes <c>on</c> to the Project's file.</summary>
    [Fact]
    public async Task Project_ChooseOneAtATime_WritesOn()
    {
        await using MudBunitContext ctx = new();
        FakeTeamFolders folders = Arrange(ctx, RuleSetting.Absent);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, Project);

        await ChooseAsync(cut, "One at a time");

        Assert.Equal(RuleSetting.On, folders.ProjectFiles[Project].OneInProgressPerProject);
    }

    /// <summary>Choosing Parallel writes <c>off</c> to the Project's file, and Inherit afterwards removes the key again.</summary>
    [Fact]
    public async Task Project_ChooseParallelThenInherit_WritesOffThenRemovesTheKey()
    {
        await using MudBunitContext ctx = new();
        FakeTeamFolders folders = Arrange(ctx, RuleSetting.On);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, Project);

        await ChooseAsync(cut, "Parallel");
        RuleSetting afterParallel = folders.ProjectFiles[Project].OneInProgressPerProject;
        await ChooseAsync(cut, "Inherit");

        Assert.Equal(RuleSetting.Off, afterParallel);
        Assert.Equal(RuleSetting.Absent, folders.ProjectFiles[Project].OneInProgressPerProject);
        Assert.Equal("Inherit (Team: One at a time)", Hint(cut));
    }

    /// <summary>A Project that sets nothing is not written just by being shown.</summary>
    [Fact]
    public async Task Project_Shown_WritesNothing()
    {
        await using MudBunitContext ctx = new();
        FakeTeamFolders folders = Arrange(ctx, RuleSetting.On);

        _ = Render(ctx, Project);

        Assert.DoesNotContain(folders.Calls, static call => call.StartsWith("Update", StringComparison.Ordinal));
    }
}
