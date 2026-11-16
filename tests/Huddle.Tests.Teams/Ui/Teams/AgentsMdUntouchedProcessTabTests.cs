using Agency.Huddle.Tests.Factories;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>Pins SB-R08 (Spec §6.2 rule 4) for the Process tab: a confirmed gate switch-off rewrites <c>_team.md</c> and leaves <c>AGENTS.md</c> byte-identical.</summary>
public sealed class AgentsMdUntouchedProcessTabTests
{
    private const string Team = "Web Team";

    private const string Prose = "# Web Team\r\n\r\n## Notes\r\n\r\nKeep it tidy.é\r\n";

    /// <summary>Turning a gate off through the real provisioner writes the gates to <c>_team.md</c> and not a byte of <c>AGENTS.md</c>.</summary>
    [Fact]
    public async Task GateSwitchOff_Confirm_LeavesAgentsMdByteIdentical()
    {
        using TempDataDir dir = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(dir.Path);
        using PersonaStore personas = dir.CreatePersonaStore();
        await using MudBunitContext ctx = new();
        TeamFolderProvisioner folders = new(personas, library.RootStore, library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, new FakeTeamCatalog());
        TeamFile file = new TeamFile(new VersionedRef("web", "1.2.0"), "Nova", null, null, string.Empty)
        {
            Process = ProcessSettings.Empty with { Gates = [GateId.QaEvidenceBeforeDone, GateId.ChecksBeforeMerge] },
        };
        TeamCreateResult created = folders.CreateTeam(Team, file);
        Assert.Equal(TeamCreateOutcome.Created, created.Outcome);
        string prosePath = Path.Combine(Path.GetDirectoryName(created.Path) ?? string.Empty, TeamPaths.AgentsMdFileName);
        await File.WriteAllTextAsync(prosePath, Prose, Xunit.TestContext.Current.CancellationToken);
        ctx.Services.AddSingleton<ITeamFolders>(folders);
        NeedsYouLevelUiRig.AddTo(ctx, dir);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ProcessTab>(0);
            builder.AddAttribute(1, nameof(ProcessTab.Team), Team);
            builder.CloseComponent();
        });

        _ = cut.InvokeAsync(() => cut.Find("div[data-gate='qa-evidence-before-done'] input").ChangeAsync(new ChangeEventArgs { Value = false }));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(static button => string.Equals(button.TextContent.Trim(), "Turn off", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([GateId.ChecksBeforeMerge], folders.ReadTeamFile(Team)?.Process.Gates));
        Assert.Equal(Prose, await File.ReadAllTextAsync(prosePath, Xunit.TestContext.Current.CancellationToken));
    }
}
