using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Task 33 (HR-8, NF-6): <c>TeamCheckBarView</c> renders the coverage chips filled or empty, the
/// vendor and process counts and the blockers; <c>TeamSpend</c> sums the persisted spend of the
/// members per currency for the Team page header.
/// </summary>
public sealed class TeamCheckBarViewTests
{
    /// <summary>A chip is filled for a covered step and empty for an uncovered one, labelled by the step.</summary>
    [Fact]
    public async Task View_CoverageChips_MatchCoverage()
    {
        await using MudBunitContext ctx = new();
        TeamCheck check = new(
            new Dictionary<string, bool>(StringComparer.Ordinal) { ["generate"] = true, ["challenge"] = false, ["verify"] = true },
            0, 0, true, []);

        IRenderedComponent<TeamCheckBarView> cut = ctx.Render<TeamCheckBarView>(p => p.Add(x => x.Check, check));

        Assert.Equal(["Generate", "Challenge", "Verify"], cut.FindAll(".team-check-chip").Select(item => item.TextContent.Trim()));
        Assert.Single(cut.FindAll(".team-check-chip-generate.team-check-chip-filled"));
        Assert.Single(cut.FindAll(".team-check-chip-challenge.team-check-chip-empty"));
        Assert.Single(cut.FindAll(".team-check-chip-verify.team-check-chip-filled"));
    }

    /// <summary>The bar says how many vendors and processes the Team would run (NF-6).</summary>
    [Fact]
    public async Task View_ShowsVendorAndProcessCounts()
    {
        await using MudBunitContext ctx = new();
        TeamCheck check = new(new Dictionary<string, bool>(StringComparer.Ordinal), 2, 3, true, []);

        IRenderedComponent<TeamCheckBarView> cut = ctx.Render<TeamCheckBarView>(p => p.Add(x => x.Check, check));

        Assert.Equal("2 vendors", cut.Find(".team-check-vendors").TextContent.Trim());
        Assert.Equal("3 processes", cut.Find(".team-check-processes").TextContent.Trim());
    }

    /// <summary>The blockers are listed one per item, and no list is drawn when there are none.</summary>
    [Fact]
    public async Task View_ListsBlockers()
    {
        await using MudBunitContext ctx = new();
        TeamCheck blocked = new(new Dictionary<string, bool>(StringComparer.Ordinal), 0, 1, false, ["Ravi needs setup: no adapter.", "The team needs a Lead."]);
        TeamCheck clear = new(new Dictionary<string, bool>(StringComparer.Ordinal), 0, 1, true, []);

        IRenderedComponent<TeamCheckBarView> withBlockers = ctx.Render<TeamCheckBarView>(p => p.Add(x => x.Check, blocked));
        IRenderedComponent<TeamCheckBarView> without = ctx.Render<TeamCheckBarView>(p => p.Add(x => x.Check, clear));

        Assert.Equal(["Ravi needs setup: no adapter.", "The team needs a Lead."], withBlockers.FindAll(".team-check-blockers li").Select(item => item.TextContent.Trim()));
        Assert.Empty(without.FindAll(".team-check-blockers"));
    }

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
        ctx.Services.AddSingleton(Options.Create(new TeamOptions()));
        ctx.ComponentFactories.AddStub<TeamRoster>();
        ctx.ComponentFactories.AddStub<TeamFilesTab>();
        ctx.ComponentFactories.AddStub<TeamTasksTab>();
        ctx.ComponentFactories.AddStub<TeamSpend>();

        IRenderedComponent<TeamPage> cut = ctx.Render<TeamPage>(p => p.Add(x => x.Team, "Business"));

        Assert.Equal(["Nova", "Kit"], cut.FindComponent<Stub<TeamSpend>>().Instance.Parameters.Get(x => x.Members));
    }
}
