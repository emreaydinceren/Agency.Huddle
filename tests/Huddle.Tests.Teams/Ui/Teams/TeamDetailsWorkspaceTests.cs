using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>Pins the missing-workspace warning on the Team page's Details tab (Spec §6.4, Settled text T18).</summary>
public sealed class TeamDetailsWorkspaceTests
{
    private const string WarningSelector = ".team-details-workspace-warning[role='status']";

    /// <summary>A Team whose workspace folder cannot be read shows exactly the T18 sentence, with the path, as a <c>role="status"</c> Warning: it reports a state to fix later, not an act now (Q-A1-1).</summary>
    [Fact]
    public async Task DetailsTab_UnreadableWorkspace_ShowsT18()
    {
        using TempDataDir dir = new();
        string missing = Path.Combine(dir.Path, "gone");
        await using MudBunitContext ctx = NewContext(new TeamSummary("Business", [], ["Nova"], true) { Workspace = missing });

        IRenderedComponent<TeamDetails> cut = ctx.Render<TeamDetails>(p => p.Add(x => x.Team, "Business"));

        Assert.Equal($"The workspace folder {missing} can't be read, so Agents aren't told to use it.", Assert.Single(cut.FindAll(WarningSelector)).TextContent.Trim());
    }

    /// <summary>A Team whose workspace folder is readable shows no warning.</summary>
    [Fact]
    public async Task DetailsTab_ReadableWorkspace_ShowsNoWarning()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = NewContext(new TeamSummary("Business", [], ["Nova"], true) { Workspace = dir.Path });

        IRenderedComponent<TeamDetails> cut = ctx.Render<TeamDetails>(p => p.Add(x => x.Team, "Business"));

        Assert.Empty(cut.FindAll(WarningSelector));
    }

    /// <summary>A Team with no workspace shows no warning.</summary>
    [Fact]
    public async Task DetailsTab_NoWorkspace_ShowsNoWarning()
    {
        await using MudBunitContext ctx = NewContext(new TeamSummary("Business", [], ["Nova"], true));

        IRenderedComponent<TeamDetails> cut = ctx.Render<TeamDetails>(p => p.Add(x => x.Team, "Business"));

        Assert.Empty(cut.FindAll(WarningSelector));
    }

    /// <summary>A context with a fake catalog holding <paramref name="team"/> and the build form replaced by a stub.</summary>
    private static MudBunitContext NewContext(TeamSummary team)
    {
        FakeTeamCatalog catalog = new() { Teams = [team] };
        MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton<ITeamCatalog>(catalog));
        ctx.ComponentFactories.AddStub<BuildTeam>();
        return ctx;
    }
}
