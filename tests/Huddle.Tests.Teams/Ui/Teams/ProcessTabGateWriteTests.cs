using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>Pins how the Process tab writes a gate switch-off to <c>_team.md</c> and what it shows without an <c>AGENTS.md</c>.</summary>
public sealed class ProcessTabGateWriteTests
{
    private const string Team = "Web Team";

    /// <summary>Switching a gate off keeps the other <c>_team.md</c> keys, because it goes through <c>UpdateTeamFile</c>.</summary>
    [Fact]
    public async Task GateSwitchOff_Confirm_KeepsTheManagerKey()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        DiskTeamFolders folders = Arrange(ctx, dir);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        _ = cut.InvokeAsync(() => cut.Find("div[data-gate='qa-evidence-before-done'] input").ChangeAsync(new ChangeEventArgs { Value = false }));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => IElementButton(cut).ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([GateId.ChecksBeforeMerge], folders.File?.Process.Gates));
        Assert.Equal("Nova", folders.File?.Manager);
    }

    /// <summary>When <c>_team.md</c> cannot be saved, the tab shows the settings error and keeps the gate.</summary>
    [Fact]
    public async Task GateSwitchOff_SaveFails_ShowsTeamSettingsError()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        DiskTeamFolders folders = Arrange(ctx, dir);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        folders.TeamFilePath = Path.Combine(dir.Path, "missing-folder", "_team.md"); // the directory does not exist, so the write throws IOException

        _ = cut.InvokeAsync(() => cut.Find("div[data-gate='qa-evidence-before-done'] input").ChangeAsync(new ChangeEventArgs { Value = false }));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => IElementButton(cut).ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("Team settings could not be saved.", cut.Find(".process-tab-error").TextContent.Trim()));
    }

    /// <summary>With settings in <c>_team.md</c> and no <c>AGENTS.md</c>, the gate switches show and the empty notice does not.</summary>
    [Fact]
    public async Task Tab_GatesButNoAgentsMd_ShowsTheSwitches()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        _ = Arrange(ctx, dir);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.False(File.Exists(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName)));
        Assert.Equal(["qa-evidence-before-done", "checks-before-merge"], cut.FindAll("div.process-tab-gate").Select(static gate => gate.GetAttribute("data-gate")).ToList());
        Assert.DoesNotContain("This Team has no process yet", cut.Find(".process-tab").TextContent, StringComparison.Ordinal);
    }

    private static AngleSharp.Dom.IElement IElementButton(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll(".mud-dialog-actions button").Single(static button => string.Equals(button.TextContent.Trim(), "Turn off", StringComparison.Ordinal));

    private static DiskTeamFolders Arrange(MudBunitContext ctx, TempDataDir dir)
    {
        DiskTeamFolders folders = new(dir.Path)
        {
            File = new TeamFile(new VersionedRef("web", "1.2.0"), "Nova", null, null, string.Empty)
            {
                Process = ProcessSettings.Empty with { Gates = [GateId.QaEvidenceBeforeDone, GateId.ChecksBeforeMerge] },
            },
        };
        ctx.Services.AddSingleton<ITeamFolders>(folders);
        return folders;
    }

    private static IRenderedComponent<ContainerFragment> RenderTab(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ProcessTab>(0);
            builder.AddAttribute(1, nameof(ProcessTab.Team), Team);
            builder.CloseComponent();
        });
}
