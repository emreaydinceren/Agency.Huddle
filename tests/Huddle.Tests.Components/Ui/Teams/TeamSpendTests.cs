using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins NF-6: <c>TeamSpend</c> sums the persisted spend of the members per currency for the Team page
/// header, and the Team page hands it the Team's members.
/// </summary>
public sealed class TeamSpendTests
{
    /// <summary>The spend line sums the members' persisted spend per currency and is empty when nothing was spent.</summary>
    [Fact]
    public async Task Spend_SumsMembersPerCurrency()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());
        store.Add("Ada", null, "USD", 1.5m);
        store.Add("Dana", "T-1", "USD", 2m);
        store.Add("Dana", null, "EUR", 3m);
        store.Add("Outsider", null, "USD", 100m);
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(store);

        IRenderedComponent<TeamSpend> cut = ctx.Render<TeamSpend>(p => p.Add(x => x.Members, ["Ada", "Dana"]));
        IRenderedComponent<TeamSpend> none = ctx.Render<TeamSpend>(p => p.Add(x => x.Members, ["Nobody"]));

        Assert.Equal(["3.500 USD", "3.000 EUR"], cut.FindAll(".team-spend-amount").Select(item => item.TextContent.Trim()).Order(StringComparer.Ordinal).Reverse());
        Assert.Empty(none.FindAll(".team-spend"));
    }

    /// <summary>The Team page header hands the Team's members to the spend line.</summary>
    [Fact]
    public async Task TeamPage_Header_PassesMembersToSpend()
    {
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Business", [], ["Nova", "Kit"], true)] };
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        ctx.Services.AddSingleton(TestOptions.Create());
        ctx.ComponentFactories.AddStub<TeamRoster>();
        ctx.ComponentFactories.AddStub<TeamFilesTab>();
        ctx.ComponentFactories.AddStub<TeamTasksTab>();
        ctx.ComponentFactories.AddStub<TeamSpend>();

        IRenderedComponent<TeamPage> cut = ctx.Render<TeamPage>(p => p.Add(x => x.Team, "Business"));

        Assert.Equal(["Nova", "Kit"], cut.FindComponent<Stub<TeamSpend>>().Instance.Parameters.Get(x => x.Members));
    }
}
