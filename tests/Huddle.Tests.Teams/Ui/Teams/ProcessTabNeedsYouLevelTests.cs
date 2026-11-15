using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>Pins that the Process tab offers the Needs You level (ADR-0087) and no longer offers the accept-before-merge switch the level replaced.</summary>
public sealed class ProcessTabNeedsYouLevelTests
{
    private static IRenderedComponent<ContainerFragment> RenderTab(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ProcessTab>(0);
            builder.AddAttribute(1, nameof(ProcessTab.Team), NeedsYouLevelUiRig.Team);
            builder.CloseComponent();
        });

    /// <summary>A Team with the accept-before-merge gate shows its other gates as switches, not that one, and points to the level.</summary>
    [Fact]
    public async Task Tab_AcceptBeforeMergeGate_HasNoSwitchAndPointsToTheLevel()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        rig.Folders.ReadResult = new TeamFile(new VersionedRef("development", "1.0.0"), null, null, null, string.Empty)
        {
            Process = ProcessSettings.Empty with { Gates = [GateId.AcceptBeforeMerge, GateId.ChecksBeforeMerge] },
        };

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Equal(["checks-before-merge"], cut.FindAll("div.process-tab-gate").Select(static gate => gate.GetAttribute("data-gate")).ToList());
        Assert.DoesNotContain("Accept before merge", cut.Find(".process-tab").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("This Team asks before a merge into main. The level above decides what that waits for.", cut.Find(".process-tab-level .process-tab-gate-note").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".process-tab-level .needs-you-level"));
    }

    /// <summary>A Team whose only gate is accept-before-merge shows the level and no switch at all.</summary>
    [Fact]
    public async Task Tab_OnlyAcceptBeforeMerge_ShowsNoSwitches()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        rig.Folders.ReadResult = new TeamFile(new VersionedRef("development", "1.0.0"), null, null, null, string.Empty)
        {
            Process = ProcessSettings.Empty with { Gates = [GateId.AcceptBeforeMerge] },
        };

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Empty(cut.FindAll("div.process-tab-gate"));
        Assert.Empty(cut.FindAll(".process-tab .mud-switch"));
        Assert.NotEmpty(cut.FindAll(".process-tab-level .needs-you-level"));
    }
}
