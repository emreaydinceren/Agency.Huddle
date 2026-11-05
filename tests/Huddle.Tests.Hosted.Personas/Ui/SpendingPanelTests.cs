using System.Globalization;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// <c>SpendingPanel</c> (Settings › Spending, issue 356 Part 2): one row per Team, the Chief of Staff, each
/// Agent in no Team and the Helpers, then a Total per currency that counts every Agent once. Rows are
/// <c>.spending-row</c> with a <c>.spending-row-name</c> and one <c>.spending-row-amount</c> per currency; the Total is
/// one <c>.spending-total-amount</c> per currency; the Helpers row also has a <c>.spending-helper-calls</c>.
/// The panel is rendered over the factory's real stores, with the Team catalog replaced by a fake that lists the same
/// Teams the seeded Agents name.
/// </summary>
public sealed class SpendingPanelTests
{
    /// <summary>Formats an amount the way the panel does: three decimals, then the currency.</summary>
    private static string Money(decimal amount, string currency) =>
        $"{amount.ToString("N3", CultureInfo.InvariantCulture)} {currency}";

    /// <summary>Adds an Agent, in the given Teams, through the factory's real <see cref="PersonaStore"/>.</summary>
    private static void AddAgent(TeamWebApplicationFactory factory, string name, params string[] teams) =>
        factory.Services.GetRequiredService<PersonaStore>().Add(new PersonaIdentity(name, name, name.ToLowerInvariant(), teams), "body");

    /// <summary>The name of the seeded Chief of Staff, found by its built-in marker as the panel must.</summary>
    private static string ChiefOfStaff(TeamWebApplicationFactory factory) =>
        factory.Services.GetRequiredService<PersonaStore>().Entries
            .First(static entry => string.Equals(entry.Builtin, BuiltinAgent.ChiefOfStaffMarker, StringComparison.Ordinal)).Name;

    /// <summary>Records spend for an Agent straight in the store, as a restart would leave it.</summary>
    private static void Spend(TeamWebApplicationFactory factory, string name, decimal amount, string currency = "USD") =>
        factory.Services.GetRequiredService<PersonaSpendStore>().Add(name, null, currency, amount);

    /// <summary>Renders the panel with the Teams Alpha (Amy, Ben) and Beta (Amy) in the catalog.</summary>
    private static IRenderedComponent<SpendingPanel> Render(TeamWebApplicationFactory factory, out MudBunitContext context, bool archived = false)
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = archived
                ? []
                :
                [
                    new TeamSummary("Alpha", [], ["Amy", "Ben"], true),
                    new TeamSummary("Beta", [], ["Amy"], true),
                ],
        };
        MudBunitContext ctx = UiTestHost.Create(services => services.CopyFrom(factory.Services, ServicePresets.Agents));
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaSpendStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<HelperUsageStore>());
        context = ctx;
        return ctx.Render<SpendingPanel>();
    }

    /// <summary>The amounts shown on the row with the given name, or an empty list when there is no such row.</summary>
    private static IReadOnlyList<string> RowAmounts(IRenderedComponent<SpendingPanel> cut, string name) =>
        [.. cut.FindAll(".spending-row")
            .Where(row => string.Equals(row.QuerySelector(".spending-row-name")?.TextContent.Trim(), name, StringComparison.Ordinal))
            .SelectMany(static row => row.QuerySelectorAll(".spending-row-amount"))
            .Select(static amount => amount.TextContent.Trim())];

    /// <summary>The Total's amounts, one per currency.</summary>
    private static IReadOnlyList<string> TotalAmounts(IRenderedComponent<SpendingPanel> cut) =>
        [.. cut.FindAll(".spending-total-amount").Select(static amount => amount.TextContent.Trim())];

    /// <summary>A Team's row is the sum of its members' spend, as the Team page shows it.</summary>
    [Fact]
    public async Task TeamRow_SumsItsMembers()
    {
        await using TeamWebApplicationFactory factory = new();
        AddAgent(factory, "Amy", "Alpha", "Beta");
        AddAgent(factory, "Ben", "Alpha");
        Spend(factory, "Amy", 0.5m);
        Spend(factory, "Ben", 1m);
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;

        Assert.Equal([Money(1.5m, "USD")], RowAmounts(cut, "Alpha"));
        Assert.Equal([Money(0.5m, "USD")], RowAmounts(cut, "Beta"));
    }

    /// <summary>The Chief of Staff belongs to no Team and still has its own row.</summary>
    [Fact]
    public async Task ChiefOfStaff_HasItsOwnRow()
    {
        await using TeamWebApplicationFactory factory = new();
        Spend(factory, ChiefOfStaff(factory), 0.034m);
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;

        Assert.Equal([Money(0.034m, "USD")], RowAmounts(cut, ChiefOfStaff(factory)));
    }

    /// <summary>An Agent in no Team has its own row, and an Agent that is in a Team does not.</summary>
    [Fact]
    public async Task AgentInNoTeam_HasItsOwnRow_AndATeamMemberDoesNot()
    {
        await using TeamWebApplicationFactory factory = new();
        AddAgent(factory, "Ben", "Alpha");
        AddAgent(factory, "Cy");
        Spend(factory, "Ben", 1m);
        Spend(factory, "Cy", 0.2m);
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;

        Assert.Equal([Money(0.2m, "USD")], RowAmounts(cut, "Cy"));
        Assert.Empty(RowAmounts(cut, "Ben"));
    }

    /// <summary>The Helpers row shows their all-time cost and their call count.</summary>
    [Fact]
    public async Task HelpersRow_ShowsAllTimeCostAndCalls()
    {
        await using TeamWebApplicationFactory factory = new();
        HelperUsageStore helpers = factory.Services.GetRequiredService<HelperUsageStore>();
        HelperId id = new("NeedsHuman");
        helpers.AddCall(id, "2026-11-01");
        helpers.AddCall(id, "2026-11-01");
        helpers.AddCall(id, "2026-11-02");
        helpers.AddCost(id, "2026-11-01", 0.005m, "USD");
        helpers.AddCost(id, "2026-11-02", 0.003m, "USD");
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;

        Assert.Equal([Money(0.008m, "USD")], RowAmounts(cut, "Helpers"));
        Assert.Equal("3 calls", cut.Find(".spending-helper-calls").TextContent.Trim());
    }

    /// <summary>An Agent in two Teams is counted once in the Total, and a note says why the Team rows add up to more.</summary>
    [Fact]
    public async Task AgentInTwoTeams_IsCountedOnceInTheTotal()
    {
        await using TeamWebApplicationFactory factory = new();
        AddAgent(factory, "Amy", "Alpha", "Beta");
        AddAgent(factory, "Ben", "Alpha");
        Spend(factory, "Amy", 0.5m);
        Spend(factory, "Ben", 1m);
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;

        Assert.Equal([Money(1.5m, "USD")], TotalAmounts(cut));
        Assert.Single(cut.FindAll(".spending-overlap-note"));
    }

    /// <summary>With no Agent in two Teams there is no overlap note.</summary>
    [Fact]
    public async Task NoAgentInTwoTeams_ShowsNoOverlapNote()
    {
        await using TeamWebApplicationFactory factory = new();
        AddAgent(factory, "Ben", "Alpha");
        Spend(factory, "Ben", 1m);
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;

        Assert.Empty(cut.FindAll(".spending-overlap-note"));
    }

    /// <summary>Two currencies are two Total lines; they are never added together.</summary>
    [Fact]
    public async Task TwoCurrencies_StayApartInRowsAndTotal()
    {
        await using TeamWebApplicationFactory factory = new();
        AddAgent(factory, "Cy");
        Spend(factory, "Cy", 1m, "USD");
        Spend(factory, "Cy", 2m, "EUR");
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;

        Assert.Equal([Money(1m, "USD"), Money(2m, "EUR")], RowAmounts(cut, "Cy").OrderBy(static a => a, StringComparer.Ordinal).ToList());
        Assert.Equal([Money(1m, "USD"), Money(2m, "EUR")], TotalAmounts(cut).OrderBy(static a => a, StringComparer.Ordinal).ToList());
    }

    /// <summary>Nothing spent shows the shared empty state: no rows, no Total, never a row of zeros.</summary>
    [Fact]
    public async Task NothingSpent_ShowsTheEmptyState()
    {
        await using TeamWebApplicationFactory factory = new();
        AddAgent(factory, "Ben", "Alpha");
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;

        Assert.Single(cut.FindAll(".empty-state"));
        Assert.Empty(cut.FindAll(".spending-row"));
        Assert.Empty(cut.FindAll(".spending-total-amount"));
    }

    /// <summary>Once something is spent the empty state is gone.</summary>
    [Fact]
    public async Task SomethingSpent_HidesTheEmptyState()
    {
        await using TeamWebApplicationFactory factory = new();
        AddAgent(factory, "Cy");
        Spend(factory, "Cy", 0.2m);
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;

        Assert.Empty(cut.FindAll(".empty-state"));
    }

    /// <summary>A new Adapter report reaches an open panel without a reload (the page must not go stale).</summary>
    [Fact]
    public async Task SpendChanged_UpdatesAnOpenPanel()
    {
        await using TeamWebApplicationFactory factory = new();
        AddAgent(factory, "Cy");
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;
        Assert.Empty(RowAmounts(cut, "Cy"));

        factory.Services.GetRequiredService<PersonaSpend>().Add("Cy", "s1", 0.25m, "USD");

        cut.WaitForAssertion(() => Assert.Equal([Money(0.25m, "USD")], RowAmounts(cut, "Cy")));
    }

    /// <summary>The panel unsubscribes from spend changes when it is disposed.</summary>
    [Fact]
    public async Task Dispose_Unsubscribes()
    {
        await using TeamWebApplicationFactory factory = new();
        PersonaSpend spend = factory.Services.GetRequiredService<PersonaSpend>();
        int before = spend.SubscriberCount;
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;
        Assert.Single(cut.FindAll(".spending-panel"));
        Assert.True(spend.SubscriberCount > before);

        await ctx.DisposeComponentsOnDispatcherAsync();

        Assert.Equal(before, spend.SubscriberCount);
    }

    /// <summary>
    /// An archived Team has left the catalog and its members lost the label, but their spend is still stored: each
    /// shows as its own row and the Total still counts it, so archiving a Team never makes spend vanish.
    /// </summary>
    [Fact]
    public async Task ArchivedTeam_MembersSpendStaysInRowsAndTotal()
    {
        await using TeamWebApplicationFactory factory = new();
        AddAgent(factory, "Amy");
        AddAgent(factory, "Ben");
        Spend(factory, "Amy", 0.5m);
        Spend(factory, "Ben", 1m);
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx, archived: true);
        await using MudBunitContext _ = ctx;

        Assert.Empty(RowAmounts(cut, "Alpha"));
        Assert.Equal([Money(0.5m, "USD")], RowAmounts(cut, "Amy"));
        Assert.Equal([Money(1m, "USD")], RowAmounts(cut, "Ben"));
        Assert.Equal([Money(1.5m, "USD")], TotalAmounts(cut));
    }

    /// <summary>The panel says it covers only what Adapters report and that removing an Agent removes its spend.</summary>
    [Fact]
    public async Task Panel_StatesItsScope()
    {
        await using TeamWebApplicationFactory factory = new();
        IRenderedComponent<SpendingPanel> cut = Render(factory, out MudBunitContext ctx);
        await using MudBunitContext _ = ctx;

        string text = cut.Find(".settings-intro").TextContent;
        Assert.Matches("only the cost Adapters report", text);
        Assert.Matches("Removing an Agent removes its spend", text);
    }
}
